using System.Security.Claims;
using System.Text.Json;
using Dhole.Notifications.Infrastructure.Realtime;
using Dhole.Notifications.Api.Hubs;

namespace Dhole.Notifications.IntegrationTests;

[TestClass]
public sealed class NotificationRealtimeF6AudienceTests
{
    private static ClaimsPrincipal Principal(string value, bool authenticated = true)
        => new(new ClaimsIdentity(
            [new Claim("user_id", value)], authenticated ? "jwt" : null));

    [TestMethod]
    public void Only_valid_authenticated_Guid_users_join_their_own_group()
    {
        var user = Guid.NewGuid();
        Assert.IsTrue(NotificationAudiencePolicy.TryUserGroup(
            Principal(user.ToString("B")), out var group));
        Assert.AreEqual("user:" + user.ToString("D"), group);
        Assert.IsFalse(NotificationAudiencePolicy.TryUserGroup(
            Principal("all:customers"), out _));
        Assert.IsFalse(NotificationAudiencePolicy.TryUserGroup(
            Principal(Guid.Empty.ToString("D")), out _));
        Assert.IsFalse(NotificationAudiencePolicy.TryUserGroup(
            Principal(user.ToString("D"), authenticated: false), out _));
        Assert.IsFalse(NotificationAudiencePolicy.TryUserGroup(null, out _));
    }

    [TestMethod]
    public void Catalog_list_rejects_arbitrary_subscription_ids()
    {
        var id = ResourceRealtimePolicy.CatalogListId;
        Assert.IsTrue(ResourceRealtimePolicy.TryCreate("config.catalog-list", id.ToString("D"), out var resource));
        Assert.AreEqual("/api/config/catalog-items/realtime-access", ResourceRealtimePolicy.PathFor(resource));
        Assert.IsFalse(ResourceRealtimePolicy.TryCreate("config.catalog-list", Guid.NewGuid().ToString("D"), out _));
        Assert.IsFalse(ResourceRealtimePolicy.TryCreate("config.catalog-list", Guid.Empty.ToString("D"), out _));
        Assert.IsFalse(ResourceRealtimePolicy.TryCreate("config.private-catalogs", id.ToString("D"), out _));
    }

    [TestMethod]
    public void CRM_and_Pricing_list_subscriptions_reject_other_resource_ids()
    {
        var resources = new[]
        {
            ("pricing.cost-list", ResourceRealtimePolicy.PricingCostListId,
                "/api/pricing/costs/realtime-access"),
            ("pricing.rate-list", ResourceRealtimePolicy.PricingRateListId,
                "/api/pricing/rates/realtime-access"),
            ("crm.accountlist", ResourceRealtimePolicy.CrmAccountListId,
                "/api/crm/accounts/realtime-access"),
            ("crm.leadlist", ResourceRealtimePolicy.CrmLeadListId,
                "/api/crm/leads/realtime-access")
        };
        foreach (var (kind, id, path) in resources)
        {
            Assert.IsTrue(ResourceRealtimePolicy.TryCreate(kind, id.ToString("D"), out var resource));
            Assert.AreEqual(path, ResourceRealtimePolicy.PathFor(resource));
            Assert.IsFalse(ResourceRealtimePolicy.TryCreate(kind, Guid.NewGuid().ToString("D"), out _));
        }
    }

    [TestMethod]
    public void Existing_NotificationsHub_stream_accepts_collection_invalidation_envelopes()
    {
        var cases = new[]
        {
            ("dhole.config.realtime.v1", "DholeConfigService", "config:catalog-list",
                ResourceRealtimePolicy.CatalogListId, "catalog-list"),
            ("dhole.pricing.realtime.v1", "DholePricingService", "pricing:cost-list",
                ResourceRealtimePolicy.PricingCostListId, "cost-list"),
            ("dhole.pricing.realtime.v1", "DholePricingService", "pricing:rate-list",
                ResourceRealtimePolicy.PricingRateListId, "rate-list"),
            ("dhole.crm.realtime.v1", "DholeCRMService", "crm:accountlist",
                ResourceRealtimePolicy.CrmAccountListId, "accountlist"),
            ("dhole.crm.realtime.v1", "DholeCRMService", "crm:leadlist",
                ResourceRealtimePolicy.CrmLeadListId, "leadlist")
        };
        foreach (var (stream, producer, prefix, id, type) in cases)
        {
            var evt = Guid.NewGuid();
            var kind = prefix.Replace(':', '.');
            var json = JsonSerializer.Serialize(new
            {
                eventId = evt,
                eventType = kind + ".changed.v1",
                schemaVersion = 1,
                aggregateType = type,
                aggregateId = id.ToString("D"),
                aggregateVersion = 100L,
                occurredAtUtc = DateTimeOffset.UtcNow,
                audienceRef = prefix + ":" + id.ToString("D"),
                producer,
                payload = new { changedFields = new[] { "collection" } }
            });
            Assert.IsTrue(NotificationsResourceStreamConsumer.TryParseEvent(
                stream, evt.ToString("D"), json, out var push), kind);
            Assert.IsNotNull(push);
            Assert.AreEqual(kind, push.Kind);
            Assert.IsFalse(NotificationsResourceStreamConsumer.TryParseEvent(
                stream, Guid.NewGuid().ToString("D"), json, out _), "Wrong eventId must be rejected");
        }
    }

    [TestMethod]
    public void Push_uses_only_server_validated_recipient_and_never_broadcasts()
    {
        var user = Guid.NewGuid();
        var message = new SystemNotificationPush(
            Guid.NewGuid(), Guid.NewGuid(), user,
            "crm.activity.reminder", "Reminder", "Body", "{}", null, null,
            DateTime.UtcNow);
        Assert.IsTrue(NotificationAudiencePolicy.TryPushGroup(message, out var group));
        Assert.AreEqual($"user:{user:D}", group);
        Assert.IsFalse(NotificationAudiencePolicy.TryPushGroup(message with
            { UserId = Guid.Empty }, out _));
        Assert.IsFalse(NotificationAudiencePolicy.TryPushGroup(message with
            { RecipientId = Guid.Empty }, out _));
        Assert.IsFalse(NotificationAudiencePolicy.TryPushGroup(message with
            { NotificationId = Guid.Empty }, out _));
        Assert.IsFalse(NotificationAudiencePolicy.TryPushGroup(message with
            { NotificationType = "" }, out _));
    }

    [TestMethod]
    public void Prefix_lookalike_or_token_claim_cannot_target_another_users_group()
    {
        var user = Guid.NewGuid();
        Assert.IsFalse(NotificationAudiencePolicy.TryUserGroup(
            Principal("user:" + user.ToString("D")), out _));
        var claims = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.ToString("D"))], "jwt"));
        Assert.IsTrue(NotificationAudiencePolicy.TryUserGroup(claims, out var group));
        Assert.AreEqual("user:" + user.ToString("D"), group);
    }
}
