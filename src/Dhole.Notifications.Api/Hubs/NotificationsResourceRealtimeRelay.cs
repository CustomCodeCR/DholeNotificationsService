using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;

namespace Dhole.Notifications.Api.Hubs;

// Existing notificationReceived relay is unchanged. This is an additional
// consumer using the same NotificationsHub and only authorized resource IDs.
public sealed class NotificationsResourceRealtimeRelay(
    NotificationsRealtimeRuntime runtime,
    NotificationsResourceRealtimeBus bus,
    RealtimeConnectionRegistry connections,
    OwnerRestRealtimeAuthorizer authorizer,
    IHubContext<NotificationsHub> hub,
    ILogger<NotificationsResourceRealtimeRelay> logger) : BackgroundService
{
    private readonly Channel<ResourceRealtimePush> _messages =
        Channel.CreateBounded<ResourceRealtimePush>(new BoundedChannelOptions(4096)
        {
            SingleReader = true, SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!runtime.Enabled) return;
        await bus.SubscribeAsync(item =>
        {
            if (!_messages.Writer.TryWrite(item))
                logger.LogWarning("Resource realtime relay is saturated; clients reconcile via REST.");
        });
        try
        {
            await foreach (var item in _messages.Reader.ReadAllAsync(stoppingToken))
                await DeliverAsync(item, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await bus.UnsubscribeAsync();
            _messages.Writer.TryComplete();
        }
    }

    private async Task DeliverAsync(ResourceRealtimePush item, CancellationToken ct)
    {
        if (!ResourceRealtimePolicy.TryCreate(item.Kind,
            item.Invalidation.AggregateId.ToString("D"), out var resource)) return;

        foreach (var (connectionId, user, bearer) in connections.Subscribers(resource))
        {
            if (!await authorizer.CanReadAsync(resource, user, bearer, ct))
            {
                connections.Unsubscribe(connectionId, resource);
                await hub.Groups.RemoveFromGroupAsync(connectionId, resource.Group, ct);
                await hub.Clients.Client(connectionId).SendCoreAsync("resourceRevoked",
                    new object?[] { resource.Kind, resource.Id.ToString("D") }, ct);
                continue;
            }

            // Never Clients.All or direct fanout to an unverified client group.
            await hub.Clients.Client(connectionId).SendCoreAsync("resourceChanged",
                new object?[] { item.Invalidation }, ct);
        }
    }
}
