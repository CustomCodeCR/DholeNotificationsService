using System.Threading.Channels;
using Dhole.Notifications.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Dhole.Notifications.Api.Hubs;

/// <summary>
/// Security notifications go only to the user group established from a validated
/// JWT during the original hub connection, never Clients.All or arbitrary groups.
/// This path is deliberately independent of resource-owner ACL: revoked clients
/// cannot pass that ACL anymore, but still need the final disconnect message.
/// </summary>
public sealed class AuthSecurityRealtimeRelay(
    NotificationsResourceRealtimeBus bus,
    IHubContext<NotificationsHub> hub,
    ILogger<AuthSecurityRealtimeRelay> logger) : BackgroundService
{
    private readonly Channel<AuthSecurityRealtimePush> _queue =
        Channel.CreateBounded<AuthSecurityRealtimePush>(
            new BoundedChannelOptions(2048)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await bus.SubscribeSecurityAsync(item =>
        {
            if (!_queue.Writer.TryWrite(item))
                logger.LogWarning("Auth security realtime backlog saturated; JWT and REST validation remain authoritative.");
        });
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                if (item.UserId == Guid.Empty || item.EventId == Guid.Empty ||
                    item.Action is not ("forceLogout" or "refreshPermissions"))
                    continue;
                await hub.Clients.Group(NotificationAudiencePolicy.UserGroup(item.UserId))
                    .SendAsync("authSecurityChanged", new
                    {
                        eventId = item.EventId,
                        userId = item.UserId,
                        targetSessionId = item.TargetSessionId,
                        action = item.Action,
                        occurredAtUtc = item.OccurredAtUtc
                    }, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            _queue.Writer.TryComplete();
            await bus.UnsubscribeSecurityAsync();
        }
    }
}
