namespace Dhole.Notifications.Contracts.Notifications;

public sealed record CreateNotificationTemplateRequest(
    string Code,
    string Name,
    string? Description,
    string NotificationType,
    string Channel,
    string? SubjectTemplate,
    string BodyTemplate,
    string DesignerJson);

public sealed record UpdateNotificationTemplateRequest(
    string Name,
    string? Description,
    string NotificationType,
    string Channel,
    string? SubjectTemplate,
    string BodyTemplate,
    string DesignerJson);

public sealed record SetNotificationTemplateActiveRequest(bool IsActive);

public sealed record NotificationRecipientRequest(
    Guid? UserId,
    string Address,
    string? DisplayName);

public sealed record CreateNotificationMessageRequest(
    string NotificationType,
    string? TemplateCode,
    string Channel,
    string? EntityType,
    string? EntityId,
    string? Subject,
    string? Body,
    string? PayloadJson,
    DateTime? ScheduledForUtc,
    int MaxAttempts,
    IReadOnlyCollection<NotificationRecipientRequest> Recipients);
