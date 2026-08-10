using CustomCodeFramework.Core.Domain.Entities;

namespace Dhole.Notifications.Domain.Notifications.Entities;

public sealed class NotificationDeliveryAttempt : Entity<Guid>
{
    private NotificationDeliveryAttempt() { }

    private NotificationDeliveryAttempt(
        Guid id,
        Guid notificationMessageId,
        Guid notificationRecipientId,
        int attemptNumber,
        bool succeeded,
        string? provider,
        string? providerMessageId,
        string? errorCode,
        string? errorMessage,
        DateTime startedAtUtc,
        DateTime completedAtUtc)
        : base(id)
    {
        NotificationMessageId = notificationMessageId;
        NotificationRecipientId = notificationRecipientId;
        AttemptNumber = attemptNumber;
        Succeeded = succeeded;
        Provider = provider;
        ProviderMessageId = providerMessageId;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
    }

    public Guid NotificationMessageId { get; private set; }
    public Guid NotificationRecipientId { get; private set; }
    public int AttemptNumber { get; private set; }
    public bool Succeeded { get; private set; }
    public string? Provider { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime CompletedAtUtc { get; private set; }

    public static NotificationDeliveryAttempt Create(
        Guid notificationMessageId,
        Guid notificationRecipientId,
        int attemptNumber,
        bool succeeded,
        string? provider,
        string? providerMessageId,
        string? errorCode,
        string? errorMessage,
        DateTime startedAtUtc,
        DateTime completedAtUtc)
        => new(Guid.NewGuid(), notificationMessageId, notificationRecipientId, attemptNumber, succeeded,
            provider, providerMessageId, errorCode, errorMessage, startedAtUtc, completedAtUtc);
}
