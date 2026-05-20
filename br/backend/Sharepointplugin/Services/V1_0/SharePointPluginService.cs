using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using NLog;
using Sharepoint_Plugin.Constants;
using static Sharepoint_Plugin.Constants.Constants;
using Sharepoint_Plugin.Interfaces.V1_0;
using Sharepoint_Plugin.Models.Request;
using Sharepoint_Plugin.Models.Response;
using Sharepoint_Plugin.Utilities;

namespace Sharepoint_Plugin.Services.V1_0;

public class SharePointPluginService : ISharePointPluginService
{
    private const string DefaultGraphEndpoint = "https://graph.microsoft.com";
    private const string DefaultGraphApiVersion = "v1.0";
    private const string DefaultTokenEndpointTemplate = "https://login.microsoftonline.com/{0}/oauth2/v2.0/token";
    private const string DefaultScope = "https://graph.microsoft.com/.default";
    private const string DefaultGrantType = "client_credentials";
    private const string DefaultLibraryNameValue = "Documents";
    private const int TokenExpiryBufferMinutes = 5;

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ISharePointPluginRepository _sharePointRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _graphEndpoint;
    private readonly string _graphApiVersion;
    private readonly string _tokenEndpointTemplate;
    private readonly string _scope;
    private readonly string _grantType;
    private readonly string _defaultLibraryName;
    private readonly ConcurrentDictionary<string, CachedToken> _tokenCache = new();

    public SharePointPluginService(
        ISharePointPluginRepository sharePointRepository,
        IHttpClientFactory httpClientFactory)
    {
        _sharePointRepository = sharePointRepository ?? throw new ArgumentNullException(nameof(sharePointRepository));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

        _graphEndpoint = Environment.GetEnvironmentVariable("sharepointgraphendpoint")?.TrimEnd('/') ?? DefaultGraphEndpoint;
        _graphApiVersion = Environment.GetEnvironmentVariable("sharepointgraphapiversion")?.Trim('/') ?? DefaultGraphApiVersion;
        _tokenEndpointTemplate = Environment.GetEnvironmentVariable("sharepointgraphtokenendpointtemplate") ?? DefaultTokenEndpointTemplate;
        _scope = Environment.GetEnvironmentVariable("sharepointgraphscope") ?? DefaultScope;
        _grantType = Environment.GetEnvironmentVariable("sharepointgraphgranttype") ?? DefaultGrantType;
        _defaultLibraryName = Environment.GetEnvironmentVariable("sharepointgraphdefaultlibraryname") ?? DefaultLibraryNameValue;
    }

    public Task<IReadOnlyList<Application.ApplicationType>> GetApplicationTypesAsync()
        => _sharePointRepository.GetApplicationTypesAsync();

    public async Task<IReadOnlyList<Application>> GetApplicationsAsync(string? ownerKey, string? typeCode)
    {
        int? typeId = null;
        if (!string.IsNullOrWhiteSpace(typeCode))
        {
            var types = await _sharePointRepository.GetApplicationTypesAsync().ConfigureAwait(false);
            typeId = types.FirstOrDefault(t =>
                string.Equals(t.Code, typeCode, StringComparison.OrdinalIgnoreCase))?.ApplicationTypeId;
        }
        return await _sharePointRepository.GetApplicationsAsync(ownerKey, typeId).ConfigureAwait(false);
    }

    public Task<Application?> GetApplicationByIdAsync(Guid applicationId)
        => _sharePointRepository.GetApplicationByIdAsync(applicationId);

