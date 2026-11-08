using Dhole.Notifications.Infrastructure.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Dhole.Notifications.Api.Hubs;

[Authorize]
public sealed class NotificationsHub(ILogger<NotificationsHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (NotificationAudiencePolicy.TryUserGroup(Context.User, out var group))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
            logger.LogDebug("Authenticated notification connection {ConnectionId} joined its own group.",
                Context.ConnectionId);
        }
        else
        {
            logger.LogWarning("Notification connection {ConnectionId} lacks a valid authenticated Guid user.",
                Context.ConnectionId);
            Context.Abort(); // Fail closed: never join arbitrary caller-chosen groups.
            return;
        }

        await base.OnConnectedAsync();
    }

}
