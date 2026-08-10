using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Dhole.Notifications.Persistence.DbContexts;

public sealed class ServiceDbContextFactory : IDesignTimeDbContextFactory<ServiceDbContext>
{
    public ServiceDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "Dhole.Notifications.Api"));
        if (!Directory.Exists(basePath))
            basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Dhole.Notifications.Api"));
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(basePath, "appsettings.json"), optional: false)
            .AddJsonFile(Path.Combine(basePath, "appsettings.Development.json"), optional: true)
            .AddEnvironmentVariables().Build();
        var connectionString = configuration.GetConnectionString("Postgres") ?? configuration["Postgres:ConnectionString"];
        return new ServiceDbContext(new DbContextOptionsBuilder<ServiceDbContext>().UseNpgsql(connectionString).Options);
    }
}
