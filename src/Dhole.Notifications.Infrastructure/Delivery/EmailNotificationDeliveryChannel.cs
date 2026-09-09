using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Domain.Notifications.Entities;
using Dhole.Notifications.Domain.Notifications.Enums;
using Microsoft.Extensions.Options;

namespace Dhole.Notifications.Infrastructure.Delivery;

public sealed class EmailNotificationDeliveryChannel(IOptions<EmailOptions> options) : INotificationDeliveryChannel
{
    private const int MaxAttachmentCount = 10;
    private const int MaxAttachmentBytes = 20 * 1024 * 1024;

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

        try
        {
            AddAttachments(mail, message.PayloadJson);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidDataException)
        {
            return NotificationDeliveryResult.Failure(
                "SMTP",
                "email_attachment_invalid",
                ex.Message,
                retryable: false);
        }

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

    private static void AddAttachments(MailMessage mail, string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson) || payloadJson.Trim() == "{}")
            return;

        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;

        if (TryGet(root, "attachments", out var attachments) && attachments.ValueKind == JsonValueKind.Array)
        {
            var count = 0;
            foreach (var attachment in attachments.EnumerateArray())
            {
                count++;
                if (count > MaxAttachmentCount)
                    throw new InvalidDataException($"Email cannot contain more than {MaxAttachmentCount} attachments.");

                AddAttachment(mail, attachment);
            }

            return;
        }

        if (TryGet(root, "attachment", out var singleAttachment) && singleAttachment.ValueKind == JsonValueKind.Object)
            AddAttachment(mail, singleAttachment);
    }

    private static void AddAttachment(MailMessage mail, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Email attachment payload must be an object.");

        var fileName = GetString(element, "fileName");
        var contentBase64 = GetString(element, "contentBase64") ?? GetString(element, "base64Content");
        var contentType = GetString(element, "contentType") ?? "application/octet-stream";

        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contentBase64))
            throw new InvalidDataException("Email attachment requires fileName and contentBase64.");

        var content = Convert.FromBase64String(contentBase64);
        if (content.Length == 0)
            throw new InvalidDataException("Email attachment cannot be empty.");
        if (content.Length > MaxAttachmentBytes)
            throw new InvalidDataException($"Email attachment exceeds the {MaxAttachmentBytes / 1024 / 1024} MB limit.");

        var safeFileName = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = "attachment";

        var stream = new MemoryStream(content, writable: false);
        try
        {
            mail.Attachments.Add(new Attachment(stream, safeFileName, contentType));
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    continue;

                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string name)
        => TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;
}
