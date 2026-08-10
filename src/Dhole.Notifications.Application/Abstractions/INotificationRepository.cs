using Dhole.Notifications.Domain.Notifications.Entities;
using Dhole.Notifications.Domain.Notifications.Enums;

namespace Dhole.Notifications.Application.Abstractions;

public interface INotificationRepository
{
    Task<bool> TemplateCodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<NotificationTemplate?> GetTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NotificationTemplate?> GetTemplateByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddTemplateAsync(NotificationTemplate template, CancellationToken cancellationToken = default);
    Task<(IReadOnlyCollection<NotificationTemplate> Items, int Total)> BrowseTemplatesAsync(int pageNumber, int pageSize, string? search, bool? isActive, CancellationToken cancellationToken = default);

    Task AddMessageAsync(NotificationMessage message, CancellationToken cancellationToken = default);
    Task<NotificationMessage?> GetMessageByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyCollection<NotificationMessage> Items, int Total)> BrowseMessagesAsync(int pageNumber, int pageSize, string? search, NotificationStatus? status, string? channel, CancellationToken cancellationToken = default);
    Task<(IReadOnlyCollection<NotificationMessage> Items, int Total)> BrowseByEntityAsync(string entityType, string entityId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<(IReadOnlyCollection<NotificationMessage> Items, int Total)> BrowseByRecipientAsync(string recipient, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<NotificationMessage>> GetDueMessagesAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default);
}
