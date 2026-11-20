using System.Text.Json;
using CustomCodeFramework.Redis.Streams.Abstractions;
using CustomCodeFramework.Redis.Streams.Messages;
using Dhole.Notifications.Infrastructure.Realtime;

namespace Dhole.Notifications.Workers.Streams;

/// <summary>
/// Realtime-only policy invalidation: never creates a user inbox notification.
/// The receiving client always re-reads effective policy from Auth.
/// </summary>
internal sealed class VersionUpdatePreferenceChangedHandler(
    SystemNotificationRealtimeBus bus,
    ILogger<VersionUpdatePreferenceChangedHandler> logger) : IRedisStreamMessageHandler
{
    public string MessageType => "auth.version_update_notifications.changed";
    private const string NotificationType = "system.version-update-policy.changed";

    public async Task HandleAsync(RedisStreamEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(envelope.PayloadJson) || StreamNotificationSafety.IsStale(envelope))
            return;

        using var doc = JsonDocument.Parse(envelope.PayloadJson);
        var payload = doc.RootElement;
        if (!payload.TryGetProperty("scope", out var scopeProperty) ||
            scopeProperty.ValueKind != JsonValueKind.String)
            return;

        var scope = scopeProperty.GetString();
        Guid userId = Guid.Empty;
        if (string.Equals(scope, "user", StringComparison.Ordinal))
        {
            if (!payload.TryGetProperty("userId", out var userProperty) ||
                !Guid.TryParse(userProperty.GetString(), out userId))
                return;
        }
        else if (!string.Equals(scope, "global", StringComparison.Ordinal))
        {
            return;
        }

        // Deterministic event key across retries supports client-side deduplication.
        var eventId = StreamNotificationSafety.FixedMessageId(envelope) ?? Guid.NewGuid();
        var pushed = new SystemNotificationPush(
            eventId,
            eventId,
            userId, // Guid.Empty signals global policy invalidation to the relay.
            NotificationType,
            null,
            null,
            JsonSerializer.Serialize(new { scope }),
            "SystemVersionUpdatePreference",
            null,
            DateTime.UtcNow
        );

        cancellationToken.ThrowIfCancellationRequested();
        await bus.PublishAsync(pushed);
        logger.LogDebug("Version update preference invalidation relayed. Scope={Scope} Target={UserId} Event={EventId}",
            scope, userId, eventId);
    }
}
