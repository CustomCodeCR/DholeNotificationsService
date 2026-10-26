using System.Security.Cryptography;
using CustomCodeFramework.Redis.Abstractions;
using CustomCodeFramework.Redis.Caching;
using Microsoft.AspNetCore.WebUtilities;

namespace Dhole.Notifications.Api.Hubs;

// Opaque, short-lived, single-use bearer exchange for browser WebSocket upgrades.
// The original (potentially large) JWT never appears in the WebSocket URL.
internal static class HubAccessTicketStore
{
    private const string KeyPrefix = "notifications:signalr:ticket:";
    public const int LifetimeSeconds = 60;

    public static async Task<string> IssueAsync(
        ICacheService cache, string accessToken, CancellationToken cancellationToken)
    {
        var ticket = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        await cache.SetAsync(
            KeyPrefix + ticket,
            accessToken,
            CacheEntryOptions.Default(TimeSpan.FromSeconds(LifetimeSeconds)),
            cancellationToken);
        return ticket;
    }

    public static async Task<string?> RedeemAsync(
        ICacheService cache, string ticket, CancellationToken cancellationToken)
    {
        // 32 random bytes have a 43-character base64url representation.
        if (ticket.Length != 43 || ticket.Any(c =>
            !((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
              (c >= '0' && c <= '9') || c is '-' or '_')))
            return null;

        var key = KeyPrefix + ticket;
        var accessToken = await cache.GetAsync<string>(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken)) return null;

        await cache.RemoveAsync(key, cancellationToken);
        return accessToken;
    }
}
