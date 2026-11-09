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
    // Only an owner-controlled REST endpoint can authorize a subscription.
    // Never allow arbitrary caller-supplied groups or paths.
    private static readonly IReadOnlyDictionary<string, string> Paths =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["crm.account"] = "/api/crm/accounts/{id}",
            ["pricing.rate"] = "/api/pricing/rates/{id}",
            ["auth.user"] = "/api/auth/users/{id}/realtime-access",
            ["config.catalog-group"] = "/api/config/catalog-groups/{id}",
            ["config.catalog-item"] = "/api/config/catalog-items/{id}",
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
        if (kind is null || !Paths.ContainsKey(kind) ||
            !Guid.TryParse(id, out var guid) || guid == Guid.Empty)
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
