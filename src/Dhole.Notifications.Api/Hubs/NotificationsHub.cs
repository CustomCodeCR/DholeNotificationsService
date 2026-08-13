using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Dhole.Notifications.Api.Hubs;

[Authorize]
public sealed class NotificationsHub(ILogger<NotificationsHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = ResolveUserId(Context.User);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            logger.LogDebug(
                "SignalR connection {ConnectionId} joined notification group user:{UserId}.",
                Context.ConnectionId,
                userId
            );
        }
        else
        {
            logger.LogWarning(
                "Authenticated SignalR connection {ConnectionId} does not contain a supported user id claim.",
                Context.ConnectionId
            );
        }

        await base.OnConnectedAsync();
    }

    private static string? ResolveUserId(ClaimsPrincipal? user)
    {
        if (user is null) return null;

        var raw = user.FindFirst("sub")?.Value
            ?? user.FindFirst("user_id")?.Value
            ?? user.FindFirst("userId")?.Value
            ?? user.FindFirst("nameidentifier")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Pricing/Notifications recipients use Guid user ids. Normalizing the claim
        // guarantees that the group name matches the UserId serialized by the worker.
        return Guid.TryParse(raw, out var parsed) ? parsed.ToString() : raw.Trim();
    }
}
