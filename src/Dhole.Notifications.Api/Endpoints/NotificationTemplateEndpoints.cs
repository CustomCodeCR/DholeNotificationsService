using Dhole.Notifications.Api.Authorization;
using Dhole.Notifications.Api.Extensions;
using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Contracts.Notifications;

namespace Dhole.Notifications.Api.Endpoints;

internal static class NotificationTemplateEndpoints
{
    public static IEndpointRouteBuilder MapNotificationTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications/templates").WithTags("Notification Templates").RequireAuthorization();

        group.MapGet("/", async (int pageNumber, int pageSize, string? search, bool? isActive, INotificationApplicationService service, CancellationToken ct)
            => Results.Ok(await service.BrowseTemplatesAsync(pageNumber, pageSize, search, isActive, ct)))
            .RequireScope(NotificationsScopeNames.MessagesView);

        group.MapGet("/{id:guid}", async (Guid id, INotificationApplicationService service, CancellationToken ct)
            => await service.GetTemplateAsync(id, ct) is { } item ? Results.Ok(item) : Results.NotFound())
            .RequireScope(NotificationsScopeNames.MessagesView);

        group.MapPost("/", async (CreateNotificationTemplateRequest request, INotificationApplicationService service, HttpContext context, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.CreateTemplateAsync(request, context.GetCurrentUserId(), ct)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
            { return Results.BadRequest(new { code = "notifications.template.invalid", message = ex.Message }); }
        }).RequireScope(NotificationsScopeNames.TemplatesManage);

        group.MapPut("/{id:guid}", async (Guid id, UpdateNotificationTemplateRequest request, INotificationApplicationService service, HttpContext context, CancellationToken ct) =>
        {
            try { return await service.UpdateTemplateAsync(id, request, context.GetCurrentUserId(), ct) is { } item ? Results.Ok(item) : Results.NotFound(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
            { return Results.BadRequest(new { code = "notifications.template.invalid", message = ex.Message }); }
        }).RequireScope(NotificationsScopeNames.TemplatesManage);

        group.MapPatch("/{id:guid}/active", async (Guid id, SetNotificationTemplateActiveRequest request, INotificationApplicationService service, HttpContext context, CancellationToken ct)
            => await service.SetTemplateActiveAsync(id, request.IsActive, context.GetCurrentUserId(), ct) ? Results.NoContent() : Results.NotFound())
            .RequireScope(NotificationsScopeNames.TemplatesManage);

        group.MapDelete("/{id:guid}", async (Guid id, INotificationApplicationService service, HttpContext context, CancellationToken ct)
            => await service.DeleteTemplateAsync(id, context.GetCurrentUserId(), ct) ? Results.NoContent() : Results.NotFound())
            .RequireScope(NotificationsScopeNames.TemplatesManage);

        return app;
    }
}
