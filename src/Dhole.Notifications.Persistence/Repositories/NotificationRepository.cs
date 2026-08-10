using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Domain.Notifications.Entities;
using Dhole.Notifications.Domain.Notifications.Enums;
using Dhole.Notifications.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Dhole.Notifications.Persistence.Repositories;

public sealed class NotificationRepository(ServiceDbContext dbContext) : INotificationRepository
{
    public Task<bool> TemplateCodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToLowerInvariant();
        return dbContext.NotificationTemplates.AnyAsync(x => x.Code == normalized && (!excludeId.HasValue || x.Id != excludeId.Value), cancellationToken);
    }

    public Task<NotificationTemplate?> GetTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.NotificationTemplates.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

    public Task<NotificationTemplate?> GetTemplateByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToLowerInvariant();
        return dbContext.NotificationTemplates.FirstOrDefaultAsync(x => x.Code == normalized && !x.IsDeleted, cancellationToken);
    }

    public Task AddTemplateAsync(NotificationTemplate template, CancellationToken cancellationToken = default)
        => dbContext.NotificationTemplates.AddAsync(template, cancellationToken).AsTask();

    public async Task<(IReadOnlyCollection<NotificationTemplate> Items, int Total)> BrowseTemplatesAsync(int pageNumber, int pageSize, string? search, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = dbContext.NotificationTemplates.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLower();
            query = query.Where(x => x.Code.ToLower().Contains(value) || x.Name.ToLower().Contains(value) || x.NotificationType.ToLower().Contains(value));
        }
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task AddMessageAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        => dbContext.NotificationMessages.AddAsync(message, cancellationToken).AsTask();

    public Task<NotificationMessage?> GetMessageByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => BaseMessageQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<(IReadOnlyCollection<NotificationMessage> Items, int Total)> BrowseMessagesAsync(int pageNumber, int pageSize, string? search, NotificationStatus? status, string? channel, CancellationToken cancellationToken = default)
    {
        var query = dbContext.NotificationMessages.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLower();
            query = query.Where(x => x.NotificationType.ToLower().Contains(value)
                || (x.TemplateCode != null && x.TemplateCode.ToLower().Contains(value))
                || (x.EntityType != null && x.EntityType.ToLower().Contains(value))
                || (x.EntityId != null && x.EntityId.ToLower().Contains(value))
                || (x.Subject != null && x.Subject.ToLower().Contains(value)));
        }
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(channel) && Enum.TryParse<NotificationChannel>(channel, true, out var parsedChannel))
            query = query.Where(x => x.Channel == parsedChannel);
        var total = await query.CountAsync(cancellationToken);
        var ids = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(x => x.Id).ToListAsync(cancellationToken);
        var items = await BaseMessageQuery().AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        items = items.OrderBy(x => ids.IndexOf(x.Id)).ToList();
        return (items, total);
    }

    public async Task<(IReadOnlyCollection<NotificationMessage> Items, int Total)> BrowseByEntityAsync(string entityType, string entityId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.NotificationMessages.AsNoTracking().Where(x => x.EntityType == entityType && x.EntityId == entityId);
        return await PageMessageQuery(query, pageNumber, pageSize, cancellationToken);
    }

    public async Task<(IReadOnlyCollection<NotificationMessage> Items, int Total)> BrowseByRecipientAsync(string recipient, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var value = recipient.Trim().ToLowerInvariant();
        Guid? userId = Guid.TryParse(recipient, out var parsed) ? parsed : null;
        var query = dbContext.NotificationMessages.AsNoTracking().Where(x =>
            x.Recipients.Any(r => r.Address.ToLower() == value || (userId.HasValue && r.UserId == userId.Value)));
        return await PageMessageQuery(query, pageNumber, pageSize, cancellationToken);
    }

    public async Task<IReadOnlyCollection<NotificationMessage>> GetDueMessagesAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var ids = await dbContext.NotificationMessages
            .Where(x =>
                (x.Status == NotificationStatus.Pending)
                || (x.Status == NotificationStatus.Scheduled && (!x.ScheduledForUtc.HasValue || x.ScheduledForUtc <= utcNow))
                || (x.Status == NotificationStatus.Retrying && (!x.NextAttemptAtUtc.HasValue || x.NextAttemptAtUtc <= utcNow))
                || (x.Status == NotificationStatus.Processing && x.ProcessingStartedAtUtc.HasValue && x.ProcessingStartedAtUtc <= utcNow.AddMinutes(-5)))
            .OrderBy(x => x.ScheduledForUtc ?? x.NextAttemptAtUtc ?? x.CreatedAtUtc)
            .Take(batchSize).Select(x => x.Id).ToListAsync(cancellationToken);
        return await BaseMessageQuery().Where(x => ids.Contains(x.Id)).OrderBy(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    private IQueryable<NotificationMessage> BaseMessageQuery()
        => dbContext.NotificationMessages.Include(x => x.Recipients).Include(x => x.DeliveryAttempts);

    private async Task<(IReadOnlyCollection<NotificationMessage> Items, int Total)> PageMessageQuery(IQueryable<NotificationMessage> query, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var ids = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(x => x.Id).ToListAsync(cancellationToken);
        var items = await BaseMessageQuery().AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        items = items.OrderBy(x => ids.IndexOf(x.Id)).ToList();
        return (items, total);
    }
}
