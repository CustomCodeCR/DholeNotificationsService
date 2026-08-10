using Dhole.Notifications.Contracts.Notifications;

namespace Dhole.Notifications.Application.Abstractions;

public interface INotificationApplicationService
{
    Task<NotificationTemplateDto> CreateTemplateAsync(CreateNotificationTemplateRequest request, Guid? actorUserId, CancellationToken cancellationToken = default);
    Task<NotificationTemplateDto?> UpdateTemplateAsync(Guid id, UpdateNotificationTemplateRequest request, Guid? actorUserId, CancellationToken cancellationToken = default);
    Task<bool> SetTemplateActiveAsync(Guid id, bool isActive, Guid? actorUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteTemplateAsync(Guid id, Guid? actorUserId, CancellationToken cancellationToken = default);
    Task<NotificationTemplateDto?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedNotificationResult<NotificationTemplateDto>> BrowseTemplatesAsync(int pageNumber, int pageSize, string? search, bool? isActive, CancellationToken cancellationToken = default);

    Task<NotificationMessageDto> CreateMessageAsync(CreateNotificationMessageRequest request, Guid? actorUserId, CancellationToken cancellationToken = default);
    Task<NotificationMessageDto?> GetMessageAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CancelMessageAsync(Guid id, Guid? actorUserId, CancellationToken cancellationToken = default);
    Task<PagedNotificationResult<NotificationMessageDto>> BrowseMessagesAsync(int pageNumber, int pageSize, string? search, string? status, string? channel, CancellationToken cancellationToken = default);
    Task<PagedNotificationResult<NotificationMessageDto>> BrowseByEntityAsync(string entityType, string entityId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedNotificationResult<NotificationMessageDto>> BrowseByRecipientAsync(string recipient, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken = default);
}
