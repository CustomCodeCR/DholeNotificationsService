namespace Dhole.Notifications.Application.Abstractions;

public interface INotificationTemplateRenderer
{
    RenderedNotification Render(string? subjectTemplate, string bodyTemplate, string payloadJson);
}

public sealed record RenderedNotification(string? Subject, string Body);
