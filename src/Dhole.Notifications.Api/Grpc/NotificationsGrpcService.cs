using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Contracts.Grpc;
using Dhole.Notifications.Contracts.Notifications;
using Grpc.Core;

namespace Dhole.Notifications.Api.Grpc;

public sealed class NotificationsGrpcService(INotificationApplicationService service) : NotificationsGrpc.NotificationsGrpcBase
{
    public override async Task<CreateNotificationGrpcResponse> CreateNotification(CreateNotificationGrpcRequest request, ServerCallContext context)
    {
        try
        {
            var recipients = request.Recipients.Select(x => new NotificationRecipientRequest(
                Guid.TryParse(x.UserId, out var uid) ? uid : null,
                x.Address,
                string.IsNullOrWhiteSpace(x.DisplayName) ? null : x.DisplayName)).ToArray();
            var scheduled = DateTime.TryParse(request.ScheduledForUtc, out var parsed) ? parsed.ToUniversalTime() : (DateTime?)null;
            var result = await service.CreateMessageAsync(new CreateNotificationMessageRequest(
                request.NotificationType,
                EmptyToNull(request.TemplateCode),
                request.Channel,
                EmptyToNull(request.EntityType),
                EmptyToNull(request.EntityId),
                EmptyToNull(request.Subject),
                EmptyToNull(request.Body),
                EmptyToNull(request.PayloadJson) ?? "{}",
                scheduled,
                request.MaxAttempts <= 0 ? 3 : request.MaxAttempts,
                recipients), null, context.CancellationToken);

            return new CreateNotificationGrpcResponse
            {
                Success = true,
                NotificationId = result.Id.ToString(),
                Status = result.Status,
            };
        }
        catch (Exception ex)
        {
            return new CreateNotificationGrpcResponse { Success = false, Error = ex.Message };
        }
    }

    public override async Task<NotificationOperationGrpcResponse> CancelNotification(CancelNotificationGrpcRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NotificationId, out var id)) return new NotificationOperationGrpcResponse { Success = false, Error = "Invalid notification id." };
        var actor = Guid.TryParse(request.ActorUserId, out var actorId) ? actorId : (Guid?)null;
        var success = await service.CancelMessageAsync(id, actor, context.CancellationToken);
        return new NotificationOperationGrpcResponse { Success = success, Status = success ? "Cancelled" : "NotFound" };
    }

    public override async Task<GetNotificationGrpcResponse> GetNotification(GetNotificationGrpcRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.NotificationId, out var id)) return new GetNotificationGrpcResponse { Found = false };
        var item = await service.GetMessageAsync(id, context.CancellationToken);
        if (item is null) return new GetNotificationGrpcResponse { Found = false };
        return new GetNotificationGrpcResponse
        {
            Found = true,
            NotificationId = item.Id.ToString(),
            NotificationType = item.NotificationType,
            TemplateCode = item.TemplateCode ?? string.Empty,
            Channel = item.Channel,
            EntityType = item.EntityType ?? string.Empty,
            EntityId = item.EntityId ?? string.Empty,
            Subject = item.Subject ?? string.Empty,
            Body = item.Body ?? string.Empty,
            Status = item.Status,
            ScheduledForUtc = item.ScheduledForUtc?.ToString("O") ?? string.Empty,
            SentAtUtc = item.SentAtUtc?.ToString("O") ?? string.Empty,
            AttemptCount = item.AttemptCount,
            LastError = item.LastErrorMessage ?? string.Empty,
        };
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
