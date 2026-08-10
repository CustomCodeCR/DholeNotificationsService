namespace Dhole.Notifications.Domain.Notifications.Enums;

public enum NotificationStatus
{
    Pending = 0,
    Scheduled = 1,
    Processing = 2,
    Sent = 3,
    Failed = 4,
    Retrying = 5,
    Cancelled = 6,
    DeadLetter = 7,
}
