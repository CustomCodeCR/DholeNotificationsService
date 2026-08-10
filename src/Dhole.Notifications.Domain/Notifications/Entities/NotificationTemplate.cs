using CustomCodeFramework.Core.Domain.Entities;
using Dhole.Notifications.Domain.Notifications.Enums;

namespace Dhole.Notifications.Domain.Notifications.Entities;

public sealed class NotificationTemplate : SoftDeletableAggregateRoot<Guid>
{
    private NotificationTemplate() { }

    private NotificationTemplate(
        Guid id,
        string code,
        string name,
        string? description,
        string notificationType,
        NotificationChannel channel,
        string? subjectTemplate,
        string bodyTemplate,
        string designerJson,
        Guid? createdBy)
        : base(id)
    {
        Code = NormalizeCode(code);
        Name = name.Trim();
        Description = NormalizeOptional(description);
        NotificationType = notificationType.Trim();
        Channel = channel;
        SubjectTemplate = NormalizeOptional(subjectTemplate);
        BodyTemplate = bodyTemplate;
        DesignerJson = designerJson;
        IsActive = true;
        MarkAsCreated(DateTime.UtcNow, createdBy?.ToString());
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string NotificationType { get; private set; } = string.Empty;
    public NotificationChannel Channel { get; private set; }
    public string? SubjectTemplate { get; private set; }
    public string BodyTemplate { get; private set; } = string.Empty;
    public string DesignerJson { get; private set; } = "[]";
    public bool IsActive { get; private set; }

    public static NotificationTemplate Create(
        string code,
        string name,
        string? description,
        string notificationType,
        NotificationChannel channel,
        string? subjectTemplate,
        string bodyTemplate,
        string designerJson,
        Guid? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Template code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Template name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(notificationType)) throw new ArgumentException("Notification type is required.", nameof(notificationType));
        if (string.IsNullOrWhiteSpace(bodyTemplate)) throw new ArgumentException("Template body is required.", nameof(bodyTemplate));

        return new NotificationTemplate(
            Guid.NewGuid(), code, name, description, notificationType, channel,
            subjectTemplate, bodyTemplate, string.IsNullOrWhiteSpace(designerJson) ? "[]" : designerJson,
            createdBy);
    }

    public void Update(
        string name,
        string? description,
        string notificationType,
        NotificationChannel channel,
        string? subjectTemplate,
        string bodyTemplate,
        string designerJson,
        Guid? updatedBy = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Template name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(notificationType)) throw new ArgumentException("Notification type is required.", nameof(notificationType));
        if (string.IsNullOrWhiteSpace(bodyTemplate)) throw new ArgumentException("Template body is required.", nameof(bodyTemplate));

        Name = name.Trim();
        Description = NormalizeOptional(description);
        NotificationType = notificationType.Trim();
        Channel = channel;
        SubjectTemplate = NormalizeOptional(subjectTemplate);
        BodyTemplate = bodyTemplate;
        DesignerJson = string.IsNullOrWhiteSpace(designerJson) ? "[]" : designerJson;
        MarkAsUpdated(DateTime.UtcNow, updatedBy?.ToString());
    }

    public void SetActive(bool isActive, Guid? updatedBy = null)
    {
        IsActive = isActive;
        MarkAsUpdated(DateTime.UtcNow, updatedBy?.ToString());
    }

    public void Delete(Guid? deletedBy = null)
    {
        MarkAsDeleted(DateTime.UtcNow, deletedBy?.ToString());
    }

    private static string NormalizeCode(string value) => value.Trim().ToLowerInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
