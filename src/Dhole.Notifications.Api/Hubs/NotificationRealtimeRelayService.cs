using Dhole.Notifications.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Channels;

namespace Dhole.Notifications.Api.Hubs;

public sealed class NotificationRealtimeRelayService(
    SystemNotificationRealtimeBus bus,
    IHubContext<NotificationsHub> hubContext,
    ILogger<NotificationRealtimeRelayService> logger
) : BackgroundService
{
    // F6.03: no fire-and-forget fanout. Bound in-memory work while the durable
    // Notifications inbox remains authoritative during load or disconnection.
    private readonly Channel<SystemNotificationPush> _queue =
        Channel.CreateBounded<SystemNotificationPush>(new BoundedChannelOptions(2048)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await bus.SubscribeAsync(notification =>
        {
            if (!NotificationAudiencePolicy.TryPushGroup(notification, out _))
                return;
            if (!_queue.Writer.TryWrite(notification))
                logger.LogWarning(
                    "Notification SignalR relay backlog saturated; persistent inbox must reconcile.");
        });

        try
        {
            await foreach (var notification in _queue.Reader.ReadAllAsync(stoppingToken))
                await RelayAsync(notification, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The REST inbox is authoritative for any queued entries.
        }
        finally
        {
            await bus.UnsubscribeAsync();
            _queue.Writer.TryComplete();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await bus.UnsubscribeAsync();
        _queue.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }

    private async Task RelayAsync(SystemNotificationPush notification, CancellationToken cancellationToken)
    {
        if (!NotificationAudiencePolicy.TryPushGroup(notification, out var group))
            return;
        try
        {
            await hubContext.Clients.Group(group)
                .SendAsync("notificationReceived", notification, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Notification relay failed for {NotificationId}; REST inbox remains authoritative.",
                notification.NotificationId);
        }
    }
}
