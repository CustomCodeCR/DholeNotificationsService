using Dhole.Notifications.Api.Authorization;
using Dhole.Notifications.Api.Extensions;
using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Contracts.Notifications;

namespace Dhole.Notifications.Api.Endpoints;

internal static class NotificationMessageEndpoints
{
    public static IEndpointRouteBuilder MapNotificationMessageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications/messages").WithTags("Notification Messages").RequireAuthorization();

        group.MapPost("/", async (CreateNotificationMessageRequest request, INotificationApplicationService service, HttpContext context, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.CreateMessageAsync(request, context.GetCurrentUserId(), ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
            { return Results.BadRequest(new { code = "notifications.message.invalid", message = ex.Message }); }
        }).RequireScope(NotificationsScopeNames.MessagesCreate);

        group.MapGet("/", async (int pageNumber, int pageSize, string? search, string? status, string? channel, INotificationApplicationService service, CancellationToken ct)
            => Results.Ok(await service.BrowseMessagesAsync(pageNumber, pageSize, search, status, channel, ct)))
            .RequireScope(NotificationsScopeNames.MessagesView);

        group.MapGet("/{id:guid}", async (Guid id, INotificationApplicationService service, CancellationToken ct)
            => await service.GetMessageAsync(id, ct) is { } item ? Results.Ok(item) : Results.NotFound())
            .RequireScope(NotificationsScopeNames.MessagesView);

        group.MapPost("/{id:guid}/cancel", async (Guid id, INotificationApplicationService service, HttpContext context, CancellationToken ct)
            => await service.CancelMessageAsync(id, context.GetCurrentUserId(), ct) ? Results.NoContent() : Results.NotFound())
            .RequireScope(NotificationsScopeNames.MessagesCreate);

        group.MapGet("/history/entity", async (string entityType, string entityId, int pageNumber, int pageSize, INotificationApplicationService service, CancellationToken ct)
            => Results.Ok(await service.BrowseByEntityAsync(entityType, entityId, pageNumber, pageSize, ct)))
            .RequireScope(NotificationsScopeNames.HistoryView);

        group.MapGet("/history/recipient", async (string recipient, int pageNumber, int pageSize, INotificationApplicationService service, CancellationToken ct)
            => Results.Ok(await service.BrowseByRecipientAsync(recipient, pageNumber, pageSize, ct)))
            .RequireScope(NotificationsScopeNames.HistoryView);

        return app;
    }
}
