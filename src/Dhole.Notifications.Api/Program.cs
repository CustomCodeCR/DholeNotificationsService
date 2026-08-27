using Dhole.Notifications.Api.Hubs;
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
            context.Token = context.Request.Query["access_token"];
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
app.MapHub<NotificationsHub>("/api/notifications/hub");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ServiceDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.Run();
