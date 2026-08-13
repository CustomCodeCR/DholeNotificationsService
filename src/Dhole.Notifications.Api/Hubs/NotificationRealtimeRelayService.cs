using Dhole.Notifications.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Dhole.Notifications.Api.Hubs;

public sealed class NotificationRealtimeRelayService(
    SystemNotificationRealtimeBus bus,
    IHubContext<NotificationsHub> hubContext,
    ILogger<NotificationRealtimeRelayService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await bus.SubscribeAsync(notification =>
        {
            _ = RelayAsync(notification, stoppingToken);
        });

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await bus.UnsubscribeAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task RelayAsync(SystemNotificationPush notification, CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients
                .Group($"user:{notification.UserId}")
                .SendAsync("notificationReceived", notification, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not relay notification {NotificationId} to SignalR.", notification.NotificationId);
        }
    }
}
