using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CustomCodeFramework.Redis.Streams.Messages;
using Dhole.Notifications.Domain.Notifications.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dhole.Notifications.Workers.Streams;

internal static class StreamNotificationSafety
{
    private static readonly TimeSpan MaxBacklogAge = TimeSpan.FromHours(24);

    public static Guid? FixedMessageId(RedisStreamEnvelope envelope)
    {
        // Redis stream IDs change on every publish. Prefer a stable business
        // operation ID so republishing the same Outbox operation cannot send
        // another email, even if its stream ID is different.
        if (!string.IsNullOrWhiteSpace(envelope.PayloadJson))
        {
            using var document = JsonDocument.Parse(envelope.PayloadJson);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                var key = GetString(root, "idempotencyKey")
                    ?? GetString(root, "deduplicationKey");
                if (!string.IsNullOrWhiteSpace(key))
                    return StableId($"notifications:business:{envelope.MessageType}:{key.Trim()}");

                // Compatibility with Pricing events already in the Outbox
                // before the idempotency key was added. Ignore attachments:
                // regenerated PDFs may have different binary metadata.
                var type = GetString(root, "notificationType");
                var entityId = GetString(root, "entityId");
                var channel = GetString(root, "channel");
                if (type?.StartsWith("pricing.", StringComparison.OrdinalIgnoreCase) == true
                    && !string.IsNullOrWhiteSpace(entityId)
                    && string.Equals(channel, "Email", StringComparison.OrdinalIgnoreCase))
                {
                    var recipients = GetRecipients(root);
                    var revision = TryGet(root, "payload", out var payload)
                        ? GetScalar(payload, "revisionNumber") : null;
                    var fingerprint = string.Join("\u001f", new[]
                    {
                        type.ToLowerInvariant(),
                        entityId.Trim().ToLowerInvariant(),
                        channel!.ToLowerInvariant(),
                        GetString(root, "subject") ?? string.Empty,
                        GetString(root, "body") ?? string.Empty,
                        revision ?? string.Empty,
                        string.Join(",", recipients),
                    });
                    return StableId("notifications:pricing-fallback:v1:" + fingerprint);
                }
            }
        }

        var streamId = envelope.MessageId?.ToString();
        return string.IsNullOrWhiteSpace(streamId)
            ? null
            : StableId($"notifications:redis:{envelope.MessageType}:{streamId}");
    }

    private static Guid StableId(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static string[] GetRecipients(JsonElement root)
    {
        if (TryGet(root, "recipients", out var array) && array.ValueKind == JsonValueKind.Array)
            return array.EnumerateArray()
                .Select(x => (GetString(x, "address") ?? GetString(x, "userId") ?? string.Empty)
                    .Trim().ToLowerInvariant())
                .Where(x => x.Length > 0)
                .Distinct()
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

        return new[] { GetString(root, "recipient") ?? GetString(root, "address") ?? string.Empty }
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => x.Length > 0)
            .ToArray();
    }

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    continue;
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string name)
        => TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static string? GetScalar(JsonElement element, string name)
        => TryGet(element, name, out var value)
           && value.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? value.ToString() : null;

    public static bool IsStale(RedisStreamEnvelope envelope, DateTime? scheduledForUtc = null)
    {
        var now = DateTime.UtcNow;
        if (scheduledForUtc.HasValue && scheduledForUtc.Value.ToUniversalTime() > now) return false;
        // Redis ID: <unix-milliseconds>-<sequence>
        var streamId = envelope.MessageId?.ToString();
        var separator = streamId?.IndexOf('-') ?? -1;
        if (separator <= 0 ||
            !long.TryParse(streamId![..separator], NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds))
            return false;
        try { return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime < now - MaxBacklogAge; }
        catch (ArgumentOutOfRangeException) { return false; }
    }

    public static bool IsDuplicateInsert(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
           && exception.Entries.Any(entry => entry.Entity is NotificationMessage);
}
