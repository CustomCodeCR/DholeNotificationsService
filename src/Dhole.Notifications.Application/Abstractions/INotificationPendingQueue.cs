namespace Dhole.Notifications.Application.Abstractions;

public interface INotificationPendingQueue
{
    Task EnqueueAsync(Guid notificationId, DateTime availableAtUtc, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid notificationId, CancellationToken cancellationToken = default);
}
