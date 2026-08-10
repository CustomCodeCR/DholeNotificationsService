using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Domain.Notifications.Entities;
using Dhole.Notifications.Domain.Notifications.Enums;

namespace Dhole.Notifications.Infrastructure.Delivery;

public sealed class SystemNotificationDeliveryChannel : INotificationDeliveryChannel
{
    public NotificationChannel Channel => NotificationChannel.System;

    public Task<NotificationDeliveryResult> SendAsync(NotificationMessage message, NotificationRecipient recipient, CancellationToken cancellationToken = default)
        => Task.FromResult(NotificationDeliveryResult.Success("Dhole.System", $"system:{message.Id:N}:{recipient.Id:N}"));
}
