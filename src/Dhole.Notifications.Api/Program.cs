using Dhole.Notifications.Api.Hubs;
using CustomCodeFramework.Redis.Abstractions;
using Dhole.Notifications.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using CustomCodeFramework.Api.DependencyInjection;
using CustomCodeFramework.Api.Swagger;
using CustomCodeFramework.Core.Abstractions;
using Dhole.Notifications.Api.Endpoints;
using Dhole.Notifications.Api.Grpc;
using Dhole.Notifications.Application.DependencyInjection;
using Dhole.Notifications.Infrastructure.DependencyInjection;
using Dhole.Notifications.Infrastructure.Time;
using Dhole.Notifications.Persistence.DbContexts;
using Dhole.Notifications.Persistence.DependencyInjection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
const string CorsPolicyName = "DholeWebCors";

builder.Services.AddCustomCodeApiWithSwagger(title: "Dhole Notifications Service", version: "v1");
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://127.0.0.1:5173"];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
    .WithOrigins(corsOrigins)
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddGrpc();
builder.Services.AddSignalR();
builder.Services.AddHostedService<NotificationRealtimeRelayService>();
builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    var previous = options.Events.OnMessageReceived;
    options.Events.OnMessageReceived = async context =>
    {
        if (previous is not null) await previous(context);
        if (string.IsNullOrWhiteSpace(context.Token)
            && context.HttpContext.Request.Path.StartsWithSegments("/api/notifications/hub"))
        {
            var ticket = context.Request.Query["ticket"].ToString();
            if (!string.IsNullOrWhiteSpace(ticket))
            {
                var cache = context.HttpContext.RequestServices.GetRequiredService<ICacheService>();
                context.Token = await HubAccessTicketStore.RedeemAsync(
                    cache, ticket, context.HttpContext.RequestAborted);
            }
            else
            {
                // Allow older frontend versions during a rolling deployment.
                context.Token = context.Request.Query["access_token"];
            }
        }
    };
});

var app = builder.Build();
app.UseCustomCodeApi();
app.UseCors(CorsPolicyName);
if (app.Environment.IsDevelopment()) app.UseCustomCodeSwagger();

app.MapGet("/health", () => Results.Ok(new { service = "DholeNotificationsService", status = "Healthy", timestamp = DateTimeOffset.UtcNow })).AllowAnonymous();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditEndpointMiddleware>();
app.MapGrpcService<NotificationsGrpcService>();
app.MapNotificationTemplateEndpoints();
app.MapNotificationMessageEndpoints();
app.MapNotificationInboxEndpoints();
app.MapHub<NotificationsHub>("/api/notifications/hub");

// The long Auth JWT is sent only in an Authorization header. The browser uses
// the returned opaque ticket for the WebSocket upgrade; it expires after 60s.
app.MapPost("/api/notifications/hub/ticket", async (HttpContext context, ICacheService cache) =>
{
    var authorization = context.Request.Headers.Authorization.ToString();
    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        return Results.Unauthorized();

    var accessToken = authorization["Bearer ".Length..].Trim();
    if (string.IsNullOrWhiteSpace(accessToken))
        return Results.Unauthorized();

    var ticket = await HubAccessTicketStore.IssueAsync(
        cache, accessToken, context.RequestAborted);
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(new { ticket, expiresInSeconds = HubAccessTicketStore.LifetimeSeconds });
}).RequireAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ServiceDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("""
        DO $dhole$
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'notifications'
                  AND table_name = 'notification_recipients'
                  AND column_name = 'read_at_utc'
            ) THEN
                ALTER TABLE notifications.notification_recipients
                ADD COLUMN read_at_utc timestamp with time zone NULL;

                UPDATE notifications.notification_recipients
                SET read_at_utc = created_at_utc
                WHERE read_at_utc IS NULL;
            END IF;
        END
        $dhole$;

        CREATE INDEX IF NOT EXISTS "IX_notification_recipients_user_read_created"
        ON notifications.notification_recipients (user_id, read_at_utc, created_at_utc DESC)
        WHERE user_id IS NOT NULL;
        """);
}

app.Run();
