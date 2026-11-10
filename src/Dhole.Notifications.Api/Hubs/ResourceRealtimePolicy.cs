using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace Dhole.Notifications.Api.Hubs;

public readonly record struct RealtimeResource(string Kind, Guid Id)
{
    public string Key => $"{Kind}:{Id:N}";
    public string Group => $"dhole:rt:{Key}";
}

public static class ResourceRealtimePolicy
{
    // A catalog-list signal is a fixed metadata-free resource, never a caller-picked group.
    public static readonly Guid CatalogListId = Guid.Parse("00000000-0000-4000-8000-000000000042");
    public static readonly Guid PricingCostListId = Guid.Parse("00000000-0000-4000-8000-000000000043");
    public static readonly Guid PricingRateListId = Guid.Parse("00000000-0000-4000-8000-000000000044");
    public static readonly Guid CrmAccountListId = Guid.Parse("00000000-0000-4000-8000-000000000045");
    public static readonly Guid CrmLeadListId = Guid.Parse("00000000-0000-4000-8000-000000000046");
    private static readonly IReadOnlyDictionary<string, Guid> ModuleIds =
        new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["crm"] = Guid.Parse("00000000-0000-4000-8000-000000000051"),
            ["pricing"] = Guid.Parse("00000000-0000-4000-8000-000000000052"),
            ["auth"] = Guid.Parse("00000000-0000-4000-8000-000000000053"),
            ["config"] = Guid.Parse("00000000-0000-4000-8000-000000000054"),
            ["srm"] = Guid.Parse("00000000-0000-4000-8000-000000000055"),
            ["audit"] = Guid.Parse("00000000-0000-4000-8000-000000000056"),
            ["storage"] = Guid.Parse("00000000-0000-4000-8000-000000000057"),
            ["extraction"] = Guid.Parse("00000000-0000-4000-8000-000000000058"),
            ["reports"] = Guid.Parse("00000000-0000-4000-8000-000000000059"),
            ["dynamics"] = Guid.Parse("00000000-0000-4000-8000-000000000060"),
            ["ai"] = Guid.Parse("00000000-0000-4000-8000-000000000061"),
            ["agent"] = Guid.Parse("00000000-0000-4000-8000-000000000062"),
            ["content"] = Guid.Parse("00000000-0000-4000-8000-000000000063")
        };

    // Safe module-level notifications have fixed IDs; delivery is still
    // reauthorized against the original owner resource, not this topic.
    public static bool IsModuleResource(RealtimeResource resource) =>
        resource.Kind.StartsWith("module.", StringComparison.Ordinal) &&
        ModuleIds.TryGetValue(resource.Kind["module.".Length..], out var expected) &&
        resource.Id == expected;

    public static bool TryModuleFor(RealtimeResource original, out RealtimeResource module)
    {
        module = default;
        if (IsModuleResource(original)) return false;
        var prefix = original.Kind.Split('.')[0];
        if (!ModuleIds.TryGetValue(prefix, out var moduleId)) return false;
        module = new RealtimeResource("module." + prefix, moduleId);
        return true;
    }

    // Only an owner-controlled REST endpoint can authorize a subscription.
    // Never allow arbitrary caller-supplied groups or paths.
    private static readonly IReadOnlyDictionary<string, string> Paths =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["crm.account"] = "/api/crm/accounts/{id}",
            ["pricing.rate"] = "/api/pricing/rates/{id}",
            ["pricing.cost-list"] = "/api/pricing/costs/realtime-access",
            ["pricing.rate-list"] = "/api/pricing/rates/realtime-access",
            ["crm.accountlist"] = "/api/crm/accounts/realtime-access",
            ["crm.leadlist"] = "/api/crm/leads/realtime-access",
            ["crm.lead"] = "/api/crm/leads/{id}",
            ["auth.user"] = "/api/auth/users/{id}/realtime-access",
            ["config.catalog-group"] = "/api/config/catalog-groups/{id}",
            ["config.catalog-item"] = "/api/config/catalog-items/{id}",
            ["config.catalog-list"] = "/api/config/catalog-items/realtime-access",
            ["srm.supplier"] = "/api/config/srm/suppliers/{id}/realtime-access",
            ["audit.event"] = "/api/auditlogs/events/{id}",
            ["storage.file"] = "/api/v1/storage/files/{id}",
            ["extraction.execution"] = "/api/data-extraction/realtime-access/execution/{id}",
            ["extraction.emailjob"] = "/api/data-extraction/realtime-access/emailjob/{id}",
            ["extraction.airequest"] = "/api/data-extraction/realtime-access/airequest/{id}",
            ["reports.template"] = "/api/reports/templates/{id}",
            ["dynamics.job"] = "/api/admin/v1/operations/realtime/jobs/{id}",
            ["ai.execution"] = "/api/ai/executions/{id}",
            ["ai.emailjob"] = "/api/ai/realtime-access/emailjob/{id}",
            ["agent.execution"] = "/api/agents/executions/{id}",
            ["agent.definition"] = "/api/agents/definitions/{id}",
            ["agent.schedule"] = "/api/agents/schedules/{id}",
            ["content.item"] = "/api/content/items/{id}",
            ["content.menu"] = "/api/content/realtime-access/menu/{id}",
            ["content.setting"] = "/api/content/realtime-access/setting/{id}"
        };

    public static bool TryCreate(string? kind, string? id, out RealtimeResource resource)
    {
        resource = default;
        if (kind is null || !Guid.TryParse(id, out var guid) || guid == Guid.Empty)
            return false;
        if (kind.StartsWith("module.", StringComparison.Ordinal))
        {
            resource = new RealtimeResource(kind, guid);
            return IsModuleResource(resource);
        }
        if (!Paths.ContainsKey(kind) ||
            (kind == "config.catalog-list" && guid != CatalogListId) ||
            (kind == "pricing.cost-list" && guid != PricingCostListId) ||
            (kind == "pricing.rate-list" && guid != PricingRateListId) ||
            (kind == "crm.accountlist" && guid != CrmAccountListId) ||
            (kind == "crm.leadlist" && guid != CrmLeadListId))
            return false;
        resource = new RealtimeResource(kind, guid);
        return true;
    }

    public static bool TryFromAudience(string? audience, out RealtimeResource resource)
    {
        resource = default;
        if (string.IsNullOrWhiteSpace(audience) || audience.Length > 160) return false;
        var parts = audience.Split(':');
        return parts.Length == 3 &&
            TryCreate(parts[0] + "." + parts[1], parts[2], out resource);
    }

    public static string? PathFor(RealtimeResource resource) =>
        Paths.TryGetValue(resource.Kind, out var path)
            ? path.Replace("{id}", resource.Id.ToString("D"), StringComparison.Ordinal)
            : null;
}