    public async Task<Application> SaveApplicationAsync(Application application)
    {
        var types = await _sharePointRepository.GetApplicationTypesAsync().ConfigureAwait(false);
        var type = types.FirstOrDefault(t =>
            string.Equals(t.Code, application.ApplicationTypeCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException(string.Format(ApplicationTypeNotRecognised, application.ApplicationTypeCode));

        application.ApplicationTypeId = type.ApplicationTypeId;

        if (application.ApplicationId == Guid.Empty)
        {
            application.ConsumerClientId = Guid.NewGuid().ToString("D");
            application.ConsumerSecret = GenerateConsumerApiSecret();
        }
        else
        {
            var existing = await _sharePointRepository.GetApplicationByIdAsync(application.ApplicationId).ConfigureAwait(false);
            if (existing != null)
            {
                if (string.IsNullOrWhiteSpace(application.ConsumerClientId))
                    application.ConsumerClientId = existing.ConsumerClientId;
                if (string.IsNullOrWhiteSpace(application.ConsumerSecret))
                    application.ConsumerSecret = existing.ConsumerSecret;
            }
        }

        return await _sharePointRepository.SaveApplicationAsync(application).ConfigureAwait(false);
    }

    /// <summary>Random secret for Streaming API consumers (not the Microsoft Entra client secret).</summary>
    private static string GenerateConsumerApiSecret()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public Task<bool> DeleteApplicationAsync(Guid applicationId)
        => _sharePointRepository.DeleteApplicationAsync(applicationId);

    public async Task<TokenResponse> GenerateTokenAsync(TokenRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ApplicationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ClientSecret);

        if (!Guid.TryParse(request.ApplicationId, out var applicationId))
            throw new UnauthorizedAccessException(InvalidClientCredentials);

        var app = await _sharePointRepository.GetApplicationByIdAsync(applicationId).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException(ApplicationNotFound);

        if (string.IsNullOrWhiteSpace(app.ConsumerSecret))
            throw new UnauthorizedAccessException(InvalidClientCredentials);

        if (!string.Equals(app.ConsumerSecret, request.ClientSecret, StringComparison.Ordinal))
            throw new UnauthorizedAccessException(InvalidClientCredentials);

        var signingKey = Environment.GetEnvironmentVariable("authsigningkey")
            ?? throw new InvalidOperationException(SigningKeyNotConfigured);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var lifetimeMinutes = 60;
        var lifetimeEnv = Environment.GetEnvironmentVariable("authtokenlifetimeminutes");
        if (!string.IsNullOrWhiteSpace(lifetimeEnv) && int.TryParse(lifetimeEnv, out var parsed))
            lifetimeMinutes = parsed;

        var expires = DateTime.UtcNow.AddMinutes(lifetimeMinutes);

        var token = new JwtSecurityToken(
            issuer: Environment.GetEnvironmentVariable("authissuer") ?? "SharepointApi",
            audience: Environment.GetEnvironmentVariable("authaudience") ?? "SharepointApi.Consumers",
            claims:
            [
                new Claim("application_id", app.ApplicationId.ToString()),
                new Claim("sub", app.ApplicationId.ToString()),
            ],
            expires: expires,
            signingCredentials: credentials);

        return new TokenResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            TokenType = "Bearer",
            ExpiresInSeconds = (int)(expires - DateTime.UtcNow).TotalSeconds
        };
    }

    public async Task<Application?> ResolveTokenAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        var signingKey = Environment.GetEnvironmentVariable("authsigningkey");
        if (string.IsNullOrWhiteSpace(signingKey))
            return null;

        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            var tokenHandler = new JwtSecurityTokenHandler();

            tokenHandler.ValidateToken(accessToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = Environment.GetEnvironmentVariable("authissuer") ?? "SharepointApi",
                ValidateAudience = true,
                ValidAudience = Environment.GetEnvironmentVariable("authaudience") ?? "SharepointApi.Consumers",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out var validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var applicationIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "application_id")?.Value;

            if (!Guid.TryParse(applicationIdClaim, out var applicationId))
                return null;

            return await _sharePointRepository.GetApplicationByIdAsync(applicationId).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<SharePointLibrary>> ListLibrariesAsync(WorkspaceCredentials credentials)
    {
        var resolved = await ResolveCredentialsAsync(credentials).ConfigureAwait(false);
        var (siteId, _) = await ResolveSiteAndDriveAsync(resolved, null).ConfigureAwait(false);
        return await ListDrivesAsync(resolved, siteId).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SharePointItem>> BrowseFolderAsync(WorkspaceCredentials credentials, string? folderPath)
    {
        var resolved = await ResolveCredentialsAsync(credentials).ConfigureAwait(false);
        var (siteId, driveId) = await ResolveSiteAndDriveAsync(resolved, null).ConfigureAwait(false);
        return await ListChildrenAsync(resolved, siteId, driveId, folderPath).ConfigureAwait(false);
    }

    public async Task<FileStreamContent> FetchFileAsync(WorkspaceCredentials credentials, string? filePath, string? rangeHeader)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var resolved = await ResolveCredentialsAsync(credentials).ConfigureAwait(false);
        var (siteId, driveId) = await ResolveSiteAndDriveAsync(resolved, null).ConfigureAwait(false);
        return await GetFileContentAsync(resolved, siteId, driveId, filePath, rangeHeader).ConfigureAwait(false);
    }

