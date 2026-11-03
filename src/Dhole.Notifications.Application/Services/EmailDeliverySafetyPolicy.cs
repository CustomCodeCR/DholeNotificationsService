using Dhole.Notifications.Domain.Notifications.Entities;

namespace Dhole.Notifications.Application.Services;

/// <summary>
/// At-most-once safeguards for SMTP and per-quotation burst limits.
/// These guards deliberately fail closed: an uncertain delivery requires review.
/// </summary>
public static class EmailDeliverySafetyPolicy
{
    public const string StartedAttemptCode = "email_delivery_started";
    public const int MaxSameTypePerQuotePerHour = 3;
    public const int MaxTotalPerQuotePerHour = 10;

    public static bool HasUnconfirmedStart(NotificationMessage message, Guid recipientId)
        => message.DeliveryAttempts.Any(attempt =>
            attempt.NotificationRecipientId == recipientId
            && attempt.ErrorCode == StartedAttemptCode);

    public static bool ShouldBlockQuoteBurst(string? entityType, string? entityId, int total, int sameType)
        => string.Equals(entityType, "RateHeader", StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(entityId)
           && (sameType >= MaxSameTypePerQuotePerHour || total >= MaxTotalPerQuotePerHour);
}
