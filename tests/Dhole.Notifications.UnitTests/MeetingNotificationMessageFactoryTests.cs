using System.Net;
using Dhole.Notifications.Application.Meetings;

namespace Dhole.Notifications.UnitTests;

[TestClass]
public sealed class MeetingNotificationMessageFactoryTests
{
    [TestMethod]
    public void CreateRequested_BuildsEmailForConfiguredMarketingRecipients()
    {
        var payload = "{\"meetingRequestId\":\"11111111-1111-1111-1111-111111111111\",\"siteKey\":\"main\",\"meetingTypeName\":\"Asesoría\",\"meetingMode\":\"Virtual\",\"requestedStartUtc\":\"2026-09-15T15:00:00Z\",\"requestedEndUtc\":\"2026-09-15T15:30:00Z\",\"timeZone\":\"America/Costa_Rica\",\"subject\":\"Cotización\"}";

        var message = MeetingNotificationMessageFactory.CreateRequested(payload, ["marketing@example.com", "MARKETING@example.com"]);

        Assert.AreEqual("Email", message.Channel);
        Assert.AreEqual("marketing.meeting.requested", message.NotificationType);
        Assert.HasCount(1, message.Recipients);
        Assert.AreEqual("marketing@example.com", message.Recipients.Single().Address);
        StringAssert.Contains(WebUtility.HtmlDecode(message.Body!), "Cotización");
    }

    [TestMethod]
    public void CreateRequested_RequiresMarketingRecipient()
    {
        var payload = "{\"meetingRequestId\":\"11111111-1111-1111-1111-111111111111\",\"siteKey\":\"main\",\"meetingTypeName\":\"Asesoría\",\"meetingMode\":\"Virtual\",\"requestedStartUtc\":\"2026-09-15T15:00:00Z\",\"requestedEndUtc\":\"2026-09-15T15:30:00Z\",\"timeZone\":\"UTC\",\"subject\":\"Cotización\"}";

        Assert.ThrowsExactly<InvalidOperationException>(() => MeetingNotificationMessageFactory.CreateRequested(payload, []));
    }

    [TestMethod]
    public void CreateConfirmed_UsesClientEmailAndEncodesUserContent()
    {
        var payload = "{\"meetingRequestId\":\"11111111-1111-1111-1111-111111111111\",\"meetingTypeName\":\"Asesoría\",\"meetingMode\":\"Virtual\",\"confirmedStartUtc\":\"2026-09-15T15:00:00Z\",\"confirmedEndUtc\":\"2026-09-15T15:30:00Z\",\"timeZone\":\"America/Costa_Rica\",\"subject\":\"<b>Consulta</b>\",\"clientEmail\":\" CLIENT@EXAMPLE.COM \",\"clientName\":\"Ana\",\"meetingUrl\":\"https://meet.example.com/abc\"}";

        var message = MeetingNotificationMessageFactory.CreateConfirmed(payload);

        Assert.AreEqual("client@example.com", message.Recipients.Single().Address);
        Assert.AreEqual("marketing.meeting.confirmed", message.NotificationType);
        StringAssert.Contains(message.Body!, "&lt;b&gt;Consulta&lt;/b&gt;");
        Assert.IsFalse(message.Body!.Contains("<b>Consulta</b>", StringComparison.Ordinal));
    }
}
