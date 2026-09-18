using System.Text.Json;
using CustomCodeFramework.Redis.Streams.Abstractions;
using CustomCodeFramework.Redis.Streams.Messages;
using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Contracts.Notifications;

namespace Dhole.Notifications.Workers.Streams;

internal sealed class NotificationRequestedStreamHandler(
    INotificationApplicationService notifications,
    ILogger<NotificationRequestedStreamHandler> logger) : IRedisStreamMessageHandler
{
    public string MessageType => "notifications.notification.requested";

    public async Task HandleAsync(RedisStreamEnvelope envelope, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(envelope.PayloadJson))
                throw new InvalidOperationException("Notification stream payload is empty.");
            using var document = JsonDocument.Parse(envelope.PayloadJson);
            var root = document.RootElement;
            var recipients = new List<NotificationRecipientRequest>();
            if (TryProperty(root, "recipients", out var recipientsElement) && recipientsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var recipient in recipientsElement.EnumerateArray())
                {
                    var userIdText = GetString(recipient, "userId");
                    recipients.Add(new NotificationRecipientRequest(
                        Guid.TryParse(userIdText, out var userId) ? userId : null,
                        GetString(recipient, "address") ?? string.Empty,
                        GetString(recipient, "displayName")));
                }
            }
            if (recipients.Count == 0)
            {
                var userIdText = GetString(root, "userId");
                var address = GetString(root, "recipient") ?? GetString(root, "address") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(address) || Guid.TryParse(userIdText, out _))
                    recipients.Add(new NotificationRecipientRequest(Guid.TryParse(userIdText, out var userId) ? userId : null, address, GetString(root, "displayName")));
            }

            DateTime? scheduled = null;
            if (DateTime.TryParse(GetString(root, "scheduledForUtc"), out var scheduledValue)) scheduled = scheduledValue.ToUniversalTime();
            var payloadJson = BuildPayloadJson(root);

            await notifications.CreateMessageAsync(new CreateNotificationMessageRequest(
                GetString(root, "notificationType") ?? "generic",
                GetString(root, "templateCode"),
                GetString(root, "channel") ?? "System",
                GetString(root, "entityType"),
                GetString(root, "entityId"),
                GetString(root, "subject"),
                GetString(root, "body"),
                payloadJson,
                scheduled,
                GetInt(root, "maxAttempts") ?? 3,
                recipients), null, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not create notification from Redis stream message {MessageId}.", envelope.MessageId);
            throw;
        }
    }

    private static string BuildPayloadJson(JsonElement root)
    {
        var payloadJson = TryProperty(root, "payload", out var payload)
            ? payload.GetRawText()
            : (GetString(root, "payloadJson") ?? "{}");

        var hasAttachments =
            TryProperty(root, "attachments", out var attachments)
            && attachments.ValueKind == JsonValueKind.Array;
        var hasAttachment =
            TryProperty(root, "attachment", out var attachment)
            && attachment.ValueKind == JsonValueKind.Object;

        // Los productores actuales colocan los adjuntos dentro de payload.
        // También aceptamos attachment/attachments en la raíz del evento para no
        // descartarlos al convertir el evento de Redis en NotificationMessage.
        if (!hasAttachments && !hasAttachment)
            return payloadJson;

        using var payloadDocument = JsonDocument.Parse(payloadJson);
        if (payloadDocument.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "Notification payload must be a JSON object when attachments are provided."
            );
        }

        var merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in payloadDocument.RootElement.EnumerateObject())
            merged[property.Name] = property.Value.Clone();

        if (hasAttachments)
            merged["attachments"] = attachments.Clone();
        if (hasAttachment)
            merged["attachment"] = attachment.Clone();

        return JsonSerializer.Serialize(merged);
    }

    private static bool TryProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        return element.TryGetProperty(pascal, out value);
    }
    private static string? GetString(JsonElement element, string name)
        => TryProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int? GetInt(JsonElement element, string name)
        => TryProperty(element, name, out var value) && value.TryGetInt32(out var number) ? number : null;
}
