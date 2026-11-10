using System.Text;
using System.Text.Json;
using StackExchange.Redis;

namespace Dhole.Notifications.Api.Hubs;

// Consume existing producers' Redis Streams in Notifications rather than
// building a second hub or publisher in ApiGateway. Redis Pub/Sub is only
// a low-latency notification; reconnect always reconciles via REST.
public sealed class NotificationsResourceStreamConsumer(
    NotificationsRealtimeRuntime runtime,
    NotificationsResourceRealtimeBus bus,
    ILogger<NotificationsResourceStreamConsumer> logger) : BackgroundService
{
    private const string ConsumerGroup = "dhole-notifications-resource-v1";
    private static readonly IReadOnlyDictionary<string, (string Prefix, string Producer)> Sources =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["dhole.crm.realtime.v1"] = ("crm", "DholeCRMService"),
            ["dhole.pricing.realtime.v1"] = ("pricing", "DholePricingService"),
            ["dhole.auth.realtime.v1"] = ("auth", "DholeAuthService"),
            ["dhole.config.realtime.v1"] = ("config", "DholeConfigService"),
            ["dhole.auditlogs.realtime.v1"] = ("audit", "DholeAuditLogsService"),
            ["dhole.storage.realtime.v1"] = ("storage", "DholeStorageService"),
            ["dhole.data-extraction.realtime.v1"] = ("extraction", "DholeDataExtractionService"),
            ["dhole.reports.realtime.v1"] = ("reports", "DholeReportsService"),
            ["dhole.dynamics.realtime.v1"] = ("dynamics", "DholeDynamicsConnectorService"),
            ["dhole.ai.realtime.v1"] = ("ai", "DholeAIService"),
            ["dhole.agent.realtime.v1"] = ("agent", "DholeAgentService"),
            ["dhole.content.realtime.v1"] = ("content", "DholeContentService")
        };

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!runtime.Enabled) return Task.CompletedTask;
        return Task.WhenAll(Sources.Keys.Select(name => ConsumeAsync(name, stoppingToken)));
    }

    private async Task ConsumeAsync(string name, CancellationToken ct)
    {
        var database = bus.Database;
        var consumer = Environment.MachineName + "-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            // A fresh pilot starts from current traffic, not historic mutations.
            await database.StreamCreateConsumerGroupAsync(name, ConsumerGroup,
                "$", createStream: true);
        }
        catch (RedisServerException e) when (
            e.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
            // Existing group/cursor survives restart.
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var entries = await database.StreamReadGroupAsync(
                    name, ConsumerGroup, consumer, ">", count: 50);
                if (entries.Length == 0)
                {
                    var recovery = await database.StreamAutoClaimAsync(
                        name, ConsumerGroup, consumer, 30_000, "0-0", count: 50);
                    entries = recovery.ClaimedEntries;
                }
                if (entries.Length == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);
                    continue;
                }

                foreach (var entry in entries)
                {
                    ct.ThrowIfCancellationRequested();
                    var fields = entry.Values.ToDictionary(x => x.Name.ToString(),
                        x => x.Value.ToString(), StringComparer.Ordinal);
                    fields.TryGetValue("payload_json", out var json);
                    if (!fields.TryGetValue("event_id", out var eventId))
                        fields.TryGetValue("header:event_id", out eventId);

                    if (TryParseAuthSecurityEvent(name, eventId, json, out var security))
                    {
                        // Auth user/session events bypass record-owner ACL because a
                        // blocked user can no longer pass it. Only their existing
                        // authenticated user group receives this minimal signal.
                        await bus.PublishSecurityAsync(security!);
                    }
                    else if (TryParseEvent(name, eventId, json, out var push))
                    {
                        // Publish before ACK: a crash can replay; consumers dedupe
                        // by eventId, and REST is authoritative.
                        await bus.PublishAsync(push!);
                    }
                    else
                    {
                        logger.LogWarning("Skipped invalid realtime envelope from {Stream}", name);
                    }
                    await database.StreamAcknowledgeAsync(name, ConsumerGroup, entry.Id);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "Notifications resource stream retry: {Stream}", name);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    public static bool TryParseAuthSecurityEvent(string stream, string? eventIdText,
        string? json, out AuthSecurityRealtimePush? result)
    {
        result = null;
        if (stream != "dhole.auth.realtime.v1" ||
            !Guid.TryParse(eventIdText, out var expectedId) || expectedId == Guid.Empty ||
            string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > 2000)
            return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("eventId", out var eventId) ||
                !Guid.TryParse(eventId.GetString(), out var id) || id != expectedId ||
                !root.TryGetProperty("eventType", out var eventType) ||
                eventType.GetString() != "auth.security.changed.v1" ||
                !root.TryGetProperty("schemaVersion", out var schema) ||
                schema.GetInt32() != 1 ||
                !root.TryGetProperty("producer", out var producer) ||
                producer.GetString() != "DholeAuthService" ||
                !root.TryGetProperty("aggregateType", out var aggregateType) ||
                aggregateType.GetString() != "user" ||
                !root.TryGetProperty("aggregateId", out var aggregateId) ||
                !Guid.TryParse(aggregateId.GetString(), out var userId) ||
                userId == Guid.Empty ||
                !root.TryGetProperty("audienceRef", out var audienceRef) ||
                audienceRef.GetString() != $"auth:user:{userId:D}" ||
                !root.TryGetProperty("action", out var actionField) ||
                actionField.GetString() is not ("forceLogout" or "refreshPermissions") ||
                !root.TryGetProperty("occurredAtUtc", out var occurredAtField) ||
                !DateTimeOffset.TryParse(occurredAtField.GetString(), out var occurredAt) ||
                occurredAt.Offset != TimeSpan.Zero)
                return false;

            Guid? sessionId = null;
            if (root.TryGetProperty("targetSessionId", out var sessionField) &&
                sessionField.ValueKind != JsonValueKind.Null)
            {
                if (sessionField.ValueKind != JsonValueKind.String ||
                    !Guid.TryParse(sessionField.GetString(), out var parsed) ||
                    parsed == Guid.Empty) return false;
                sessionId = parsed;
            }
            var action = actionField.GetString()!;
            if (action == "refreshPermissions" && sessionId is not null) return false;
            result = new AuthSecurityRealtimePush(id, userId, sessionId, action, occurredAt);
            return true;
        }
        catch (Exception error) when (
            error is JsonException or FormatException or InvalidOperationException)
        {
            return false;
        }
    }

    public static bool TryParseEvent(string stream, string? eventIdText, string? json,
        out ResourceRealtimePush? result)
    {
        result = null;
        if (!Sources.TryGetValue(stream, out var source) ||
            !Guid.TryParse(eventIdText, out var expectedId) || expectedId == Guid.Empty ||
            string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > 6000)
            return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("eventId", out var idField) ||
                !Guid.TryParse(idField.GetString(), out var id) || id != expectedId ||
                !root.TryGetProperty("eventType", out var typeField) ||
                typeField.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("schemaVersion", out var schema) ||
                schema.GetInt32() <= 0 ||
                !root.TryGetProperty("aggregateType", out var typeOfAggregate) ||
                !root.TryGetProperty("aggregateId", out var aggregateField) ||
                !Guid.TryParse(aggregateField.GetString(), out var aggregateId) ||
                aggregateId == Guid.Empty ||
                !root.TryGetProperty("aggregateVersion", out var versionField) ||
                !versionField.TryGetInt64(out var version) || version <= 0 ||
                !root.TryGetProperty("occurredAtUtc", out var timeField) ||
                !DateTimeOffset.TryParse(timeField.GetString(), out var when) ||
                when.Offset != TimeSpan.Zero ||
                !root.TryGetProperty("audienceRef", out var audienceField) ||
                !ResourceRealtimePolicy.TryFromAudience(audienceField.GetString(), out var resource) ||
                resource.Id != aggregateId)
                return false;

            var type = typeField.GetString();
            var kind = resource.Kind;
            if (type is null || type.Length > 160 || !type.EndsWith(".v1", StringComparison.Ordinal))
                return false;

            if (stream == "dhole.crm.realtime.v1" || stream == "dhole.pricing.realtime.v1")
            {
                if (kind != source.Prefix + "." + typeOfAggregate.GetString() ||
                    !type.StartsWith(kind + ".", StringComparison.Ordinal))
                    return false;
            }
            else
            {
                if (!root.TryGetProperty("producer", out var producer) ||
                    producer.GetString() != source.Producer)
                    return false;
                var exactKind = kind.StartsWith(source.Prefix + ".", StringComparison.Ordinal) &&
                    kind[(source.Prefix.Length + 1)..] == typeOfAggregate.GetString();
                var supplierAlias = source.Prefix == "config" &&
                    kind == "srm.supplier" && typeOfAggregate.GetString() == "supplier";
                if (!exactKind && !supplierAlias) return false;
                var expectedType = kind == "audit.event"
                    ? "audit.event.created.v1"
                    : kind + ".changed.v1";
                if (!string.Equals(type, expectedType, StringComparison.Ordinal)) return false;
            }

            result = new ResourceRealtimePush(kind, new ResourceInvalidation(
                id, type, typeOfAggregate.GetString() ?? "", aggregateId, version, when));
            return true;
        }
        catch (Exception e) when (
            e is JsonException or FormatException or InvalidOperationException)
        {
            return false;
        }
    }
}