    public async Task<Application> ResolveCredentialsAsync(WorkspaceCredentials credentials)
    {
        if (credentials.ApplicationId.HasValue && credentials.ApplicationId.Value != Guid.Empty)
        {
            var app = await _sharePointRepository.GetApplicationByIdAsync(credentials.ApplicationId.Value).ConfigureAwait(false)
                ?? throw new KeyNotFoundException(ApplicationNotFound);

            return new Application
            {
                TenantId = credentials.TenantId ?? app.TenantId,
                ClientId = credentials.ClientId ?? app.ClientId,
                ClientSecret = credentials.ClientSecret ?? app.ClientSecret,
                HostName = credentials.HostName ?? app.HostName,
                SiteName = credentials.SiteName ?? app.SiteName,
                LibraryName = credentials.LibraryName ?? app.LibraryName
            };
        }

        if (string.IsNullOrWhiteSpace(credentials.TenantId)
            || string.IsNullOrWhiteSpace(credentials.ClientId)
            || string.IsNullOrWhiteSpace(credentials.ClientSecret)
            || string.IsNullOrWhiteSpace(credentials.HostName))
        {
            throw new ArgumentException(CredentialsRequired);
        }

        return new Application
        {
            TenantId = credentials.TenantId.Trim(),
            ClientId = credentials.ClientId.Trim(),
            ClientSecret = credentials.ClientSecret.Trim(),
            HostName = credentials.HostName.Trim(),
            SiteName = credentials.SiteName?.Trim(),
            LibraryName = credentials.LibraryName?.Trim() ?? _defaultLibraryName
        };
    }

    public async Task<(string SiteId, string DriveId)> ResolveSiteAndDriveAsync(Application credentials, string? driveName)
    {
        var sitePath = NormalizeSitePath(credentials.SiteName);
        var siteUrl = BuildGraphUrl($"sites/{credentials.HostName}:/{EncodePath(sitePath)}");
        using var siteDoc = await SendGraphGetAsync(siteUrl, credentials).ConfigureAwait(false);
        var siteId = CommonUtilities.JsonGetString(siteDoc.RootElement, "id");

        if (string.IsNullOrWhiteSpace(siteId))
        {
            Logger.Error("Graph API did not return a site id for host: {0}, site: {1}", credentials.HostName, sitePath);
            throw new InvalidOperationException(SiteIdMissing);
        }

        var driveId = await ResolveDriveIdAsync(credentials, siteId, driveName ?? credentials.LibraryName).ConfigureAwait(false);
        return (siteId, driveId);
    }

    public async Task<string> ResolveDriveIdAsync(Application credentials, string siteId, string? libraryName)
    {
        var drives = await ListDrivesAsync(credentials, siteId).ConfigureAwait(false);

        if (drives.Count == 0)
            throw new InvalidOperationException(NoLibrariesAvailable);

        if (string.IsNullOrWhiteSpace(libraryName))
            return drives[0].Id;

        var match = CommonUtilities.FindDrive(drives, libraryName);
        if (match is not null)
            return match.Id;

        var available = string.Join(", ", drives.Select(d => $"'{d.Name}'"));
        throw new ArgumentException(string.Format(LibraryNotFound, libraryName, available));
    }

    public async Task<IReadOnlyList<SharePointLibrary>> ListDrivesAsync(Application credentials, string siteId)
    {
        var url = BuildGraphUrl($"sites/{siteId}/drives");
        using var doc = await SendGraphGetAsync(url, credentials).ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty("value", out var values))
            return Array.Empty<SharePointLibrary>();

