using CustomCodeFramework.Cqrs.DependencyInjection;
using CustomCodeFramework.Validation.DependencyInjection;
using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Dhole.Notifications.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddCustomCodeValidation(assembly);
        services.AddCustomCodeCqrs(assembly);
        services.AddCustomCodeCqrsBehaviors();
        services.AddScoped<INotificationApplicationService, NotificationApplicationService>();
        return services;
    }
}
