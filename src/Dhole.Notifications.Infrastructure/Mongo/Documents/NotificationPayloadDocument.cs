namespace Dhole.Notifications.Infrastructure.Mongo.Documents;

public sealed class NotificationPayloadDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string NotificationId { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class NotificationRenderedMessageDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string NotificationId { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime RenderedAtUtc { get; set; }
}