public sealed class NotificationsRealtimeRuntime(IConfiguration configuration)
{
    public bool Enabled =>
        configuration.GetValue<bool>("Features:UniversalRealtimeEnabled") &&
        configuration.GetValue<bool>("Realtime:NotificationsHostEnabled") &&
        string.Equals(configuration["Realtime:ReleaseId"], "rt-20261107-01", StringComparison.Ordinal) &&
        (string.Equals(configuration["Realtime:DeploymentTarget"], "staging", StringComparison.OrdinalIgnoreCase) ||
         (string.Equals(configuration["Realtime:DeploymentTarget"], "production", StringComparison.OrdinalIgnoreCase) &&
          configuration.GetValue<bool>("Realtime:ProductionApproved")));

    public int MaxSubscriptions =>
        Math.Clamp(configuration.GetValue<int?>("Realtime:MaxSubscriptionsPerConnection") ?? 25, 1, 100);
}

public sealed class RealtimeConnectionRegistry
{
    private sealed class Connection(ClaimsPrincipal user, string bearer)
    {
        public ClaimsPrincipal User { get; } = user;
        public string Bearer { get; } = bearer;
        public HashSet<RealtimeResource> Resources { get; } = [];
    }
    private readonly ConcurrentDictionary<string, Connection> _connections = new();

    public void Connect(string id, ClaimsPrincipal user, string bearer) =>
        _connections[id] = new Connection(user, bearer);

    public bool TryGet(string id, out ClaimsPrincipal? user, out string? bearer)
    {
        user = null; bearer = null;
        if (!_connections.TryGetValue(id, out var state)) return false;
        user = state.User; bearer = state.Bearer;
        return true;
    }

    public bool Subscribe(string id, RealtimeResource resource, int limit)
    {
        if (!_connections.TryGetValue(id, out var state)) return false;
        lock (state.Resources)
        {
            if (state.Resources.Contains(resource)) return true;
            if (state.Resources.Count >= limit) return false;
            return state.Resources.Add(resource);
        }
    }

    public void Unsubscribe(string id, RealtimeResource resource)
    {
        if (!_connections.TryGetValue(id, out var state)) return;
        lock (state.Resources) state.Resources.Remove(resource);
    }

    public void Disconnect(string id) => _connections.TryRemove(id, out _);

    public IReadOnlyList<(string ConnectionId, ClaimsPrincipal User, string Bearer)> Subscribers(RealtimeResource resource)
    {
        var result = new List<(string, ClaimsPrincipal, string)>();
        foreach (var (id, state) in _connections)
        {
            lock (state.Resources)
            {
                if (state.Resources.Contains(resource))
                    result.Add((id, state.User, state.Bearer));
            }
        }
        return result;
    }
}

public sealed class OwnerRestRealtimeAuthorizer(
    IHttpClientFactory clients, IConfiguration configuration)
{
    public async Task<bool> CanReadAsync(RealtimeResource resource,
        ClaimsPrincipal? user, string? bearer, CancellationToken cancellationToken)
    {
        if (user?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(bearer))
            return false;
        var path = ResourceRealtimePolicy.PathFor(resource);
        var host = configuration["Realtime:GatewayBaseUrl"];
        if (path is null || !Uri.TryCreate(host, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment))
            return false;

        // Base URL is operator configuration; the only interpolated value is a GUID.
        var target = new Uri(baseUri.GetLeftPart(UriPartial.Authority) + path);
        using var request = new HttpRequestMessage(HttpMethod.Get, target);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var response = await clients.CreateClient("resource-realtime-acl")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false; // 401/403/404, timeouts and unavailable owner all fail closed.
        }
    }
}
