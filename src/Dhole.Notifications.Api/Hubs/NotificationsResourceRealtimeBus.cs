using System.Text.Json;
using StackExchange.Redis;

namespace Dhole.Notifications.Api.Hubs;

// Redis Pub/Sub distributes minimal invalidations to every Notifications API
// instance; the owning REST API remains the source of truth after disconnects.
public sealed record ResourceInvalidation(
    Guid EventId, string EventType, string AggregateType,
    Guid AggregateId, long AggregateVersion, DateTimeOffset OccurredAtUtc);
public sealed record ResourceRealtimePush(string Kind, ResourceInvalidation Invalidation);

public sealed class NotificationsResourceRealtimeBus(IConfiguration configuration) : IAsyncDisposable
{
    private const string Channel = "dhole.notifications.resource.invalidation.v1";
    private readonly ConnectionMultiplexer _redis = ConnectionMultiplexer.Connect(
        configuration["Realtime:RedisConnectionString"] ??
        configuration["Redis:ConnectionString"] ??
        throw new InvalidOperationException("A Redis connection is required for Notifications realtime."));
    private static readonly RedisChannel RedisTopic = RedisChannel.Literal(Channel);

    public IDatabase Database => _redis.GetDatabase();

    public Task<long> PublishAsync(ResourceRealtimePush item) =>
        _redis.GetSubscriber().PublishAsync(RedisTopic, JsonSerializer.Serialize(item));

    public Task SubscribeAsync(Action<ResourceRealtimePush> callback) =>
        _redis.GetSubscriber().SubscribeAsync(RedisTopic, (_, raw) =>
        {
            if (raw.IsNullOrEmpty) return;
            try
            {
                var item = JsonSerializer.Deserialize<ResourceRealtimePush>(raw.ToString());
                if (item is not null &&
                    ResourceRealtimePolicy.TryCreate(item.Kind,
                        item.Invalidation.AggregateId.ToString("D"), out _) &&
                    item.Invalidation.EventId != Guid.Empty &&
                    item.Invalidation.AggregateVersion > 0)
                    callback(item);
            }
            catch (JsonException)
            {
                // Untrusted Redis payloads never reach clients.
            }
        });

    public Task UnsubscribeAsync() => _redis.GetSubscriber().UnsubscribeAsync(RedisTopic);

    public async ValueTask DisposeAsync()
    {
        await UnsubscribeAsync();
        await _redis.CloseAsync();
        _redis.Dispose();
    }
}
