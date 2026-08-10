using CustomCodeFramework.Auth.DependencyInjection;
using CustomCodeFramework.Mongo.DependencyInjection;
using CustomCodeFramework.Redis.DependencyInjection;
using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Domain.Notifications.Enums;
using Dhole.Notifications.Infrastructure.Cache;
using Dhole.Notifications.Infrastructure.Delivery;
using Dhole.Notifications.Infrastructure.Mongo;
using Dhole.Notifications.Infrastructure.Queue;
using Dhole.Notifications.Infrastructure.Rendering;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dhole.Notifications.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCustomCodeAuth(configuration);
        services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        });
        services.AddCustomCodeRedis(configuration);
        services.AddCustomCodeMongo(configuration);
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddScoped<INotificationTemplateCache, NotificationTemplateCache>();
        services.AddScoped<INotificationPendingQueue, NotificationPendingQueue>();
        services.AddScoped<INotificationDocumentStore, NotificationDocumentStore>();
        services.AddSingleton<INotificationTemplateRenderer, NotificationTemplateRenderer>();
        services.AddScoped<INotificationDeliveryChannel, SystemNotificationDeliveryChannel>();
        services.AddScoped<INotificationDeliveryChannel, EmailNotificationDeliveryChannel>();
        services.AddSingleton<INotificationDeliveryChannel>(new FutureNotificationDeliveryChannel(NotificationChannel.WhatsAppFuture));
        services.AddSingleton<INotificationDeliveryChannel>(new FutureNotificationDeliveryChannel(NotificationChannel.SmsFuture));
        services.AddSingleton<INotificationDeliveryChannel>(new FutureNotificationDeliveryChannel(NotificationChannel.WebhookFuture));
        return services;
    }
}
