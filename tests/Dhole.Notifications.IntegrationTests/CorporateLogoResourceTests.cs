using Dhole.Notifications.Infrastructure.Delivery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dhole.Notifications.IntegrationTests;

[TestClass]
public sealed class CorporateLogoResourceTests
{
    [TestMethod]
    public void LogoResourceIsEmbeddedAsPngWithExpectedSignature()
    {
        using var stream = typeof(EmailNotificationDeliveryChannel).Assembly
            .GetManifestResourceStream("Dhole.Notifications.Infrastructure.Delivery.Assets.CastroFallasLogoWhite.png");
        Assert.IsNotNull(stream);
        Assert.IsTrue(stream.Length > 500);
        Assert.AreEqual(0x89, stream.ReadByte());
        Assert.AreEqual(0x50, stream.ReadByte());
        Assert.AreEqual(0x4e, stream.ReadByte());
        Assert.AreEqual(0x47, stream.ReadByte());
    }
}
