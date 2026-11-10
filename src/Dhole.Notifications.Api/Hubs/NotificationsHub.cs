using Dhole.Notifications.Infrastructure.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Dhole.Notifications.Api.Hubs;

[Authorize]
public sealed class NotificationsHub(
    ILogger<NotificationsHub> logger,
    RealtimeConnectionRegistry registry,
    OwnerRestRealtimeAuthorizer authorizer,
    NotificationsRealtimeRuntime runtime) : Hub
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

        // Universal subscriptions are opt-in; legacy notification user groups remain.
        if (runtime.Enabled)
        {
            var bearer = Context.GetHttpContext()?.Items["Dhole.Notifications.UniversalBearer"] as string;
            if (string.IsNullOrWhiteSpace(bearer))
            {
                Context.Abort();
                return;
            }
            registry.Connect(Context.ConnectionId, Context.User!, bearer);
        }

        await base.OnConnectedAsync();
    }

    // Module topics are inert until each event passes its original resource-owner ACL.
    public async Task SubscribeResource(string resourceKind, string resourceId)
    {
        if (!runtime.Enabled ||
            !ResourceRealtimePolicy.TryCreate(resourceKind, resourceId, out var resource) ||
            !registry.TryGet(Context.ConnectionId, out var user, out var bearer) ||
            (ResourceRealtimePolicy.IsModuleResource(resource)
                ? user?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(bearer)
                : !await authorizer.CanReadAsync(resource, user, bearer, Context.ConnectionAborted)))
            throw new HubException("Realtime subscription denied.");

        if (!registry.Subscribe(Context.ConnectionId, resource, runtime.MaxSubscriptions))
            throw new HubException("Realtime subscription limit exceeded.");
        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, resource.Group, Context.ConnectionAborted);
        }
        catch
        {
            registry.Unsubscribe(Context.ConnectionId, resource);
            throw;
        }
    }

    public async Task UnsubscribeResource(string resourceKind, string resourceId)
    {
        if (!runtime.Enabled ||
            !ResourceRealtimePolicy.TryCreate(resourceKind, resourceId, out var resource))
            throw new HubException("Realtime subscription denied.");

        registry.Unsubscribe(Context.ConnectionId, resource);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, resource.Group, Context.ConnectionAborted);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        registry.Disconnect(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
