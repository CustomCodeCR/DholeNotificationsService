using CustomCodeFramework.Core.Domain.Entities;
using Dhole.Notifications.Domain.Notifications.Enums;

namespace Dhole.Notifications.Domain.Notifications.Entities;

public sealed class NotificationMessage : SoftDeletableAggregateRoot<Guid>
{
    private readonly List<NotificationRecipient> _recipients = [];
    private readonly List<NotificationDeliveryAttempt> _deliveryAttempts = [];

    private NotificationMessage() { }

    private NotificationMessage(
        Guid id,
        string notificationType,
        string? templateCode,
        NotificationChannel channel,
        string? entityType,
        string? entityId,
        string? subject,
        string? body,
        string payloadJson,
        DateTime? scheduledForUtc,
        int maxAttempts,
        Guid? createdBy)
        : base(id)
    {
        NotificationType = notificationType.Trim();
        TemplateCode = string.IsNullOrWhiteSpace(templateCode) ? null : templateCode.Trim().ToLowerInvariant();
        Channel = channel;
        EntityType = NormalizeOptional(entityType);
        EntityId = NormalizeOptional(entityId);
        Subject = NormalizeOptional(subject);
        Body = NormalizeOptional(body);
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
        ScheduledForUtc = scheduledForUtc;
        MaxAttempts = Math.Clamp(maxAttempts, 1, 20);
        Status = scheduledForUtc.HasValue && scheduledForUtc.Value > DateTime.UtcNow
            ? NotificationStatus.Scheduled
            : NotificationStatus.Pending;
        MarkAsCreated(DateTime.UtcNow, createdBy?.ToString());
    }

    public string NotificationType { get; private set; } = string.Empty;
    public string? TemplateCode { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string? EntityType { get; private set; }
    public string? EntityId { get; private set; }
    public string? Subject { get; private set; }
    public string? Body { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public NotificationStatus Status { get; private set; }
    public DateTime? ScheduledForUtc { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public DateTime? ProcessingStartedAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? LastErrorMessage { get; private set; }

    public IReadOnlyCollection<NotificationRecipient> Recipients => _recipients;
    public IReadOnlyCollection<NotificationDeliveryAttempt> DeliveryAttempts => _deliveryAttempts;

    public static NotificationMessage Create(
        string notificationType,
        string? templateCode,
        NotificationChannel channel,
        string? entityType,
        string? entityId,
        string? subject,
        string? body,
        string payloadJson,
        DateTime? scheduledForUtc,
        int maxAttempts = 3,
        Guid? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(notificationType)) throw new ArgumentException("Notification type is required.", nameof(notificationType));
        return new NotificationMessage(Guid.NewGuid(), notificationType, templateCode, channel, entityType, entityId,
            subject, body, payloadJson, scheduledForUtc, maxAttempts, createdBy);
    }

    public NotificationRecipient AddRecipient(Guid? userId, string address, string? displayName = null)
    {
        var recipient = NotificationRecipient.Create(Id, userId, address, displayName);
        _recipients.Add(recipient);
        return recipient;
    }

    public void MarkProcessing()
    {
        if (Status is NotificationStatus.Cancelled or NotificationStatus.Sent or NotificationStatus.DeadLetter) return;
        Status = NotificationStatus.Processing;
        ProcessingStartedAtUtc = DateTime.UtcNow;
        AttemptCount++;
        NextAttemptAtUtc = null;
        MarkAsUpdated(DateTime.UtcNow, null);
    }

    public void SetRenderedContent(string? subject, string body)
    {
        Subject = NormalizeOptional(subject);
        Body = body;
        MarkAsUpdated(DateTime.UtcNow, null);
    }

    public void AddDeliveryAttempt(NotificationDeliveryAttempt attempt) => _deliveryAttempts.Add(attempt);

    public void MarkSent()
    {
        Status = NotificationStatus.Sent;
        SentAtUtc = DateTime.UtcNow;
        LastErrorCode = null;
        LastErrorMessage = null;
        MarkAsUpdated(DateTime.UtcNow, null);
    }

    public void MarkFailed(string errorCode, string errorMessage, TimeSpan retryDelay)
    {
        LastErrorCode = NormalizeOptional(errorCode);
        LastErrorMessage = NormalizeOptional(errorMessage);
        if (AttemptCount >= MaxAttempts)
        {
            Status = NotificationStatus.DeadLetter;
            NextAttemptAtUtc = null;
        }
        else
        {
            Status = NotificationStatus.Retrying;
            NextAttemptAtUtc = DateTime.UtcNow.Add(retryDelay);
        }
        MarkAsUpdated(DateTime.UtcNow, null);
    }

    public void MarkTerminalFailed(string errorCode, string errorMessage)
    {
        Status = NotificationStatus.Failed;
        LastErrorCode = NormalizeOptional(errorCode);
        LastErrorMessage = NormalizeOptional(errorMessage);
        NextAttemptAtUtc = null;
        MarkAsUpdated(DateTime.UtcNow, null);
    }

    public void Cancel(Guid? cancelledBy = null)
    {
        if (Status is NotificationStatus.Sent or NotificationStatus.DeadLetter) return;
        Status = NotificationStatus.Cancelled;
        CancelledAtUtc = DateTime.UtcNow;
        NextAttemptAtUtc = null;
        MarkAsUpdated(DateTime.UtcNow, cancelledBy?.ToString());
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
