using CustomCodeFramework.Redis.Streams.Abstractions;
using CustomCodeFramework.Redis.Streams.Messages;
using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Application.Meetings;
using Microsoft.EntityFrameworkCore;

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
            if (StreamNotificationSafety.IsStale(envelope))
            {
                logger.LogWarning("Discarded stale meeting request event {MessageId}.", envelope.MessageId);
                return;
            }
            var recipients = configuration
                .GetSection("Notifications:Meeting:MarketingRecipients")
                .GetChildren()
                .Select(item => item.Value ?? string.Empty)
                .ToArray();
            var request = MeetingNotificationMessageFactory.CreateRequested(envelope.PayloadJson, recipients);
            await notifications.CreateMessageAsync(request, null, cancellationToken, StreamNotificationSafety.FixedMessageId(envelope));
        }
        catch (DbUpdateException exception) when (StreamNotificationSafety.IsDuplicateInsert(exception))
        {
            logger.LogInformation("Duplicate meeting event {MessageId} already exists.", envelope.MessageId);
        }
        catch (DbUpdateException exception) when (StreamNotificationSafety.IsDuplicateInsert(exception))
        {
            logger.LogInformation("Duplicate meeting confirmation event {MessageId} already exists.", envelope.MessageId);
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
            if (StreamNotificationSafety.IsStale(envelope))
            {
                logger.LogWarning("Discarded stale meeting confirmation event {MessageId}.", envelope.MessageId);
                return;
            }
            var request = MeetingNotificationMessageFactory.CreateConfirmed(envelope.PayloadJson);
            await notifications.CreateMessageAsync(request, null, cancellationToken, StreamNotificationSafety.FixedMessageId(envelope));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not create client meeting confirmation from Redis message {MessageId}.", envelope.MessageId);
            throw;
        }
    }
}
