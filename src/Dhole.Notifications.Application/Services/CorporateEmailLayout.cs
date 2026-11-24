using System.Net;
using System.Text.RegularExpressions;
using Dhole.Notifications.Application.Abstractions;

namespace Dhole.Notifications.Application.Services;

/// <summary>
/// Shared, email-client-safe layout for every notification dispatched through
/// DholeNotificationsService. Keeps existing links, attachments and content intact.
/// </summary>
public static class CorporateEmailLayout
{
    public const string Marker = "data-dhole-email-layout=\"v1\"";

    public static RenderedNotification Apply(RenderedNotification rendered, string? notificationType)
    {
        var original = rendered.Body?.Trim() ?? string.Empty;

        // Safe for retries/reprocessing and preserves complete, already-branded
        // HTML documents (including the existing Pricing return template).
        if (original.Contains(Marker, StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(original, @"<(?:!doctype\s+html|html\b)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return rendered;

        var isHtml = Regex.IsMatch(original,
            @"<\s*(?:p|br|div|table|tbody|tr|td|a|h[1-6]|strong|b|em|ul|ol|li|span)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var content = isHtml
            ? original
            : WebUtility.HtmlEncode(original)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal)
                .Replace("\n", "<br />", StringComparison.Ordinal);

        var title = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(rendered.Subject)
            ? "Notificación de DHOLE"
            : rendered.Subject.Trim());
        var category = LabelFor(notificationType);
        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>{title}</title>
            </head>
            <body data-dhole-email-layout="v1" style="margin:0;padding:0;background:#f7f7f8;color:#30343b;font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background:#f7f7f8;border-collapse:collapse;">
                <tr><td align="center" style="padding:32px 12px;">
                  <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="600" style="width:100%;max-width:600px;border-collapse:separate;background:#ffffff;border:1px solid #e5e7eb;border-radius:14px;">
                    <tr><td style="padding:17px 30px;background:#a80e18;border-radius:13px 13px 0 0;">
                      <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="border-collapse:collapse;">
                        <tr>
                          <td width="125" valign="middle" style="width:125px;padding:0 20px 0 0;">
                            <img src="cid:dhole-castro-fallas-logo" alt="Castro Fallas" width="120" style="display:block;width:120px;height:auto;border:0;outline:0;text-decoration:none;" />
                          </td>
                          <td valign="middle" style="vertical-align:middle;">
                            <p style="margin:0;color:#ffe1e4;font-size:12px;font-weight:bold;letter-spacing:1px;">GRUPO CASTRO FALLAS</p>
                            <p style="margin:5px 0 0;color:#ffffff;font-size:15px;font-weight:bold;">DHOLE · Notificaciones</p>
                          </td>
                        </tr>
                      </table>
                    </td></tr>
                    <tr><td style="padding:28px 30px;">
                      <p style="margin:0 0 10px;color:#9a661c;font-size:12px;font-weight:bold;letter-spacing:.7px;">{category}</p>
                      <h1 style="margin:0 0 22px;color:#a80e18;font-size:23px;line-height:31px;overflow-wrap:break-word;">{title}</h1>
                      <div style="font-size:14px;line-height:23px;color:#343a40;overflow-wrap:anywhere;">{content}</div>
                    </td></tr>
                    <tr><td style="padding:18px 30px;border-top:1px solid #e5e7eb;">
                      <p style="margin:0;color:#6b7280;font-size:12px;line-height:18px;">Notificación automática de DHOLE · Grupo Castro Fallas</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
        return new RenderedNotification(rendered.Subject, html);
    }

    private static string LabelFor(string? type)
    {
        var value = type ?? string.Empty;
        if (value.Contains("overdue", StringComparison.OrdinalIgnoreCase)
            || value.Contains("expired", StringComparison.OrdinalIgnoreCase)
            || value.Contains("vencida", StringComparison.OrdinalIgnoreCase))
            return "VENCIMIENTO · ACCIÓN PENDIENTE";
        if (value.Contains("returned", StringComparison.OrdinalIgnoreCase)
            || value.Contains("devol", StringComparison.OrdinalIgnoreCase))
            return "ACCIÓN REQUERIDA";
        if (value.Contains("accepted", StringComparison.OrdinalIgnoreCase)
            || value.Contains("opening", StringComparison.OrdinalIgnoreCase))
            return "COTIZACIÓN ACEPTADA · APERTURA";
        return "NOTIFICACIÓN DEL SISTEMA";
    }
}
