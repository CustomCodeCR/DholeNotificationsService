using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Domain.Notifications.Entities;
using Dhole.Notifications.Domain.Notifications.Enums;
using Dhole.Notifications.Infrastructure.Realtime;

namespace Dhole.Notifications.Infrastructure.Delivery;

public sealed class SystemNotificationDeliveryChannel(SystemNotificationRealtimeBus realtimeBus)
    : INotificationDeliveryChannel
{
    public NotificationChannel Channel => NotificationChannel.System;

    public async Task<NotificationDeliveryResult> SendAsync(
        NotificationMessage message,
        NotificationRecipient recipient,
        CancellationToken cancellationToken = default
    )
    {
        if (!recipient.UserId.HasValue)
        {
            return NotificationDeliveryResult.Failure(
                "Dhole.SignalR",
                "SystemRecipientUserRequired",
                "Las notificaciones System requieren un UserId.",
                retryable: false
            );
        }

        var subscribers = await realtimeBus.PublishAsync(
            new SystemNotificationPush(
                message.Id,
                recipient.Id,
                recipient.UserId.Value,
                message.NotificationType,
                message.Subject,
                message.Body,
                message.PayloadJson,
                message.EntityType,
                message.EntityId,
                DateTime.UtcNow
            )
        );

        if (subscribers <= 0)
        {
            // The System recipient already lives in the persistent inbox; Redis
            // pub/sub has no active API subscribers right now. This is not a
            // delivery failure and must not exhaust retry attempts offline.
            return NotificationDeliveryResult.Success(
                "Dhole.Inbox",
                $"inbox:{message.Id:N}:{recipient.Id:N}"
            );
        }

        return NotificationDeliveryResult.Success(
            "Dhole.SignalR",
            $"signalr:{message.Id:N}:{recipient.Id:N}"
        );
    }
}
