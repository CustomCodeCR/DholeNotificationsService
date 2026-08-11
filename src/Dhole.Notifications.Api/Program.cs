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
builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://192.168.1.193:5173")
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddGrpc();
builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseCustomCodeApi();
app.UseCors(CorsPolicyName);
if (app.Environment.IsDevelopment()) app.UseCustomCodeSwagger();

app.MapGet("/health", () => Results.Ok(new { service = "DholeNotificationsService", status = "Healthy", timestamp = DateTimeOffset.UtcNow })).AllowAnonymous();
app.UseAuthentication();
app.UseAuthorization();
app.MapGrpcService<NotificationsGrpcService>();
app.MapNotificationTemplateEndpoints();
app.MapNotificationMessageEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ServiceDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.Run();
