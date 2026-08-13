namespace Dhole.Notifications.Infrastructure.Realtime;

public sealed record SystemNotificationPush(
    Guid NotificationId,
    Guid RecipientId,
    Guid UserId,
    string NotificationType,
    string? Subject,
    string? Body,
    string PayloadJson,
    string? EntityType,
    string? EntityId,
    DateTime OccurredAtUtc
);
