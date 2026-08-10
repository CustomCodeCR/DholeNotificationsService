using CustomCodeFramework.Redis.Streams.DependencyInjection;
using Dhole.Notifications.Application.DependencyInjection;
using Dhole.Notifications.Infrastructure.DependencyInjection;
using Dhole.Notifications.Persistence.DependencyInjection;
using Dhole.Notifications.Workers.Streams;
using Dhole.Notifications.Workers.Workers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCustomCodeRedisStreams(builder.Configuration);
builder.Services.AddCustomCodeRedisStreamConsumerBackgroundService();
builder.Services.AddCustomCodeRedisStreamHandler<NotificationRequestedStreamHandler>();
builder.Services.AddHostedService<NotificationProcessingWorker>();
var host = builder.Build();
await host.RunAsync();
