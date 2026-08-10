using CustomCodeFramework.Persistence.Abstractions;
using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Contracts.Notifications;
using Dhole.Notifications.Domain.Notifications.Entities;
using Dhole.Notifications.Domain.Notifications.Enums;

namespace Dhole.Notifications.Application.Services;

public sealed class NotificationApplicationService(
    INotificationRepository repository,
    INotificationTemplateCache templateCache,
    INotificationDocumentStore documentStore,
    INotificationPendingQueue pendingQueue,
    INotificationTemplateRenderer renderer,
    IEnumerable<INotificationDeliveryChannel> deliveryChannels,
    IUnitOfWork unitOfWork)
    : INotificationApplicationService
{
    private readonly IReadOnlyDictionary<NotificationChannel, INotificationDeliveryChannel> _deliveryChannels =
        deliveryChannels.GroupBy(x => x.Channel).ToDictionary(x => x.Key, x => x.First());

    public async Task<NotificationTemplateDto> CreateTemplateAsync(CreateNotificationTemplateRequest request, Guid? actorUserId, CancellationToken cancellationToken = default)
    {
        var channel = ParseChannel(request.Channel);
        var code = request.Code.Trim().ToLowerInvariant();
        if (await repository.TemplateCodeExistsAsync(code, cancellationToken: cancellationToken))
            throw new InvalidOperationException($"A notification template with code '{code}' already exists.");

        var template = NotificationTemplate.Create(code, request.Name, request.Description, request.NotificationType,
            channel, request.SubjectTemplate, request.BodyTemplate, request.DesignerJson, actorUserId);
        await repository.AddTemplateAsync(template, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var dto = template.ToDto();
        await templateCache.SetAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<NotificationTemplateDto?> UpdateTemplateAsync(Guid id, UpdateNotificationTemplateRequest request, Guid? actorUserId, CancellationToken cancellationToken = default)
    {
        var template = await repository.GetTemplateByIdAsync(id, cancellationToken);
        if (template is null) return null;
        template.Update(request.Name, request.Description, request.NotificationType, ParseChannel(request.Channel),
            request.SubjectTemplate, request.BodyTemplate, request.DesignerJson, actorUserId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var dto = template.ToDto();
        await templateCache.SetAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<bool> SetTemplateActiveAsync(Guid id, bool isActive, Guid? actorUserId, CancellationToken cancellationToken = default)
    {
        var template = await repository.GetTemplateByIdAsync(id, cancellationToken);
        if (template is null) return false;
        template.SetActive(isActive, actorUserId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (isActive) await templateCache.SetAsync(template.ToDto(), cancellationToken);
        else await templateCache.RemoveAsync(template.Code, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteTemplateAsync(Guid id, Guid? actorUserId, CancellationToken cancellationToken = default)
    {
        var template = await repository.GetTemplateByIdAsync(id, cancellationToken);
        if (template is null) return false;
        template.Delete(actorUserId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await templateCache.RemoveAsync(template.Code, cancellationToken);
        return true;
    }

    public async Task<NotificationTemplateDto?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default)
        => (await repository.GetTemplateByIdAsync(id, cancellationToken))?.ToDto();

    public async Task<PagedNotificationResult<NotificationTemplateDto>> BrowseTemplatesAsync(int pageNumber, int pageSize, string? search, bool? isActive, CancellationToken cancellationToken = default)
    {
        (pageNumber, pageSize) = NormalizePage(pageNumber, pageSize);
        var (items, total) = await repository.BrowseTemplatesAsync(pageNumber, pageSize, search, isActive, cancellationToken);
        return new(items.Select(x => x.ToDto()).ToArray(), pageNumber, pageSize, total);
    }

    public async Task<NotificationMessageDto> CreateMessageAsync(CreateNotificationMessageRequest request, Guid? actorUserId, CancellationToken cancellationToken = default)
    {
        var channel = ParseChannel(request.Channel);
        if (request.Recipients is null || request.Recipients.Count == 0)
            throw new InvalidOperationException("At least one recipient is required.");

        NotificationTemplateDto? template = null;
        if (!string.IsNullOrWhiteSpace(request.TemplateCode))
        {
            var code = request.TemplateCode.Trim().ToLowerInvariant();
            template = await templateCache.GetAsync(code, cancellationToken);
            if (template is null)
            {
                var entity = await repository.GetTemplateByCodeAsync(code, cancellationToken);
                if (entity is null || !entity.IsActive) throw new InvalidOperationException($"Template '{code}' was not found or is inactive.");
                template = entity.ToDto();
                await templateCache.SetAsync(template, cancellationToken);
            }
            if (!string.Equals(template.Channel, channel.ToString(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The requested channel does not match the template channel.");
        }

        if (template is null && string.IsNullOrWhiteSpace(request.Body))
            throw new InvalidOperationException("Body is required when no template is provided.");

        var payloadJson = string.IsNullOrWhiteSpace(request.PayloadJson) ? "{}" : request.PayloadJson!;
        _ = System.Text.Json.JsonDocument.Parse(payloadJson);

        var message = NotificationMessage.Create(request.NotificationType, request.TemplateCode, channel,
            request.EntityType, request.EntityId, request.Subject, request.Body, payloadJson,
            request.ScheduledForUtc?.ToUniversalTime(), request.MaxAttempts <= 0 ? 3 : request.MaxAttempts, actorUserId);
        foreach (var recipient in request.Recipients)
            message.AddRecipient(recipient.UserId, recipient.Address ?? string.Empty, recipient.DisplayName);

        await repository.AddMessageAsync(message, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await documentStore.SavePayloadAsync(message.Id, message.NotificationType, payloadJson, message.CreatedAtUtc, cancellationToken);
        await pendingQueue.EnqueueAsync(message.Id, message.ScheduledForUtc ?? DateTime.UtcNow, cancellationToken);
        return message.ToDto();
    }

    public async Task<NotificationMessageDto?> GetMessageAsync(Guid id, CancellationToken cancellationToken = default)
        => (await repository.GetMessageByIdAsync(id, cancellationToken))?.ToDto();

    public async Task<bool> CancelMessageAsync(Guid id, Guid? actorUserId, CancellationToken cancellationToken = default)
    {
        var message = await repository.GetMessageByIdAsync(id, cancellationToken);
        if (message is null) return false;
        message.Cancel(actorUserId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await pendingQueue.RemoveAsync(id, cancellationToken);
        return true;
    }

    public async Task<PagedNotificationResult<NotificationMessageDto>> BrowseMessagesAsync(int pageNumber, int pageSize, string? search, string? status, string? channel, CancellationToken cancellationToken = default)
    {
        (pageNumber, pageSize) = NormalizePage(pageNumber, pageSize);
        NotificationStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<NotificationStatus>(status, true, out var s)) parsedStatus = s;
        var (items, total) = await repository.BrowseMessagesAsync(pageNumber, pageSize, search, parsedStatus, channel, cancellationToken);
        return new(items.Select(x => x.ToDto()).ToArray(), pageNumber, pageSize, total);
    }

    public async Task<PagedNotificationResult<NotificationMessageDto>> BrowseByEntityAsync(string entityType, string entityId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        (pageNumber, pageSize) = NormalizePage(pageNumber, pageSize);
        var (items, total) = await repository.BrowseByEntityAsync(entityType, entityId, pageNumber, pageSize, cancellationToken);
        return new(items.Select(x => x.ToDto()).ToArray(), pageNumber, pageSize, total);
    }

    public async Task<PagedNotificationResult<NotificationMessageDto>> BrowseByRecipientAsync(string recipient, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        (pageNumber, pageSize) = NormalizePage(pageNumber, pageSize);
        var (items, total) = await repository.BrowseByRecipientAsync(recipient, pageNumber, pageSize, cancellationToken);
        return new(items.Select(x => x.ToDto()).ToArray(), pageNumber, pageSize, total);
    }

    public async Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var due = await repository.GetDueMessagesAsync(Math.Clamp(batchSize, 1, 200), DateTime.UtcNow, cancellationToken);
        var processed = 0;
        foreach (var message in due)
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                message.MarkProcessing();
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var rendered = await RenderAsync(message, cancellationToken);
                message.SetRenderedContent(rendered.Subject, rendered.Body);
                await documentStore.SaveRenderedMessageAsync(message.Id, message.Channel.ToString(), rendered.Subject, rendered.Body, DateTime.UtcNow, cancellationToken);

                if (!_deliveryChannels.TryGetValue(message.Channel, out var deliveryChannel))
                    throw new InvalidOperationException($"No delivery channel is registered for {message.Channel}.");

                var failures = new List<NotificationDeliveryResult>();
                foreach (var recipient in message.Recipients)
                {
                    if (message.DeliveryAttempts.Any(x => x.NotificationRecipientId == recipient.Id && x.Succeeded)) continue;
                    var started = DateTime.UtcNow;
                    NotificationDeliveryResult result;
                    try
                    {
                        result = await deliveryChannel.SendAsync(message, recipient, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        result = NotificationDeliveryResult.Failure(message.Channel.ToString(), "delivery_exception", ex.Message);
                    }
                    var attempt = NotificationDeliveryAttempt.Create(message.Id, recipient.Id, message.AttemptCount,
                        result.Succeeded, result.Provider, result.ProviderMessageId, result.ErrorCode, result.ErrorMessage,
                        started, DateTime.UtcNow);
                    message.AddDeliveryAttempt(attempt);
                    if (!result.Succeeded) failures.Add(result);
                }

                if (failures.Count == 0)
                {
                    message.MarkSent();
                    await pendingQueue.RemoveAsync(message.Id, cancellationToken);
                }
                else
                {
                    var retryableFailure = failures.FirstOrDefault(x => x.Retryable);
                    var failure = retryableFailure ?? failures[0];
                    if (retryableFailure is not null)
                    {
                        var retryDelay = TimeSpan.FromSeconds(Math.Min(1800, 30 * Math.Pow(2, Math.Max(0, message.AttemptCount - 1))));
                        message.MarkFailed(failure.ErrorCode ?? "delivery_failed", failure.ErrorMessage ?? "Delivery failed.", retryDelay);
                        if (message.Status == NotificationStatus.DeadLetter) await pendingQueue.RemoveAsync(message.Id, cancellationToken);
                        else await pendingQueue.EnqueueAsync(message.Id, message.NextAttemptAtUtc ?? DateTime.UtcNow, cancellationToken);
                    }
                    else
                    {
                        message.MarkTerminalFailed(failure.ErrorCode ?? "delivery_failed", failure.ErrorMessage ?? "Delivery failed.");
                        await pendingQueue.RemoveAsync(message.Id, cancellationToken);
                    }
                }
                await unitOfWork.SaveChangesAsync(cancellationToken);
                processed++;
            }
            catch (Exception ex)
            {
                var retryDelay = TimeSpan.FromSeconds(Math.Min(1800, 30 * Math.Pow(2, Math.Max(0, message.AttemptCount - 1))));
                message.MarkFailed("processing_failed", ex.Message, retryDelay);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                if (message.Status == NotificationStatus.DeadLetter) await pendingQueue.RemoveAsync(message.Id, cancellationToken);
                else await pendingQueue.EnqueueAsync(message.Id, message.NextAttemptAtUtc ?? DateTime.UtcNow, cancellationToken);
            }
        }
        return processed;
    }

    private async Task<RenderedNotification> RenderAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.TemplateCode))
            return renderer.Render(message.Subject, message.Body ?? string.Empty, message.PayloadJson);

        var template = await templateCache.GetAsync(message.TemplateCode, cancellationToken);
        if (template is null)
        {
            var entity = await repository.GetTemplateByCodeAsync(message.TemplateCode, cancellationToken);
            if (entity is null || !entity.IsActive) throw new InvalidOperationException($"Template '{message.TemplateCode}' was not found or is inactive.");
            template = entity.ToDto();
            await templateCache.SetAsync(template, cancellationToken);
        }
        return renderer.Render(template.SubjectTemplate, template.BodyTemplate, message.PayloadJson);
    }

    private static NotificationChannel ParseChannel(string value)
        => Enum.TryParse<NotificationChannel>(value, true, out var channel)
            ? channel
            : throw new ArgumentException($"Unknown notification channel '{value}'.", nameof(value));

    private static (int PageNumber, int PageSize) NormalizePage(int pageNumber, int pageSize)
        => (Math.Max(1, pageNumber), Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 200));
}
