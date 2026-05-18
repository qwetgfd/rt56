using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sharepoint_Plugin.Interfaces.V_1_0;

namespace Sharepoint_Plugin.Services.V_1_0;

/// <summary>
/// Manages OAuth2 client_credentials token acquisition and caching for Microsoft Graph.
/// Tokens are cached with a 5-minute skew before expiry.
/// </summary>
public class AuthService : IAuthService
{
    private static readonly TimeSpan TokenSkew = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, CachedToken> _tokens = new();
    private readonly HttpClient _http;
    private readonly string _graphEndpoint;

    public AuthService()
    {
        _http = new HttpClient();
        _graphEndpoint = Environment.GetEnvironmentVariable("sharepointGraphEndpoint") ?? "https://graph.microsoft.com";
        var timeoutStr = Environment.GetEnvironmentVariable("sharepointTimeoutSeconds");
        TimeoutSeconds = int.TryParse(timeoutStr, out var t) && t > 0 ? t : 300;
    }

    public string GraphEndpoint => _graphEndpoint;
    public int TimeoutSeconds { get; }

    public bool IsTokenValid
    {
        get
        {
            var key = BuildCacheKey();
            return _tokens.TryGetValue(key, out var cached) && DateTimeOffset.UtcNow < cached.ExpiresOn - TokenSkew;
        }
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var key = BuildCacheKey();

        if (_tokens.TryGetValue(key, out var cached) && DateTimeOffset.UtcNow < cached.ExpiresOn - TokenSkew)
            return cached.Token;

        var tenantId = Environment.GetEnvironmentVariable("tenantId") ?? throw new ArgumentException("tenantId env var is required.");
        var clientId = Environment.GetEnvironmentVariable("clientId") ?? throw new ArgumentException("clientId env var is required.");
        var clientSecret = Environment.GetEnvironmentVariable("clientSecret") ?? throw new ArgumentException("clientSecret env var is required.");
        var scope = Environment.GetEnvironmentVariable("graphScope") ?? $"{GraphEndpoint.TrimEnd('/')}/.default";
        var grantType = Environment.GetEnvironmentVariable("grantType") ?? "client_credentials";
        var tokenEndpoint = (Environment.GetEnvironmentVariable("tokenEndpoint")
            ?? Environment.GetEnvironmentVariable("sharepointTokenEndpointTemplate")
            ?? "https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token")
            .Replace("{tenantId}", Uri.EscapeDataString(tenantId), StringComparison.OrdinalIgnoreCase);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = scope,
            ["grant_type"] = grantType
        });

        using var response = await _http.PostAsync(tokenEndpoint, content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        var token = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Token response missing access_token.");
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ei) && ei.TryGetInt32(out var s) ? s : 3599;

        _tokens[key] = new CachedToken(token, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
        return token;
    }

    private static string BuildCacheKey()
    {
        var tenantId = Environment.GetEnvironmentVariable("tenantId") ?? "";
        var clientId = Environment.GetEnvironmentVariable("clientId") ?? "";
        var clientSecret = Environment.GetEnvironmentVariable("clientSecret") ?? "";
        var scope = Environment.GetEnvironmentVariable("graphScope") ?? "";
        var grantType = Environment.GetEnvironmentVariable("grantType") ?? "";
        var tokenEndpoint = Environment.GetEnvironmentVariable("tokenEndpoint")
            ?? Environment.GetEnvironmentVariable("sharepointTokenEndpointTemplate") ?? "";

        var raw = string.Join('|', tenantId, clientId, clientSecret, scope, grantType, tokenEndpoint);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private record CachedToken(string Token, DateTimeOffset ExpiresOn);
}
