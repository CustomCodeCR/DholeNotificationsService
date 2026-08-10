using CustomCodeFramework.Mongo.Abstractions;
using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Infrastructure.Mongo.Documents;

namespace Dhole.Notifications.Infrastructure.Mongo;

public sealed class NotificationDocumentStore(IMongoContext mongoContext) : INotificationDocumentStore
{
    public Task SavePayloadAsync(Guid notificationId, string notificationType, string payloadJson, DateTime createdAtUtc, CancellationToken cancellationToken = default)
        => mongoContext.GetCollection<NotificationPayloadDocument>().InsertOneAsync(new NotificationPayloadDocument
        {
            NotificationId = notificationId.ToString(),
            NotificationType = notificationType,
            PayloadJson = payloadJson,
            CreatedAtUtc = createdAtUtc,
        }, cancellationToken: cancellationToken);

    public Task SaveRenderedMessageAsync(Guid notificationId, string channel, string? subject, string body, DateTime renderedAtUtc, CancellationToken cancellationToken = default)
        => mongoContext.GetCollection<NotificationRenderedMessageDocument>().InsertOneAsync(new NotificationRenderedMessageDocument
        {
            NotificationId = notificationId.ToString(),
            Channel = channel,
            Subject = subject,
            Body = body,
            RenderedAtUtc = renderedAtUtc,
        }, cancellationToken: cancellationToken);
}
