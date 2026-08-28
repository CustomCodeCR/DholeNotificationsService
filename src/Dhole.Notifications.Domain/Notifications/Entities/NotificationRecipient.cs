using CustomCodeFramework.Core.Domain.Entities;

namespace Dhole.Notifications.Domain.Notifications.Entities;

public sealed class NotificationRecipient : Entity<Guid>
{
    private NotificationRecipient() { }

    private NotificationRecipient(Guid id, Guid notificationMessageId, Guid? userId, string address, string? displayName)
        : base(id)
    {
        NotificationMessageId = notificationMessageId;
        UserId = userId;
        Address = address.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid NotificationMessageId { get; private set; }
    public Guid? UserId { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    public NotificationMessage NotificationMessage { get; private set; } = default!;

    public static NotificationRecipient Create(Guid notificationMessageId, Guid? userId, string address, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(address) && !userId.HasValue)
            throw new ArgumentException("Recipient address or user id is required.", nameof(address));

        var normalizedAddress = string.IsNullOrWhiteSpace(address) && userId.HasValue
            ? userId.Value.ToString()
            : address;

        return new NotificationRecipient(Guid.NewGuid(), notificationMessageId, userId, normalizedAddress, displayName);
    }

    public bool MarkRead(DateTime? readAtUtc = null)
    {
        if (ReadAtUtc.HasValue) return false;
        ReadAtUtc = readAtUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        return true;
    }
}
