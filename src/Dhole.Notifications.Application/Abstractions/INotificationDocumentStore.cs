namespace Dhole.Notifications.Application.Abstractions;

public interface INotificationDocumentStore
{
    Task SavePayloadAsync(Guid notificationId, string notificationType, string payloadJson, DateTime createdAtUtc, CancellationToken cancellationToken = default);
    Task SaveRenderedMessageAsync(Guid notificationId, string channel, string? subject, string body, DateTime renderedAtUtc, CancellationToken cancellationToken = default);
}
