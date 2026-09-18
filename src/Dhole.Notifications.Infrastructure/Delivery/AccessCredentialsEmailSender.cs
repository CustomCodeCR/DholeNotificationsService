using System.Net;
using System.Net.Mail;
using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Contracts.Notifications;
using Microsoft.Extensions.Options;

namespace Dhole.Notifications.Infrastructure.Delivery;

public sealed class AccessCredentialsEmailSender(
    IOptions<EmailOptions> emailOptions,
    IOptions<AccessCredentialsEmailOptions> credentialsOptions)
    : IAccessCredentialsEmailSender
{
    private readonly EmailOptions _emailOptions = emailOptions.Value;
    private readonly AccessCredentialsEmailOptions _credentialsOptions = credentialsOptions.Value;

    public async Task SendAsync(
        SendAccessCredentialsEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_emailOptions.Enabled)
            throw new InvalidOperationException("Email delivery is disabled.");

        if (string.IsNullOrWhiteSpace(_emailOptions.Host) || string.IsNullOrWhiteSpace(_emailOptions.FromAddress))
            throw new InvalidOperationException("SMTP host and FromAddress are required.");

        if (request.UserId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.UserName))
            throw new ArgumentException("UserName is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.TemporaryPassword))
            throw new ArgumentException("TemporaryPassword is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(_credentialsOptions.AccessUrl))
            throw new InvalidOperationException("Access credentials URL is not configured.");

        if (string.IsNullOrWhiteSpace(_credentialsOptions.SupportEmail))
            throw new InvalidOperationException("Access credentials support email is not configured.");

        MailAddress recipient;
        try
        {
            recipient = new MailAddress(request.Email, request.DisplayName);
        }
        catch (FormatException)
        {
            throw new ArgumentException("Recipient email address is invalid.", nameof(request));
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(_emailOptions.FromAddress, _emailOptions.FromName),
            Subject = _credentialsOptions.Subject,
            Body = BuildBody(request),
            IsBodyHtml = true,
        };
        mail.To.Add(recipient);

        using var smtp = new SmtpClient(_emailOptions.Host, _emailOptions.Port)
        {
            EnableSsl = _emailOptions.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = string.IsNullOrWhiteSpace(_emailOptions.UserName),
        };

        if (!string.IsNullOrWhiteSpace(_emailOptions.UserName))
            smtp.Credentials = new NetworkCredential(_emailOptions.UserName, _emailOptions.Password);

        cancellationToken.ThrowIfCancellationRequested();
        await smtp.SendMailAsync(mail);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private string BuildBody(SendAccessCredentialsEmailRequest request)
    {
        var userName = WebUtility.HtmlEncode(request.UserName.Trim());
        var temporaryPassword = WebUtility.HtmlEncode(request.TemporaryPassword);
        var accessUrl = WebUtility.HtmlEncode(_credentialsOptions.AccessUrl.Trim());
        var supportEmail = WebUtility.HtmlEncode(_credentialsOptions.SupportEmail.Trim());

        return $"""
<p>Buenos días, espero que se encuentre muy bien.</p>

<p>Le comparto sus credenciales de acceso y el enlace para ingresar al sistema:</p>

<p>
<strong>Usuario:</strong> {userName}<br />
<strong>Contraseña:</strong> {temporaryPassword}<br />
<strong>Link de acceso:</strong> <a href="{accessUrl}">{accessUrl}</a>
</p>

<p>Por motivos de seguridad, estas credenciales son <strong>personales e intransferibles</strong>. Le solicitamos no compartir su usuario, contraseña ni acceso al sistema con ninguna otra persona.</p>

<p>En caso de presentar algún inconveniente para ingresar, por favor <strong>comuníquese conmigo para brindarle asistencia</strong> al correo <a href="mailto:{supportEmail}">{supportEmail}</a>.</p>

<p>Saludos cordiales.</p>
""";
    }
}
