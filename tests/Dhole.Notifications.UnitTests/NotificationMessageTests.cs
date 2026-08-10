using Dhole.Notifications.Domain.Notifications.Entities;
using Dhole.Notifications.Domain.Notifications.Enums;

namespace Dhole.Notifications.UnitTests;

[TestClass]
public sealed class NotificationMessageTests
{
    [TestMethod]
    public void Create_WithFutureSchedule_SetsScheduledStatus()
    {
        var message = NotificationMessage.Create(
            "pricing.rate.expiring",
            "rate-expiring",
            NotificationChannel.System,
            "PricingRate",
            Guid.NewGuid().ToString(),
            "Rate expiring",
            null,
            "{}",
            DateTime.UtcNow.AddHours(1));

        message.AddRecipient(Guid.NewGuid(), string.Empty, "Pricing user");

        Assert.AreEqual(NotificationStatus.Scheduled, message.Status);
        Assert.AreEqual(1, message.Recipients.Count);
    }

    [TestMethod]
    public void RetryableFailure_ReachesDeadLetterAfterMaxAttempts()
    {
        var message = NotificationMessage.Create(
            "generic", null, NotificationChannel.Email, null, null,
            "Subject", "Body", "{}", null, maxAttempts: 2);

        message.AddRecipient(null, "test@example.com");
        message.MarkProcessing();
        message.MarkFailed("smtp_timeout", "Timeout", TimeSpan.FromSeconds(1));

        Assert.AreEqual(NotificationStatus.Retrying, message.Status);
        Assert.AreEqual(1, message.AttemptCount);

        message.MarkProcessing();
        message.MarkFailed("smtp_timeout", "Timeout", TimeSpan.FromSeconds(1));

        Assert.AreEqual(NotificationStatus.DeadLetter, message.Status);
        Assert.AreEqual(2, message.AttemptCount);
    }

    [TestMethod]
    public void NonRetryableFailure_SetsFailedStatus()
    {
        var message = NotificationMessage.Create(
            "generic", null, NotificationChannel.Email, null, null,
            "Subject", "Body", "{}", null);

        message.MarkProcessing();
        message.MarkTerminalFailed("invalid_recipient", "Invalid address");

        Assert.AreEqual(NotificationStatus.Failed, message.Status);
        Assert.AreEqual("invalid_recipient", message.LastErrorCode);
    }
}
