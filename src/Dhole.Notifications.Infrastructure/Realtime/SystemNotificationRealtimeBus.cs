using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Dhole.Notifications.Infrastructure.Realtime;

public sealed class SystemNotificationRealtimeBus : IAsyncDisposable
{
    private const string ChannelName = "dhole.notifications.signalr";
    private readonly ConnectionMultiplexer _connection;
    private readonly ISubscriber _subscriber;

    public SystemNotificationRealtimeBus(IConfiguration configuration)
    {
        var connectionString = configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Redis:ConnectionString is required for realtime notifications.");
        _connection = ConnectionMultiplexer.Connect(connectionString);
        _subscriber = _connection.GetSubscriber();
    }

    public Task<long> PublishAsync(SystemNotificationPush notification)
        => _subscriber.PublishAsync(
            RedisChannel.Literal(ChannelName),
            JsonSerializer.Serialize(notification)
        );

    public Task SubscribeAsync(Action<SystemNotificationPush> handler)
        => _subscriber.SubscribeAsync(RedisChannel.Literal(ChannelName), (_, value) =>
        {
            if (value.IsNullOrEmpty) return;
            try
            {
                var notification = JsonSerializer.Deserialize<SystemNotificationPush>(value.ToString());
                if (notification is not null) handler(notification);
            }
            catch (JsonException)
            {
                // Invalid realtime payloads are ignored; persistent notification state remains intact.
            }
        });

    public Task UnsubscribeAsync() => _subscriber.UnsubscribeAsync(RedisChannel.Literal(ChannelName));

    public async ValueTask DisposeAsync()
    {
        await UnsubscribeAsync();
        await _connection.CloseAsync();
        _connection.Dispose();
    }
}
