using System.Security.Claims;
using System.Text.Json;
using CustomCodeFramework.Messaging.Outbox;
using Dhole.Notifications.Persistence.DbContexts;

namespace Dhole.Notifications.Api.Middleware;

public sealed class AuditEndpointMiddleware(RequestDelegate next, ILogger<AuditEndpointMiddleware> logger)
{
    private const string SourceService = "DholeNotificationsService";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);
        if (!ShouldAudit(context)) return;

        try
        {
            var db = context.RequestServices.GetRequiredService<ServiceDbContext>();
            var correlationId = ResolveCorrelationId(context);
            var entityType = ResolveEntityType(context);
            var action = ResolveAction(context);
            var entityId = ResolveEntityId(context);
            var now = DateTime.UtcNow;

            var payload = new
            {
                EventId = Guid.NewGuid(),
                CorrelationId = correlationId,
                SourceService,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                EventType = $"notifications.http.{entityType.ToLowerInvariant()}.{action}",
                UserId = ResolveUserId(context.User),
                UserName = ResolveUserName(context.User),
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                OccurredAt = now,
                BeforeJson = (string?)null,
                AfterJson = (string?)null,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    Method = context.Request.Method,
                    Path = context.Request.Path.Value,
                    QueryString = context.Request.QueryString.Value,
                    StatusCode = context.Response.StatusCode,
                    Endpoint = context.GetEndpoint()?.DisplayName,
                }, JsonOptions),
                Metadata = JsonSerializer.Serialize(new
                {
                    AuditLayer = "endpoint",
                    RouteValues = context.Request.RouteValues.ToDictionary(x => x.Key, x => x.Value?.ToString()),
                    Query = context.Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString()),
                    context.TraceIdentifier,
                }, JsonOptions),
                ErrorMessage = context.Response.StatusCode >= 400 ? $"HTTP {context.Response.StatusCode}" : null,
                StackTrace = (string?)null,
                Details = Array.Empty<object>(),
            };

            db.OutboxMessages.Add(new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = "Dhole.AuditLogs.Contracts.AuditEvents.RegisterAuditEventRequest",
                EventName = "audit.event.registered",
                SourceService = SourceService,
                PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
                HeadersJson = null,
                CorrelationId = correlationId.ToString(),
                Status = OutboxMessageStatus.Pending,
                RetryCount = 0,
                ErrorMessage = null,
                CreatedAtUtc = now,
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to audit Notifications action {Method} {Path}.", context.Request.Method, context.Request.Path.Value);
        }
    }

    private static bool ShouldAudit(HttpContext context)
        => context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/api/notifications/hub");

    private static string ResolveAction(HttpContext context)
    {
        if (context.Response.StatusCode == StatusCodes.Status401Unauthorized) return "unauthorized";
        if (context.Response.StatusCode == StatusCodes.Status403Forbidden) return "forbidden";
        if (context.Response.StatusCode >= 500) return "http_error";
        return context.Request.Method.ToUpperInvariant() switch
        {
            "GET" or "HEAD" => "viewed",
            "POST" => "created",
            "PUT" or "PATCH" => "updated",
            "DELETE" => "deleted",
            _ => "executed",
        };
    }

    private static string ResolveEntityType(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        if (path.Contains("template")) return "NotificationTemplate";
        if (path.Contains("message")) return "NotificationMessage";
        return "Notification";
    }

    private static Guid? ResolveEntityId(HttpContext context)
    {
        foreach (var value in context.Request.RouteValues.Values)
            if (Guid.TryParse(value?.ToString(), out var id)) return id;
        foreach (var segment in context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [])
            if (Guid.TryParse(segment, out var id)) return id;
        return null;
    }

    private static Guid ResolveCorrelationId(HttpContext context)
    {
        var raw = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        return Guid.TryParse(raw, out var id) ? id : Guid.NewGuid();
    }

    private static Guid? ResolveUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") ?? user.FindFirstValue("user_id");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static string? ResolveUserName(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("name") ?? user.Identity?.Name;
}
