using Dhole.Notifications.Domain.Notifications.Entities;

namespace Dhole.Notifications.UnitTests;

[TestClass]
public sealed class NotificationRecipientTests
{
    [TestMethod]
    public void MarkRead_SetsTimestampOnlyOnce()
    {
        var recipient = NotificationRecipient.Create(Guid.NewGuid(), Guid.NewGuid(), string.Empty);
        var firstRead = new DateTime(2026, 8, 28, 21, 0, 0, DateTimeKind.Utc);
        var secondRead = firstRead.AddMinutes(10);

        Assert.IsTrue(recipient.MarkRead(firstRead));
        Assert.AreEqual(firstRead, recipient.ReadAtUtc);
        Assert.IsFalse(recipient.MarkRead(secondRead));
        Assert.AreEqual(firstRead, recipient.ReadAtUtc);
    }
}
