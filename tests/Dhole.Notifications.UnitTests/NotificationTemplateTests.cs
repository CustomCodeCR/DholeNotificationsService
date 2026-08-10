using Dhole.Notifications.Domain.Notifications.Entities;
using Dhole.Notifications.Domain.Notifications.Enums;

namespace Dhole.Notifications.UnitTests;

[TestClass]
public sealed class NotificationTemplateTests
{
    [TestMethod]
    public void Create_NormalizesCodeAndStartsActive()
    {
        var template = NotificationTemplate.Create(
            "  Pricing-Rate-Expiring  ",
            "Pricing rate expiring",
            null,
            "pricing.rate.expiring",
            NotificationChannel.Email,
            "Rate {{rateName}}",
            "<p>{{rateName}}</p>",
            "[]");

        Assert.AreEqual("pricing-rate-expiring", template.Code);
        Assert.IsTrue(template.IsActive);
    }
}
