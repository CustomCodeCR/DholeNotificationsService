using System.Net;
using System.Net.Mail;
using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Domain.Notifications.Entities;
using Dhole.Notifications.Domain.Notifications.Enums;
using Microsoft.Extensions.Options;

namespace Dhole.Notifications.Infrastructure.Delivery;

public sealed class EmailNotificationDeliveryChannel(IOptions<EmailOptions> options) : INotificationDeliveryChannel
{
    private readonly EmailOptions _options = options.Value;
    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task<NotificationDeliveryResult> SendAsync(NotificationMessage message, NotificationRecipient recipient, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return NotificationDeliveryResult.Failure("SMTP", "email_not_configured", "Email delivery is disabled.", retryable: false);
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.FromAddress))
            return NotificationDeliveryResult.Failure("SMTP", "email_configuration_invalid", "SMTP host and FromAddress are required.", retryable: false);
        if (string.IsNullOrWhiteSpace(recipient.Address) || !recipient.Address.Contains('@'))
            return NotificationDeliveryResult.Failure("SMTP", "invalid_recipient", "Recipient email address is invalid.", retryable: false);

        using var mail = new MailMessage();
        mail.From = new MailAddress(_options.FromAddress, _options.FromName);
        mail.To.Add(new MailAddress(recipient.Address, recipient.DisplayName));
        mail.Subject = message.Subject ?? string.Empty;
        mail.Body = message.Body ?? string.Empty;
        mail.IsBodyHtml = true;

        using var smtp = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = string.IsNullOrWhiteSpace(_options.UserName),
        };
        if (!string.IsNullOrWhiteSpace(_options.UserName))
            smtp.Credentials = new NetworkCredential(_options.UserName, _options.Password);

        cancellationToken.ThrowIfCancellationRequested();
        await smtp.SendMailAsync(mail);
        cancellationToken.ThrowIfCancellationRequested();
        return NotificationDeliveryResult.Success("SMTP", Guid.NewGuid().ToString("N"));
    }
}
