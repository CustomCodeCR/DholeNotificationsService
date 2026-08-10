using Dhole.Notifications.Contracts.Notifications;
using Dhole.Notifications.Domain.Notifications.Entities;

namespace Dhole.Notifications.Application.Services;

internal static class NotificationMappings
{
    public static NotificationTemplateDto ToDto(this NotificationTemplate x) => new(
        x.Id, x.Code, x.Name, x.Description, x.NotificationType, x.Channel.ToString(), x.SubjectTemplate,
        x.BodyTemplate, x.DesignerJson, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc);

    public static NotificationMessageDto ToDto(this NotificationMessage x) => new(
        x.Id, x.NotificationType, x.TemplateCode, x.Channel.ToString(), x.EntityType, x.EntityId,
        x.Subject, x.Body, x.Status.ToString(), x.ScheduledForUtc, x.NextAttemptAtUtc, x.SentAtUtc,
        x.AttemptCount, x.MaxAttempts, x.LastErrorCode, x.LastErrorMessage, x.CreatedAtUtc, x.UpdatedAtUtc,
        x.Recipients.Select(r => new NotificationRecipientDto(r.Id, r.UserId, r.Address, r.DisplayName)).ToArray(),
        x.DeliveryAttempts.OrderBy(a => a.AttemptNumber).Select(a => new NotificationDeliveryAttemptDto(
            a.Id, a.NotificationRecipientId, a.AttemptNumber, a.Succeeded, a.Provider, a.ProviderMessageId,
            a.ErrorCode, a.ErrorMessage, a.StartedAtUtc, a.CompletedAtUtc)).ToArray());
}
