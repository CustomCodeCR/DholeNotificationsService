using CustomCodeFramework.Redis.Abstractions;
using CustomCodeFramework.Redis.Caching;
using Dhole.Notifications.Application.Abstractions;

namespace Dhole.Notifications.Infrastructure.Queue;

public sealed class NotificationPendingQueue(ICacheService cache) : INotificationPendingQueue
{
    private static string Key(Guid id) => $"notifications:pending:{id:N}";

    public Task EnqueueAsync(Guid notificationId, DateTime availableAtUtc, CancellationToken cancellationToken = default)
        => cache.SetAsync(Key(notificationId), availableAtUtc, CacheEntryOptions.Default(TimeSpan.FromDays(14)), cancellationToken);

    public Task RemoveAsync(Guid notificationId, CancellationToken cancellationToken = default)
        => cache.RemoveAsync(Key(notificationId), cancellationToken);
}
