using Dhole.Notifications.Api.Extensions;
using Dhole.Notifications.Contracts.Notifications;
using Dhole.Notifications.Domain.Notifications.Enums;
using Dhole.Notifications.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Dhole.Notifications.Api.Endpoints;

internal static class NotificationInboxEndpoints
{
    public static IEndpointRouteBuilder MapNotificationInboxEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications/inbox")
            .WithTags("Notification Inbox")
            .RequireAuthorization();

        group.MapGet("/", async (
            int? pageNumber,
            int? pageSize,
            bool? unreadOnly,
            ServiceDbContext dbContext,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.GetCurrentUserId();
            if (!userId.HasValue) return Results.Unauthorized();

            var page = Math.Max(1, pageNumber ?? 1);
            var size = Math.Clamp(pageSize ?? 20, 1, 100);
            var now = DateTime.UtcNow;

            var query = dbContext.NotificationRecipients
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId.Value
                    && x.NotificationMessage.Channel == NotificationChannel.System
                    && x.NotificationMessage.Status != NotificationStatus.Cancelled
                    && (!x.NotificationMessage.ScheduledForUtc.HasValue || x.NotificationMessage.ScheduledForUtc <= now));

            if (unreadOnly == true)
                query = query.Where(x => x.ReadAtUtc == null);

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(x => x.CreatedAtUtc)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(x => new NotificationInboxItemDto(
                    x.Id,
                    x.NotificationMessageId,
                    x.NotificationMessage.NotificationType,
                    x.NotificationMessage.Subject,
                    x.NotificationMessage.Body,
                    x.NotificationMessage.PayloadJson,
                    x.NotificationMessage.EntityType,
                    x.NotificationMessage.EntityId,
                    x.CreatedAtUtc,
                    x.ReadAtUtc))
                .ToListAsync(ct);

            return Results.Ok(new PagedNotificationResult<NotificationInboxItemDto>(items, page, size, total));
        });

        group.MapGet("/unread-count", async (
            ServiceDbContext dbContext,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.GetCurrentUserId();
            if (!userId.HasValue) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var count = await dbContext.NotificationRecipients
                .AsNoTracking()
                .CountAsync(x =>
                    x.UserId == userId.Value
                    && x.ReadAtUtc == null
                    && x.NotificationMessage.Channel == NotificationChannel.System
                    && x.NotificationMessage.Status != NotificationStatus.Cancelled
                    && (!x.NotificationMessage.ScheduledForUtc.HasValue || x.NotificationMessage.ScheduledForUtc <= now), ct);

            return Results.Ok(new NotificationUnreadCountDto(count));
        });

        group.MapPost("/{recipientId:guid}/read", async (
            Guid recipientId,
            ServiceDbContext dbContext,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.GetCurrentUserId();
            if (!userId.HasValue) return Results.Unauthorized();

            var recipient = await dbContext.NotificationRecipients
                .Include(x => x.NotificationMessage)
                .FirstOrDefaultAsync(x =>
                    x.Id == recipientId
                    && x.UserId == userId.Value
                    && x.NotificationMessage.Channel == NotificationChannel.System, ct);

            if (recipient is null) return Results.NotFound();
            if (recipient.MarkRead()) await dbContext.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapPost("/read-all", async (
            ServiceDbContext dbContext,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.GetCurrentUserId();
            if (!userId.HasValue) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var recipients = await dbContext.NotificationRecipients
                .Where(x =>
                    x.UserId == userId.Value
                    && x.ReadAtUtc == null
                    && x.NotificationMessage.Channel == NotificationChannel.System
                    && x.NotificationMessage.Status != NotificationStatus.Cancelled
                    && (!x.NotificationMessage.ScheduledForUtc.HasValue || x.NotificationMessage.ScheduledForUtc <= now))
                .ToListAsync(ct);

            foreach (var recipient in recipients)
                recipient.MarkRead(now);

            if (recipients.Count > 0) await dbContext.SaveChangesAsync(ct);
            return Results.Ok(new { markedRead = recipients.Count });
        });

        return app;
    }
}
