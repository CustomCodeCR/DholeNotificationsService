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
            return NotificationDeliveryResult.Failure(
                "Dhole.SignalR",
                "SystemRealtimeRelayUnavailable",
                "El relay SignalR del API de Notifications no está disponible.",
                retryable: true
            );
        }

        return NotificationDeliveryResult.Success(
            "Dhole.SignalR",
            $"signalr:{message.Id:N}:{recipient.Id:N}"
        );
    }
}
