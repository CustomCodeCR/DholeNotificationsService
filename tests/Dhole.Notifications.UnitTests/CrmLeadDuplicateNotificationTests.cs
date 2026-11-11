using Dhole.Notifications.Application.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dhole.Notifications.UnitTests;

[TestClass]
public sealed class CrmLeadDuplicateNotificationTests
{
    [TestMethod]
    public void FormatsSellerAndProspectForSystemAlert()
    {
        var notification = CrmLeadDuplicateNotification.Format("  COMPUTADORAS PCI  ", "  Sharlyn Morales ");
        Assert.AreEqual("Prospecto ya registrado", notification.Subject);
        StringAssert.Contains(notification.Body, "COMPUTADORAS PCI");
        StringAssert.Contains(notification.Body, "Sharlyn Morales");
        StringAssert.Contains(notification.Body, "No se creó otro prospecto");
    }

    [TestMethod]
    public void GracefullyHandlesMissingAuthDisplayName()
    {
        var notification = CrmLeadDuplicateNotification.Format(null, null);
        StringAssert.Contains(notification.Body, "Vendedor no disponible");
    }
}
