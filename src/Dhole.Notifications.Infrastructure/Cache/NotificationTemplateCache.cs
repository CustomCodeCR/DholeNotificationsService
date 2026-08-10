using CustomCodeFramework.Redis.Abstractions;
using CustomCodeFramework.Redis.Caching;
using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Contracts.Notifications;

namespace Dhole.Notifications.Infrastructure.Cache;

public sealed class NotificationTemplateCache(ICacheService cache) : INotificationTemplateCache
{
    private static string Key(string code) => $"notifications:templates:{code.Trim().ToLowerInvariant()}";

    public Task<NotificationTemplateDto?> GetAsync(string code, CancellationToken cancellationToken = default)
        => cache.GetAsync<NotificationTemplateDto>(Key(code), cancellationToken);

    public Task SetAsync(NotificationTemplateDto template, CancellationToken cancellationToken = default)
        => cache.SetAsync(Key(template.Code), template, CacheEntryOptions.Default(TimeSpan.FromMinutes(30)), cancellationToken);

    public Task RemoveAsync(string code, CancellationToken cancellationToken = default)
        => cache.RemoveAsync(Key(code), cancellationToken);
}
