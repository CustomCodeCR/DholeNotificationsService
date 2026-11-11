namespace Dhole.Notifications.Application.Services;

/// <summary>
/// Stable system notification text for CRM duplicate-prospect conflicts.
/// Notification delivery is best-effort; the source CRM request still returns 409.
/// </summary>
public static class CrmLeadDuplicateNotification
{
    public const string Type = "crm.lead.duplicate.v1";
    public const string Subject = "Prospecto ya registrado";

    public static (string Subject, string Body) Format(string? prospectName, string? sellerName)
    {
        var prospect = string.IsNullOrWhiteSpace(prospectName)
            ? "Este prospecto" : prospectName.Trim();
        var seller = string.IsNullOrWhiteSpace(sellerName)
            ? "Vendedor no disponible" : sellerName.Trim();
        return (Subject,
            $"El prospecto '{prospect}' ya existe en CRM y está asignado a {seller}. No se creó otro prospecto.");
    }
}
