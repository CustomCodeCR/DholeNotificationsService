using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
        var eventId = envelope.MessageId?.ToString();
        if (string.IsNullOrWhiteSpace(eventId)) return null;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"notifications:redis:{envelope.MessageType}:{eventId}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes.AsSpan(0, 16));
    }

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
