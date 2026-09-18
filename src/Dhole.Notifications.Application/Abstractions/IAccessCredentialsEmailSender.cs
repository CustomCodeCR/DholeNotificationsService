using Dhole.Notifications.Contracts.Notifications;

namespace Dhole.Notifications.Application.Abstractions;

public interface IAccessCredentialsEmailSender
{
    Task SendAsync(
        SendAccessCredentialsEmailRequest request,
        CancellationToken cancellationToken = default);
}
