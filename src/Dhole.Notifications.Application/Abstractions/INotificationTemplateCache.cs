using Dhole.Notifications.Contracts.Notifications;

namespace Dhole.Notifications.Application.Abstractions;

public interface INotificationTemplateCache
{
    Task<NotificationTemplateDto?> GetAsync(string code, CancellationToken cancellationToken = default);
    Task SetAsync(NotificationTemplateDto template, CancellationToken cancellationToken = default);
    Task RemoveAsync(string code, CancellationToken cancellationToken = default);
}
