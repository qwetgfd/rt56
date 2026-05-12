using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sharepoint_Plugin.Interfaces.V_1_0;

namespace Sharepoint_Plugin.Services.V_1_0;

public sealed class AuthService : IAuthService
{
    private static readonly TimeSpan TokenSkew = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, CachedAccessToken> _tokens = new();
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _graphEndpoint;

    public AuthService(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _http = httpClientFactory.CreateClient(nameof(AuthService));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _graphEndpoint = GetEnvironmentValue("sharepointGraphEndpoint", "https://graph.microsoft.com");
        TimeoutSeconds = GetEnvironmentInt("sharepointTimeoutSeconds", 300);
    }

    public string GraphEndpoint => _graphEndpoint;
    public int TimeoutSeconds { get; }

    public bool IsTokenValid
    {
        get
        {
            var tokenRequest = CreateTokenRequest();
            return _tokens.TryGetValue(tokenRequest.CacheKey, out var cachedToken)
                && DateTimeOffset.UtcNow < cachedToken.ExpiresOn - TokenSkew;
        }
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var tokenRequest = CreateTokenRequest();

        if (_tokens.TryGetValue(tokenRequest.CacheKey, out var cachedToken)
            && DateTimeOffset.UtcNow < cachedToken.ExpiresOn - TokenSkew)
            return cachedToken.Token;

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = tokenRequest.ClientId,
            ["client_secret"] = tokenRequest.ClientSecret,
            ["scope"] = tokenRequest.Scope,
            ["grant_type"] = tokenRequest.GrantType
        });

        using var response = await _http.PostAsync(tokenRequest.TokenEndpoint, content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        var accessToken = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Token response did not include access_token.");
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expiresInElement)
            && expiresInElement.TryGetInt32(out var seconds)
                ? seconds
                : 3599;

        _tokens[tokenRequest.CacheKey] = new CachedAccessToken(accessToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
        return accessToken;
    }

    private TokenRequest CreateTokenRequest()
    {
        var tenantId = GetRequiredHeader("tenantId");
        var clientId = GetRequiredHeader("clientId");
        var clientSecret = GetRequiredHeader("clientSecret");
        var scope = GetHeaderOrEnvironment("graphScope", $"{GraphEndpoint.TrimEnd('/')}/.default");
        var grantType = GetHeaderOrEnvironment("grantType", "client_credentials");
        var tokenEndpointTemplate = GetHeaderOrEnvironment("tokenEndpoint", GetEnvironmentValue(
            "sharepointTokenEndpointTemplate",
            "https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token"));
        var tokenEndpoint = tokenEndpointTemplate.Replace("{tenantId}", Uri.EscapeDataString(tenantId), StringComparison.OrdinalIgnoreCase);
        var cacheKey = CreateCacheKey(tenantId, clientId, clientSecret, scope, grantType, tokenEndpoint);

        return new TokenRequest(clientId, clientSecret, scope, grantType, tokenEndpoint, cacheKey);
    }

    private string GetRequiredHeader(string key)
    {
        var headers = _httpContextAccessor.HttpContext?.Request.Headers
            ?? throw new InvalidOperationException("Request headers are not available.");

        var query = _httpContextAccessor.HttpContext?.Request.Query;

        if (headers.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value.ToString();

        return query is not null && query.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : throw new ArgumentException($"{key} header is required.");
    }

    private string GetHeaderOrEnvironment(string key, string defaultValue)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        var headers = request?.Headers;
        var query = request?.Query;

        if (headers is not null && headers.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value.ToString();

        return query is not null && query.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : GetEnvironmentValue(key, defaultValue);
    }

    private static string GetEnvironmentValue(string key, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static int GetEnvironmentInt(string key, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return int.TryParse(value, out var parsedValue) && parsedValue > 0 ? parsedValue : defaultValue;
    }

    private static string CreateCacheKey(params string[] values)
    {
        var rawKey = string.Join('|', values);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(hash);
    }

    private sealed record TokenRequest(string ClientId, string ClientSecret, string Scope, string GrantType, string TokenEndpoint, string CacheKey);
    private sealed record CachedAccessToken(string Token, DateTimeOffset ExpiresOn);
}