        return values.EnumerateArray().Select(e => new SharePointLibrary
        {
            Id = CommonUtilities.JsonGetString(e, "id"),
            Name = CommonUtilities.JsonGetString(e, "name"),
            Description = CommonUtilities.JsonGetStringOrNull(e, "description"),
            WebUrl = CommonUtilities.JsonGetStringOrNull(e, "webUrl")
        }).ToList();
    }

    public async Task<IReadOnlyList<SharePointItem>> ListChildrenAsync(Application credentials, string siteId, string driveId, string? folderPath)
    {
        var url = string.IsNullOrWhiteSpace(folderPath) || folderPath == "/"
            ? BuildGraphUrl($"sites/{siteId}/drives/{driveId}/root/children")
            : BuildGraphUrl($"sites/{siteId}/drives/{driveId}/root:/{EncodePath(folderPath)}:/children");

        using var doc = await SendGraphGetAsync(url, credentials).ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty("value", out var values))
            return Array.Empty<SharePointItem>();

        return values.EnumerateArray().Select(e => MapSharePointItem(e, folderPath)).ToList();
    }

    public async Task<FileStreamContent> GetFileContentAsync(Application credentials, string siteId, string driveId, string filePath, string? rangeHeader)
    {
        var url = BuildGraphUrl($"sites/{siteId}/drives/{driveId}/root:/{EncodePath(filePath)}:/content");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(credentials).ConfigureAwait(false));

        if (!string.IsNullOrWhiteSpace(rangeHeader))
            request.Headers.TryAddWithoutValidation("Range", rangeHeader);

        var response = await _httpClientFactory.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return new FileStreamContent
        {
            Content = await response.Content.ReadAsStreamAsync().ConfigureAwait(false),
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
            ContentLength = response.Content.Headers.ContentLength,
            StatusCode = (int)response.StatusCode,
            ContentRange = response.Content.Headers.ContentRange?.ToString(),
            AcceptRanges = response.Headers.AcceptRanges.Count > 0 ? string.Join(", ", response.Headers.AcceptRanges) : "bytes",
            FileName = ExtractFileName(filePath)
        };
    }

    public async Task<string> GetAccessTokenAsync(Application credentials)
    {
        var key = $"{credentials.TenantId}|{credentials.ClientId}|{credentials.ClientSecret}";

        if (_tokenCache.TryGetValue(key, out var cached)
            && DateTimeOffset.UtcNow < cached.ExpiresOn - TimeSpan.FromMinutes(TokenExpiryBufferMinutes))
        {
            return cached.Token;
        }

        var tokenEndpoint = string.Format(_tokenEndpointTemplate, Uri.EscapeDataString(credentials.TenantId));
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = credentials.ClientId,
            ["client_secret"] = credentials.ClientSecret,
            ["scope"] = _scope,
            ["grant_type"] = _grantType
        });

        using var response = await _httpClientFactory.CreateClient().PostAsync(tokenEndpoint, content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        var token = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Token response missing access_token.");

        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ei) && ei.TryGetInt32(out var s) ? s : 3599;

        _tokenCache[key] = new CachedToken(token, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
        return token;
    }

    public async Task<JsonDocument> SendGraphGetAsync(string url, Application credentials)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(credentials).ConfigureAwait(false));
        using var response = await _httpClientFactory.CreateClient().SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
    }

    private string BuildGraphUrl(string path) => $"{_graphEndpoint}/{_graphApiVersion}/{path}";

    private static string NormalizeSitePath(string? siteName)
    {
        if (string.IsNullOrWhiteSpace(siteName)) return string.Empty;
        var trimmed = siteName.Trim().Trim('/');
        return trimmed.StartsWith("sites/", StringComparison.OrdinalIgnoreCase) ? trimmed : $"sites/{trimmed}";
    }

    private static string EncodePath(string path)
    {
        path = path.TrimStart('/').Replace("\\", "/", StringComparison.Ordinal);
        return string.IsNullOrEmpty(path)
            ? path
            : string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }

    private static string ExtractFileName(string filePath)
    {
        var slash = filePath.LastIndexOf('/');
        return slash >= 0 && slash + 1 < filePath.Length ? filePath[(slash + 1)..] : filePath;
    }

    private static SharePointItem MapSharePointItem(JsonElement element, string? parentPath)
    {
        var name = CommonUtilities.JsonGetString(element, "name");
        return new SharePointItem
        {
            Id = CommonUtilities.JsonGetString(element, "id"),
            Name = name,
            IsFolder = CommonUtilities.JsonHasProperty(element, "folder"),
            Size = CommonUtilities.JsonGetInt64(element, "size"),
            MimeType = element.TryGetProperty("file", out var file) ? CommonUtilities.JsonGetStringOrNull(file, "mimeType") : null,
            LastModifiedDateTime = CommonUtilities.JsonGetDateTimeOffsetOrNull(element, "lastModifiedDateTime"),
            WebUrl = CommonUtilities.JsonGetStringOrNull(element, "webUrl"),
            Path = string.IsNullOrWhiteSpace(parentPath) ? name : $"{parentPath.TrimEnd('/')}/{name}",
            ChildCount = element.TryGetProperty("folder", out var folder) ? CommonUtilities.JsonGetInt32(folder, "childCount") : null
        };
    }

    private sealed record CachedToken(string Token, DateTimeOffset ExpiresOn);
}
