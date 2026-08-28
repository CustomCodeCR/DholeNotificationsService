namespace Dhole.Notifications.Contracts.Notifications;

public sealed record NotificationTemplateDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string NotificationType,
    string Channel,
    string? SubjectTemplate,
    string BodyTemplate,
    string DesignerJson,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record NotificationRecipientDto(
    Guid Id,
    Guid? UserId,
    string Address,
    string? DisplayName);

public sealed record NotificationDeliveryAttemptDto(
    Guid Id,
    Guid NotificationRecipientId,
    int AttemptNumber,
    bool Succeeded,
    string? Provider,
    string? ProviderMessageId,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc);

public sealed record NotificationMessageDto(
    Guid Id,
    string NotificationType,
    string? TemplateCode,
    string Channel,
    string? EntityType,
    string? EntityId,
    string? Subject,
    string? Body,
    string Status,
    DateTime? ScheduledForUtc,
    DateTime? NextAttemptAtUtc,
    DateTime? SentAtUtc,
    int AttemptCount,
    int MaxAttempts,
    string? LastErrorCode,
    string? LastErrorMessage,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyCollection<NotificationRecipientDto> Recipients,
    IReadOnlyCollection<NotificationDeliveryAttemptDto> DeliveryAttempts);

public sealed record NotificationInboxItemDto(
    Guid RecipientId,
    Guid NotificationId,
    string NotificationType,
    string? Subject,
    string? Body,
    string PayloadJson,
    string? EntityType,
    string? EntityId,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

public sealed record NotificationUnreadCountDto(int UnreadCount);

public sealed record PagedNotificationResult<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize)));
}
