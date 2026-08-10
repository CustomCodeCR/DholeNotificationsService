using CustomCodeFramework.Postgres.DependencyInjection;
using CustomCodeFramework.Postgres.EntityFramework.DependencyInjection;
using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Persistence.DbContexts;
using Dhole.Notifications.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dhole.Notifications.Persistence.DependencyInjection;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCustomCodePostgres(configuration);
        services.AddCustomCodePostgresEntityFramework<ServiceDbContext>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        return services;
    }
}
