using System.Security.Claims;

namespace Dhole.Notifications.Infrastructure.Realtime;

/// <summary>
/// F6.03: maintain the existing NotificationsHub without ever allowing
/// arbitrary strings from a JWT to become a recipient SignalR group.
/// The source of truth remains the authorized persisted Notifications inbox.
/// </summary>
public static class NotificationAudiencePolicy
{
    public static bool TryUserGroup(ClaimsPrincipal? principal, out string group)
    {
        group = string.Empty;
        if (principal?.Identity?.IsAuthenticated != true)
            return false;
        var raw = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst("user_id")?.Value
            ?? principal.FindFirst("userId")?.Value
            ?? principal.FindFirst("nameidentifier")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(raw, out var id) || id == Guid.Empty)
            return false;
        group = UserGroup(id);
        return true;
    }

    public static bool TryPushGroup(SystemNotificationPush? notification, out string group)
    {
        group = string.Empty;
        if (notification is null || notification.NotificationId == Guid.Empty ||
            notification.RecipientId == Guid.Empty || notification.UserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(notification.NotificationType))
            return false;
        group = UserGroup(notification.UserId);
        return true;
    }

    public static string UserGroup(Guid userId) => $"user:{userId:D}";
}
