using Dhole.Notifications.Application.Services;
using Dhole.Notifications.Domain.Notifications.Entities;
using Dhole.Notifications.Domain.Notifications.Enums;

namespace Dhole.Notifications.UnitTests;

[TestClass]
public sealed class EmailDeliverySafetyPolicyTests
{
    [TestMethod]
    public void QuoteBurstBlocksAfterThreeSameTypeOrTenTotal()
    {
        Assert.IsFalse(EmailDeliverySafetyPolicy.ShouldBlockQuoteBurst("RateHeader", "quote-362", 9, 2));
        Assert.IsTrue(EmailDeliverySafetyPolicy.ShouldBlockQuoteBurst("RateHeader", "quote-362", 3, 3));
        Assert.IsTrue(EmailDeliverySafetyPolicy.ShouldBlockQuoteBurst("RateHeader", "quote-362", 10, 1));
        Assert.IsFalse(EmailDeliverySafetyPolicy.ShouldBlockQuoteBurst("AnotherEntity", "other", 99, 99));
    }

    [TestMethod]
    public void UnconfirmedSmtpReservationPreventsAutomaticReplay()
    {
        var message = NotificationMessage.Create("pricing.rate.ready", null,
            NotificationChannel.Email, "RateHeader", "quote-362",
            "Tarifa lista", "Body", "{}", null);
        var recipient = message.AddRecipient(null, "seller@example.com");
        Assert.IsFalse(EmailDeliverySafetyPolicy.HasUnconfirmedStart(message, recipient.Id));

        var started = DateTime.UtcNow;
        message.AddDeliveryAttempt(NotificationDeliveryAttempt.Create(
            message.Id, recipient.Id, 1, false, "SMTP", null,
            EmailDeliverySafetyPolicy.StartedAttemptCode,
            "Started", started, started));
        Assert.IsTrue(EmailDeliverySafetyPolicy.HasUnconfirmedStart(message, recipient.Id));
    }
}
