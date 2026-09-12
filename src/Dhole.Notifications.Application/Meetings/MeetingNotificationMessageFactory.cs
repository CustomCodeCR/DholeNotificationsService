using System.Net;
using System.Text.Json;
using Dhole.Notifications.Contracts.Notifications;

namespace Dhole.Notifications.Application.Meetings;

public static class MeetingNotificationMessageFactory
{
    public static CreateNotificationMessageRequest CreateRequested(
        string payloadJson,
        IEnumerable<string> marketingRecipients)
    {
        using var document = Parse(payloadJson);
        var root = document.RootElement;
        var meetingRequestId = Required(root, "meetingRequestId");
        var subject = Required(root, "subject");
        var meetingTypeName = Required(root, "meetingTypeName");
        var start = Required(root, "requestedStartUtc");
        var end = Required(root, "requestedEndUtc");
        var timeZone = Required(root, "timeZone");
        var siteKey = Required(root, "siteKey");
        var mode = Required(root, "meetingMode");

        var recipients = marketingRecipients
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value) && value.Contains('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => new NotificationRecipientRequest(null, value!, "Mercadeo"))
            .ToArray();
        if (recipients.Length == 0)
            throw new InvalidOperationException("At least one Marketing email recipient must be configured.");

        var body = $"""
<p>Se recibió una nueva solicitud de reunión.</p>
<ul>
<li><strong>Asunto:</strong> {Encode(subject)}</li>
<li><strong>Tipo:</strong> {Encode(meetingTypeName)}</li>
<li><strong>Modalidad:</strong> {Encode(mode)}</li>
<li><strong>Inicio solicitado:</strong> {Encode(start)}</li>
<li><strong>Fin solicitado:</strong> {Encode(end)}</li>
<li><strong>Zona horaria:</strong> {Encode(timeZone)}</li>
<li><strong>Sitio:</strong> {Encode(siteKey)}</li>
</ul>
""";

        return new CreateNotificationMessageRequest(
            "marketing.meeting.requested",
            null,
            "Email",
            "MeetingRequest",
            meetingRequestId,
            $"Nueva solicitud de reunión: {subject}",
            body,
            payloadJson,
            null,
            3,
            recipients);
    }

    public static CreateNotificationMessageRequest CreateConfirmed(string payloadJson)
    {
        using var document = Parse(payloadJson);
        var root = document.RootElement;
        var meetingRequestId = Required(root, "meetingRequestId");
        var subject = Required(root, "subject");
        var meetingTypeName = Required(root, "meetingTypeName");
        var start = Required(root, "confirmedStartUtc");
        var end = Required(root, "confirmedEndUtc");
        var timeZone = Required(root, "timeZone");
        var mode = Required(root, "meetingMode");
        var clientEmail = Required(root, "clientEmail").Trim().ToLowerInvariant();
        if (!clientEmail.Contains('@')) throw new InvalidOperationException("Confirmed meeting client email is invalid.");
        var clientName = Optional(root, "clientName");
        var meetingUrl = Optional(root, "meetingUrl");

        var meetingLink = string.IsNullOrWhiteSpace(meetingUrl)
            ? string.Empty
            : $"<li><strong>Enlace:</strong> {Encode(meetingUrl)}</li>";
        var greeting = string.IsNullOrWhiteSpace(clientName)
            ? "Hola,"
            : $"Hola {Encode(clientName)},";
        var body = $"""
<p>{greeting}</p>
<p>Tu reunión ha sido confirmada.</p>
<ul>
<li><strong>Asunto:</strong> {Encode(subject)}</li>
<li><strong>Tipo:</strong> {Encode(meetingTypeName)}</li>
<li><strong>Modalidad:</strong> {Encode(mode)}</li>
<li><strong>Inicio:</strong> {Encode(start)}</li>
<li><strong>Fin:</strong> {Encode(end)}</li>
<li><strong>Zona horaria:</strong> {Encode(timeZone)}</li>
{meetingLink}
</ul>
""";

        return new CreateNotificationMessageRequest(
            "marketing.meeting.confirmed",
            null,
            "Email",
            "MeetingRequest",
            meetingRequestId,
            $"Reunión confirmada: {subject}",
            body,
            payloadJson,
            null,
            3,
            [new NotificationRecipientRequest(null, clientEmail, clientName)]);
    }

    private static JsonDocument Parse(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new InvalidOperationException("Meeting notification payload is empty.");
        try
        {
            var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                throw new InvalidOperationException("Meeting notification payload must be a JSON object.");
            }
            return document;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Meeting notification payload is invalid JSON.", exception);
        }
    }

    private static string Required(JsonElement root, string name)
        => Optional(root, name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Meeting notification payload requires '{name}'.");

    private static string? Optional(JsonElement root, string name)
    {
        if (TryProperty(root, name, out var value))
        {
            if (value.ValueKind == JsonValueKind.String) return value.GetString()?.Trim();
            if (value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined) return value.ToString().Trim();
        }
        return null;
    }

    private static bool TryProperty(JsonElement root, string name, out JsonElement value)
    {
        if (root.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        return root.TryGetProperty(pascal, out value);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
