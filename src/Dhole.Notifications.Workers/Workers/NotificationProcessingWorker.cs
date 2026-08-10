using Dhole.Notifications.Application.Abstractions;

namespace Dhole.Notifications.Workers.Workers;

internal sealed class NotificationProcessingWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<NotificationProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Max(1, configuration.GetValue("Notifications:Worker:PollingIntervalSeconds", 5));
        var batchSize = Math.Clamp(configuration.GetValue("Notifications:Worker:BatchSize", 50), 1, 200);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<INotificationApplicationService>();
                var processed = await service.ProcessPendingAsync(batchSize, stoppingToken);
                if (processed > 0) logger.LogInformation("Processed {Count} notification messages.", processed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification processing cycle failed.");
            }
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }
}
