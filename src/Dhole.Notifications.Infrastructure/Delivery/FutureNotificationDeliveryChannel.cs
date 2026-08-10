using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Domain.Notifications.Entities;
using Dhole.Notifications.Domain.Notifications.Enums;

namespace Dhole.Notifications.Infrastructure.Delivery;

public sealed class FutureNotificationDeliveryChannel(NotificationChannel channel) : INotificationDeliveryChannel
{
    public NotificationChannel Channel { get; } = channel;

    public Task<NotificationDeliveryResult> SendAsync(NotificationMessage message, NotificationRecipient recipient, CancellationToken cancellationToken = default)
        => Task.FromResult(NotificationDeliveryResult.Failure(Channel.ToString(), "channel_future_not_configured", $"Channel {Channel} is reserved for a future provider and is not configured.", retryable: false));
}
