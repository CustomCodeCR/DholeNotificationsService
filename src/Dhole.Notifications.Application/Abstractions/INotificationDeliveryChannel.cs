using Dhole.Notifications.Domain.Notifications.Entities;
using Dhole.Notifications.Domain.Notifications.Enums;

namespace Dhole.Notifications.Application.Abstractions;

public interface INotificationDeliveryChannel
{
    NotificationChannel Channel { get; }
    Task<NotificationDeliveryResult> SendAsync(NotificationMessage message, NotificationRecipient recipient, CancellationToken cancellationToken = default);
}

public sealed record NotificationDeliveryResult(
    bool Succeeded,
    string Provider,
    string? ProviderMessageId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    bool Retryable = true)
{
    public static NotificationDeliveryResult Success(string provider, string? providerMessageId = null)
        => new(true, provider, providerMessageId);

    public static NotificationDeliveryResult Failure(string provider, string errorCode, string errorMessage, bool retryable = true)
        => new(false, provider, null, errorCode, errorMessage, retryable);
}
