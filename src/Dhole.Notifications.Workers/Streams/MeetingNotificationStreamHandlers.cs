using CustomCodeFramework.Redis.Streams.Abstractions;
using CustomCodeFramework.Redis.Streams.Messages;
using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Application.Meetings;

namespace Dhole.Notifications.Workers.Streams;

internal sealed class MeetingRequestedStreamHandler(
    INotificationApplicationService notifications,
    IConfiguration configuration,
    ILogger<MeetingRequestedStreamHandler> logger) : IRedisStreamMessageHandler
{
    public string MessageType => "content.meeting.requested";

    public async Task HandleAsync(RedisStreamEnvelope envelope, CancellationToken cancellationToken = default)
    {
        try
        {
            var recipients = configuration
                .GetSection("Notifications:Meeting:MarketingRecipients")
                .GetChildren()
                .Select(item => item.Value ?? string.Empty)
                .ToArray();
            var request = MeetingNotificationMessageFactory.CreateRequested(envelope.PayloadJson, recipients);
            await notifications.CreateMessageAsync(request, null, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not create Marketing meeting notification from Redis message {MessageId}.", envelope.MessageId);
            throw;
        }
    }
}

internal sealed class MeetingConfirmedStreamHandler(
    INotificationApplicationService notifications,
    ILogger<MeetingConfirmedStreamHandler> logger) : IRedisStreamMessageHandler
{
    public string MessageType => "content.meeting.confirmed";

    public async Task HandleAsync(RedisStreamEnvelope envelope, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = MeetingNotificationMessageFactory.CreateConfirmed(envelope.PayloadJson);
            await notifications.CreateMessageAsync(request, null, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not create client meeting confirmation from Redis message {MessageId}.", envelope.MessageId);
            throw;
        }
    }
}
