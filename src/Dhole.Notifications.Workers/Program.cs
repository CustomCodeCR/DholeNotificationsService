using CustomCodeFramework.Core.Abstractions;
using CustomCodeFramework.Redis.Streams.DependencyInjection;
using Dhole.Notifications.Application.DependencyInjection;
using Dhole.Notifications.Infrastructure.DependencyInjection;
using Dhole.Notifications.Infrastructure.Time;
using Dhole.Notifications.Persistence.DependencyInjection;
using Dhole.Notifications.Workers.Security;
using Dhole.Notifications.Workers.Streams;
using Dhole.Notifications.Workers.Workers;

var contentRoot = Path.Combine(Directory.GetCurrentDirectory(), "src", "Dhole.Notifications.Workers");

if (!Directory.Exists(contentRoot))
    contentRoot = Directory.GetCurrentDirectory();

var builder = Host.CreateApplicationBuilder(
    new HostApplicationBuilderSettings { Args = args, ContentRootPath = contentRoot }
);

builder.Configuration.Sources.Clear();
builder.Configuration
    .SetBasePath(contentRoot)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
builder.Services.AddScoped<ICurrentUser, WorkerCurrentUser>();
builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddWorkerInfrastructure(builder.Configuration);
builder.Services.AddCustomCodeRedisStreams(builder.Configuration);
builder.Services.AddCustomCodeRedisStreamConsumerBackgroundService();
builder.Services.AddCustomCodeRedisStreamHandler<NotificationRequestedStreamHandler>();
builder.Services.AddHostedService<NotificationProcessingWorker>();

var host = builder.Build();
await host.RunAsync();
