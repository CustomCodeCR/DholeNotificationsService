using Dhole.Notifications.Application.Abstractions;
using Dhole.Notifications.Application.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dhole.Notifications.UnitTests;

[TestClass]
public sealed class CorporateEmailLayoutTests
{
    [TestMethod]
    public void PlainTextIsEscapedAndWrappedInCorporateLayout()
    {
        var result = CorporateEmailLayout.Apply(
            new RenderedNotification("Nueva solicitud <Ventas>", "Cliente: A&B\nRuta: China → Costa Rica"),
            "pricing.rate.requested");
        StringAssert.Contains(result.Body, CorporateEmailLayout.Marker);
        StringAssert.Contains(result.Body, "GRUPO CASTRO FALLAS");
        StringAssert.Contains(result.Body, "A&amp;B");
        StringAssert.Contains(result.Body, "Nueva solicitud &lt;Ventas&gt;");
        StringAssert.Contains(result.Body, "<br />");
    }

    [TestMethod]
    public void ExistingHtmlKeepsItsLinkAndLayoutIsIdempotent()
    {
        var original = new RenderedNotification("Apertura", "<p>Cliente</p><a href=\"https://example.com/rate\">Consultar</a>");
        var first = CorporateEmailLayout.Apply(original, "pricing.rate.accepted.opening");
        var second = CorporateEmailLayout.Apply(first, "pricing.rate.accepted.opening");
        Assert.AreEqual(first.Body, second.Body);
        StringAssert.Contains(first.Body, "https://example.com/rate");
        StringAssert.Contains(first.Body, "COTIZACIÓN ACEPTADA");
    }

    [TestMethod]
    public void CompleteBrandedHtmlIsPreservedWithoutNestedDocuments()
    {
        var original = new RenderedNotification("Devuelta", "<!DOCTYPE html><html><body><h1>Corregir</h1></body></html>");
        Assert.AreEqual(original.Body, CorporateEmailLayout.Apply(original, "pricing.rate.returned").Body);
    }
    [TestMethod]
    public void CorporateEmailUsesCastroFallasRedAndInlineWhiteLogo()
    {
        var result = CorporateEmailLayout.Apply(
            new RenderedNotification("Compromiso comercial vencido", "<p>Consultar en CRM</p>"),
            "crm.commitment.overdue.v1");

        StringAssert.Contains(result.Body, "background:#a80e18");
        StringAssert.Contains(result.Body, "cid:dhole-castro-fallas-logo");
        StringAssert.Contains(result.Body, "alt=\"Castro Fallas\"");
        Assert.IsFalse(result.Body.Contains("#123f34", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(result.Body.Contains("#14765c", StringComparison.OrdinalIgnoreCase));
    }

}
