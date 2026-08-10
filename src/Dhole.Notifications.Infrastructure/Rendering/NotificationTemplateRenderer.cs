using System.Text.Json;
using System.Text.RegularExpressions;
using Dhole.Notifications.Application.Abstractions;

namespace Dhole.Notifications.Infrastructure.Rendering;

public sealed partial class NotificationTemplateRenderer : INotificationTemplateRenderer
{
    public RenderedNotification Render(string? subjectTemplate, string bodyTemplate, string payloadJson)
    {
        var values = Flatten(payloadJson);
        string RenderText(string? template)
        {
            if (string.IsNullOrEmpty(template)) return template ?? string.Empty;
            return TokenRegex().Replace(template, match =>
            {
                var key = match.Groups[1].Value.Trim();
                return values.TryGetValue(key, out var value) ? value : match.Value;
            });
        }

        return new RenderedNotification(
            string.IsNullOrWhiteSpace(subjectTemplate) ? null : RenderText(subjectTemplate),
            RenderText(bodyTemplate));
    }

    private static Dictionary<string, string> Flatten(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Walk(document.RootElement, string.Empty, result);
        return result;
    }

    private static void Walk(JsonElement element, string prefix, IDictionary<string, string> output)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    Walk(property.Value, string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}", output);
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray()) Walk(item, $"{prefix}.{index++}", output);
                if (!string.IsNullOrEmpty(prefix)) output[prefix] = element.GetRawText();
                break;
            case JsonValueKind.String:
                output[prefix] = element.GetString() ?? string.Empty;
                break;
            case JsonValueKind.Null:
                output[prefix] = string.Empty;
                break;
            default:
                output[prefix] = element.ToString();
                break;
        }
    }

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex TokenRegex();
}
