using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Teleperformance.DataIngestion.Sharepoint.Constants;
using static Teleperformance.DataIngestion.Sharepoint.Constants.Constants;
using Teleperformance.DataIngestion.Sharepoint.Interfaces.V1_0;
using Teleperformance.DataIngestion.Sharepoint.Models.Request;
using Teleperformance.DataIngestion.Sharepoint.Models.Response;
using Teleperformance.DataIngestion.Sharepoint.Utilities;

namespace Teleperformance.DataIngestion.Sharepoint.Services.V1_0;

public class SharePointPluginService : ISharePointPluginService
{
    private const string DefaultTokenEndpointTemplate = "https://login.microsoftonline.com/{0}/oauth2/v2.0/token";
    private const string DefaultScope = "https://graph.microsoft.com/.default";
    private const string DefaultGrantType = "client_credentials";
    private const string DefaultLibraryNameValue = "Documents";
    private const int TokenExpiryBufferMinutes = 5;

    private readonly ILogger<SharePointPluginService> _logger;
    private readonly ISharePointPluginRepository _sharePointRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GraphApiConfiguration _graph;
    private readonly string _tokenEndpointTemplate;
    private readonly string _scope;
    private readonly string _grantType;
    private readonly string _defaultLibraryName;
    private readonly ConcurrentDictionary<string, CachedToken> _tokenCache = new();

    public SharePointPluginService(
        ISharePointPluginRepository sharePointRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<SharePointPluginService> logger)
    {
        _sharePointRepository = sharePointRepository ?? throw new ArgumentNullException(nameof(sharePointRepository));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _graph = new GraphApiConfiguration();
        _tokenEndpointTemplate = Environment.GetEnvironmentVariable("sharepointgraphtokenendpointtemplate") ?? DefaultTokenEndpointTemplate;
        _scope = Environment.GetEnvironmentVariable("sharepointgraphscope") ?? DefaultScope;
        _grantType = Environment.GetEnvironmentVariable("sharepointgraphgranttype") ?? DefaultGrantType;
        _defaultLibraryName = Environment.GetEnvironmentVariable("sharepointgraphdefaultlibraryname") ?? DefaultLibraryNameValue;
    }

    private static readonly HashSet<string> AllowedApplicationTypeCodes =
        new(StringComparer.OrdinalIgnoreCase) { "tp_internal", "tp_external", "tp_user_delegated" };

    private static bool IsSiteOnlyType(string code) =>
        string.Equals(code, "tp_internal", StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, "tp_user_delegated", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<Application.ApplicationType>> GetApplicationTypesAsync()
    {
        var types = await _sharePointRepository.GetApplicationTypesAsync().ConfigureAwait(false);
        return types.Where(t => AllowedApplicationTypeCodes.Contains(t.Code)).ToList();
    }

    public async Task<IReadOnlyList<Application>> GetApplicationsAsync(string? ownerKey, string? typeCode)
    {
        int? typeId = null;
        if (!string.IsNullOrWhiteSpace(typeCode))
        {
            var types = await _sharePointRepository.GetApplicationTypesAsync().ConfigureAwait(false);
            typeId = types.FirstOrDefault(t =>
                string.Equals(t.Code, typeCode, StringComparison.OrdinalIgnoreCase))?.ApplicationTypeId;
        }
        var apps = await _sharePointRepository.GetApplicationsAsync(ownerKey, typeId).ConfigureAwait(false);
        await AttachApplicationSitesAsync(apps).ConfigureAwait(false);
        return apps;
    }

    public async Task<Application?> GetApplicationByIdAsync(Guid applicationId)
    {
        var app = await _sharePointRepository.GetApplicationByIdAsync(applicationId).ConfigureAwait(false);
        if (app is null) return null;
        await AttachApplicationSitesAsync([app]).ConfigureAwait(false);
        return app;
    }

    public async Task<Application> SaveApplicationAsync(Application application)
    {
        if (string.IsNullOrWhiteSpace(application.Owner))
            throw new ArgumentException(ApplicationOwnerRequired);

        application.Owner = application.Owner.Trim();
        application.OwnerUpn = string.IsNullOrWhiteSpace(application.OwnerUpn) ? null : application.OwnerUpn.Trim();
        application.CoOwner = string.IsNullOrWhiteSpace(application.CoOwner) ? null : application.CoOwner.Trim();
        application.CoOwnerUpn = string.IsNullOrWhiteSpace(application.CoOwnerUpn) ? null : application.CoOwnerUpn.Trim();

        var types = await _sharePointRepository.GetApplicationTypesAsync().ConfigureAwait(false);
        if (!AllowedApplicationTypeCodes.Contains(application.ApplicationTypeCode))
            throw new ArgumentException(string.Format(ApplicationTypeNotRecognised, application.ApplicationTypeCode));

        var type = types.FirstOrDefault(t =>
            string.Equals(t.Code, application.ApplicationTypeCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException(string.Format(ApplicationTypeNotRecognised, application.ApplicationTypeCode));

        application.ApplicationTypeId = type.ApplicationTypeId;

        if (IsSiteOnlyType(application.ApplicationTypeCode))
        {
            application.TenantId = string.Empty;
            application.ClientId = string.Empty;
            application.ClientSecret = string.Empty;
        }

        application.SourceInternalApplicationId = null;
        NormalizeApplicationSites(application);
        SyncPrimarySiteFromSites(application);

        if (string.Equals(application.ApplicationTypeCode, "tp_internal", StringComparison.OrdinalIgnoreCase))
            await ValidateInternalSiteRegistrationAsync(application).ConfigureAwait(false);

        if (application.ApplicationId == Guid.Empty)
        {
            application.ConsumerClientId = Guid.NewGuid().ToString("D");
            application.ConsumerSecret = GenerateConsumerApiSecret();
        }
        else
        {
            var existing = await _sharePointRepository.GetApplicationByIdAsync(application.ApplicationId).ConfigureAwait(false);
            if (existing is not null)
            {
                if (string.IsNullOrWhiteSpace(application.ConsumerClientId))
                    application.ConsumerClientId = existing.ConsumerClientId;
                if (string.IsNullOrWhiteSpace(application.ConsumerSecret))
                    application.ConsumerSecret = existing.ConsumerSecret;
            }
        }

        var saved = await _sharePointRepository.SaveApplicationAsync(application).ConfigureAwait(false);
        if (application.Sites.Count > 0)
        {
            saved.Sites = (await _sharePointRepository
                .SaveApplicationSitesAsync(saved.ApplicationId, application.Sites)
                .ConfigureAwait(false)).ToList();
            SyncPrimarySiteFromSites(saved);
        }
        else
        {
            await AttachApplicationSitesAsync([saved]).ConfigureAwait(false);
        }

        return saved;
    }

    private static void NormalizeApplicationSites(Application application)
    {
        if (application.Sites.Count > 0) return;
        var siteName = application.SiteName?.Trim();
        if (string.IsNullOrWhiteSpace(siteName)) return;

        application.Sites.Add(new ApplicationSite
        {
            HostName = application.HostName,
            SiteName = siteName,
            LibraryName = application.LibraryName,
            SortOrder = 0
        });
    }

    private static void SyncPrimarySiteFromSites(Application application)
    {
        if (application.Sites.Count == 0) return;
        var primary = application.Sites.OrderBy(s => s.SortOrder).First();
        if (!string.IsNullOrWhiteSpace(primary.HostName))
            application.HostName = primary.HostName.Trim();
        application.SiteName = primary.SiteName.Trim();
        application.LibraryName = string.IsNullOrWhiteSpace(primary.LibraryName)
            ? application.LibraryName
            : primary.LibraryName.Trim();
    }

    private async Task AttachApplicationSitesAsync(IReadOnlyList<Application> applications)
    {
        if (applications.Count == 0) return;
        var allSites = await _sharePointRepository.GetApplicationSitesAsync().ConfigureAwait(false);
        var grouped = allSites
            .GroupBy(s => s.ApplicationId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SortOrder).ToList());

        foreach (var app in applications)
        {
            if (grouped.TryGetValue(app.ApplicationId, out var sites))
            {
                app.Sites = sites;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(app.SiteName))
            {
                app.Sites = new List<ApplicationSite>
                {
                    new()
                    {
                        ApplicationId = app.ApplicationId,
                        HostName = app.HostName,
                        SiteName = app.SiteName!,
                        LibraryName = app.LibraryName,
                        SortOrder = 0
                    }
                };
            }
        }
    }

    public async Task<ApplicationUsageEntry> RecordApplicationUsageAsync(Guid applicationId, RecordApplicationUsageRequest request)
    {
        var app = await _sharePointRepository.GetApplicationByIdAsync(applicationId).ConfigureAwait(false)
            ?? throw new KeyNotFoundException(ApplicationNotFound);

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? app.DisplayName
            : request.DisplayName.Trim();

        var usedByUpn = string.IsNullOrWhiteSpace(request.UsedByUpn) ? null : request.UsedByUpn.Trim();
        var usedByDisplayName = string.IsNullOrWhiteSpace(request.UsedByDisplayName)
            ? null
            : request.UsedByDisplayName.Trim();

        return new ApplicationUsageEntry
        {
            ApplicationId = applicationId,
            DisplayName = displayName,
            UsedByUpn = usedByUpn,
            UsedByDisplayName = usedByDisplayName,
            UsedOn = DateTimeOffset.UtcNow,
        };
    }

    public async Task<ExternalSiteConnectivityResult> ValidateExternalSiteConnectivityAsync(ExternalSiteConnectivityRequest request)
    {
        var trimmedSite = request.SiteName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedSite))
            throw new ArgumentException(ExternalSiteNameRequired);

        if (UsesProvidedEntraCredentials(request))
        {
            var host = request.HostName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host name is required when verifying connectivity with application credentials.");

            var credentials = new Application
            {
                TenantId = request.TenantId!.Trim(),
                ClientId = request.ClientId!.Trim(),
                ClientSecret = request.ClientSecret!.Trim(),
                HostName = host,
                SiteName = trimmedSite,
                LibraryName = _defaultLibraryName
            };
            return await ProbeSiteConnectivityAsync(credentials, trimmedSite).ConfigureAwait(false);
        }

        var internalApp = await ResolveInternalConnectivityApplicationAsync(request.InternalApplicationId).ConfigureAwait(false);
        return await ProbeSiteConnectivityAsync(internalApp, trimmedSite, request.HostName).ConfigureAwait(false);
    }

    private Application GetEnvironmentInternalApplication()
    {
        var tenantId = Environment.GetEnvironmentVariable("sharepointinternaltenantid")?.Trim() ?? string.Empty;
        var clientId = Environment.GetEnvironmentVariable("sharepointinternalclientid")?.Trim() ?? string.Empty;
        var clientSecret = Environment.GetEnvironmentVariable("sharepointinternalclientsecret")?.Trim() ?? string.Empty;
        var hostName = Environment.GetEnvironmentVariable("sharepointinternalhostname")?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(tenantId)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret)
            || string.IsNullOrWhiteSpace(hostName))
        {
            throw new InvalidOperationException(InternalConnectivityApplicationNotConfigured);
        }

        return new Application
        {
            TenantId = tenantId,
            ClientId = clientId,
            ClientSecret = clientSecret,
            HostName = hostName,
            LibraryName = _defaultLibraryName
        };
    }

    private void ApplyInternalCredentialsIfNeeded(Application application)
    {
        if (!string.Equals(application.ApplicationTypeCode, "tp_internal", StringComparison.OrdinalIgnoreCase))
            return;

        var internalApp = GetEnvironmentInternalApplication();
        application.TenantId = internalApp.TenantId;
        application.ClientId = internalApp.ClientId;
        application.ClientSecret = internalApp.ClientSecret;
        if (string.IsNullOrWhiteSpace(application.HostName))
            application.HostName = internalApp.HostName;
    }

    private async Task ValidateInternalSiteRegistrationAsync(Application application)
    {
        NormalizeApplicationSites(application);
        var primary = application.Sites.OrderBy(s => s.SortOrder).FirstOrDefault();
        var siteName = primary?.SiteName?.Trim() ?? application.SiteName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(siteName))
            throw new ArgumentException(ExternalSiteNameRequired);

        var hostName = primary?.HostName?.Trim() ?? application.HostName?.Trim();
        var internalApp = GetEnvironmentInternalApplication();
        var probe = await ProbeSiteConnectivityAsync(internalApp, siteName, hostName).ConfigureAwait(false);
        if (!probe.IsConnected)
            throw new InvalidOperationException(probe.Message);

        application.HostName = probe.HostName;
        application.SiteName = siteName;
        application.LibraryName = string.IsNullOrWhiteSpace(primary?.LibraryName ?? application.LibraryName)
            ? _defaultLibraryName
            : (primary?.LibraryName ?? application.LibraryName)!.Trim();

        if (string.IsNullOrWhiteSpace(application.DisplayName))
            application.DisplayName = siteName;
    }

    private static bool UsesProvidedEntraCredentials(ExternalSiteConnectivityRequest request) =>
        !string.IsNullOrWhiteSpace(request.TenantId)
        && !string.IsNullOrWhiteSpace(request.ClientId)
        && !string.IsNullOrWhiteSpace(request.ClientSecret);

    private async Task<Application> ResolveInternalConnectivityApplicationAsync(Guid? sourceInternalApplicationId)
    {
        _ = sourceInternalApplicationId;
        return GetEnvironmentInternalApplication();
    }

    private async Task<ExternalSiteConnectivityResult> ProbeSiteConnectivityAsync(Application sourceApp, string siteName, string? hostName = null)
    {
        var probeHost = string.IsNullOrWhiteSpace(hostName) ? sourceApp.HostName : hostName.Trim();
        var credentials = new Application
        {
            TenantId = sourceApp.TenantId,
            ClientId = sourceApp.ClientId,
            ClientSecret = sourceApp.ClientSecret,
            HostName = probeHost,
            SiteName = siteName,
            LibraryName = _defaultLibraryName
        };
        return await ProbeSiteConnectivityAsync(credentials, siteName).ConfigureAwait(false);
    }

    private async Task<ExternalSiteConnectivityResult> ProbeSiteConnectivityAsync(Application credentials, string siteName)
    {
        var probeHost = credentials.HostName;
        try
        {
            var sitePath = CommonUtilities.NormalizeSharePointSitePath(siteName);
            var siteUrl = _graph.BuildUrl($"sites/{credentials.HostName}:/{CommonUtilities.EncodeGraphPath(sitePath)}");
            using var siteDoc = await SendGraphGetAsync(siteUrl, credentials).ConfigureAwait(false);
            var siteId = CommonUtilities.JsonGetString(siteDoc.RootElement, "id");
            if (string.IsNullOrWhiteSpace(siteId))
            {
                return BuildFailedConnectivityResult(siteName, probeHost,
                    string.Format(ExternalSiteNotAccessible, siteName));
            }

            var siteTitle = CommonUtilities.JsonGetStringOrNull(siteDoc.RootElement, "displayName")
                ?? CommonUtilities.JsonGetStringOrNull(siteDoc.RootElement, "name");
            var libraries = await ListDrivesAsync(credentials, siteId).ConfigureAwait(false);

            return new ExternalSiteConnectivityResult
            {
                IsConnected = true,
                RequiresAccessRequest = false,
                SiteName = siteName,
                HostName = probeHost,
                SiteTitle = siteTitle,
                LibraryCount = libraries.Count,
                Libraries = libraries,
                Message = ExternalSiteConnectivityVerified
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SharePoint site connectivity check failed for site {SiteName} on host {HostName}", siteName, probeHost);
            return BuildFailedConnectivityResult(siteName, probeHost,
                string.Format(ExternalSiteNotAccessible, siteName));
        }
    }

    private static ExternalSiteConnectivityResult BuildFailedConnectivityResult(string siteName, string hostName, string message) =>
        new()
        {
            IsConnected = false,
            RequiresAccessRequest = true,
            SiteName = siteName,
            HostName = hostName,
            LibraryCount = 0,
            Message = message
        };

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

        var (token, expires) = AuthConfiguration.CreateJwt(
        [
            new Claim("application_id", app.ApplicationId.ToString()),
            new Claim("sub", app.ApplicationId.ToString()),
        ]);

        return new TokenResponse
        {
            AccessToken = AuthConfiguration.WriteToken(token),
            TokenType = "Bearer",
            ExpiresInSeconds = AuthConfiguration.ExpiresInSeconds(expires)
        };
    }

    public async Task<Application?> ResolveTokenAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        if (string.IsNullOrWhiteSpace(AuthConfiguration.SigningKey))
            return null;

        try
        {
            var key = AuthConfiguration.RequireSigningKey();
            var tokenHandler = new JwtSecurityTokenHandler();

            tokenHandler.ValidateToken(
                accessToken,
                AuthConfiguration.CreateValidationParameters(key),
                out var validatedToken);

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

    public async Task<bool> MoveFileAsync(WorkspaceCredentials credentials, string sourceFilePath, string destinationFolderPath, string newFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(newFileName);

        var resolved = await ResolveCredentialsAsync(credentials).ConfigureAwait(false);
        var (siteId, driveId) = await ResolveSiteAndDriveAsync(resolved, null).ConfigureAwait(false);

        // 1. Get the file item by path to obtain its Graph item ID
        var fileUrl = _graph.BuildUrl($"sites/{siteId}/drives/{driveId}/root:/{CommonUtilities.EncodeGraphPath(sourceFilePath.TrimStart('/'))}");
        using var fileDoc = await SendGraphGetAsync(fileUrl, resolved).ConfigureAwait(false);
        var fileItemId = CommonUtilities.JsonGetString(fileDoc.RootElement, "id")
            ?? throw new InvalidOperationException($"Cannot find file item for path: {sourceFilePath}");

        // 2. Ensure the destination folder exists (create if needed), then get its item ID
        var destFolderUrl = _graph.BuildUrl($"sites/{siteId}/drives/{driveId}/root:/{CommonUtilities.EncodeGraphPath(destinationFolderPath.TrimStart('/'))}");

        string destFolderItemId;
        try
        {
            using var folderDoc = await SendGraphGetAsync(destFolderUrl, resolved).ConfigureAwait(false);
            destFolderItemId = CommonUtilities.JsonGetString(folderDoc.RootElement, "id")
                ?? throw new InvalidOperationException($"Cannot read destination folder: {destinationFolderPath}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Destination folder doesn't exist — create it under the library root
            var createFolderUrl = _graph.BuildUrl($"sites/{siteId}/drives/{driveId}/root/children");
            var createPayload = JsonSerializer.Serialize(new
            {
                name = destinationFolderPath.TrimStart('/').Split('/').Last(),
                folder = new { },
                @odata_type = "#microsoft.graph.driveItem"
            });

            using var createRequest = new HttpRequestMessage(HttpMethod.Post, createFolderUrl);
            createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(resolved).ConfigureAwait(false));
            createRequest.Content = new StringContent(createPayload, System.Text.Encoding.UTF8, "application/json");

            using var createResponse = await _httpClientFactory.CreateClient().SendAsync(createRequest).ConfigureAwait(false);
            createResponse.EnsureSuccessStatusCode();

            await using var createStream = await createResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var createDoc = await JsonDocument.ParseAsync(createStream).ConfigureAwait(false);
            destFolderItemId = CommonUtilities.JsonGetString(createDoc.RootElement, "id")
                ?? throw new InvalidOperationException($"Created folder but could not read its ID: {destinationFolderPath}");
        }

        // 3.PATCH the file item to move it to the destination folder and rename it
        var moveUrl = _graph.BuildUrl($"sites/{siteId}/drives/{driveId}/items/{fileItemId}");
        var moveBody = new Dictionary<string, object>
        {
            ["parentReference"] = new { id = destFolderItemId },
            ["name"] = newFileName
        };

        using var moveRequest = new HttpRequestMessage(HttpMethod.Patch, moveUrl);
        moveRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(resolved).ConfigureAwait(false));
            moveRequest.Content = new StringContent(
            JsonSerializer.Serialize(moveBody),
            System.Text.Encoding.UTF8,
            "application/json");

        using var moveResponse = await _httpClientFactory.CreateClient().SendAsync(moveRequest).ConfigureAwait(false);

        if (!moveResponse.IsSuccessStatusCode)
        {
            var errorBody = await moveResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.LogError("Graph API move file failed: {StatusCode} {Body}", (int)moveResponse.StatusCode, errorBody);
            return false;
        }

        return true;
    }

    public async Task<Application> ResolveCredentialsAsync(WorkspaceCredentials credentials)
    {
        if (credentials.ApplicationId.HasValue && credentials.ApplicationId.Value != Guid.Empty)
        {
            var app = await _sharePointRepository.GetApplicationByIdAsync(credentials.ApplicationId.Value).ConfigureAwait(false)
                ?? throw new KeyNotFoundException(ApplicationNotFound);

            ApplyInternalCredentialsIfNeeded(app);

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
        var sitePath = CommonUtilities.NormalizeSharePointSitePath(credentials.SiteName);
        var siteUrl = _graph.BuildUrl($"sites/{credentials.HostName}:/{CommonUtilities.EncodeGraphPath(sitePath)}");
        using var siteDoc = await SendGraphGetAsync(siteUrl, credentials).ConfigureAwait(false);
        var siteId = CommonUtilities.JsonGetString(siteDoc.RootElement, "id");

        if (string.IsNullOrWhiteSpace(siteId))
        {
            _logger.LogError("Graph API did not return a site id for host: {HostName}, site: {SitePath}", credentials.HostName, sitePath);
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
        var url = _graph.BuildUrl($"sites/{siteId}/drives");
        using var doc = await SendGraphGetAsync(url, credentials).ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty("value", out var values))
            return [];

        return values.EnumerateArray()
            .Select(CommonUtilities.MapSharePointLibrary)
            .Where(l => l is not null)
            .Cast<SharePointLibrary>()
            .ToList();
    }

    public async Task<IReadOnlyList<SharePointItem>> ListChildrenAsync(Application credentials, string siteId, string driveId, string? folderPath)
    {
        var url = string.IsNullOrWhiteSpace(folderPath) || folderPath == "/"
            ? _graph.BuildUrl($"sites/{siteId}/drives/{driveId}/root/children")
            : _graph.BuildUrl($"sites/{siteId}/drives/{driveId}/root:/{CommonUtilities.EncodeGraphPath(folderPath)}:/children");

        using var doc = await SendGraphGetAsync(url, credentials).ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty("value", out var values))
            return [];

        return values.EnumerateArray().Select(e => CommonUtilities.MapSharePointItem(e, folderPath)).ToList();
    }

    public async Task<FileStreamContent> GetFileContentAsync(Application credentials, string siteId, string driveId, string filePath, string? rangeHeader)
    {
        var url = _graph.BuildUrl($"sites/{siteId}/drives/{driveId}/root:/{CommonUtilities.EncodeGraphPath(filePath)}:/content");
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
            FileName = CommonUtilities.ExtractFileNameFromPath(filePath)
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

    private sealed record CachedToken(string Token, DateTimeOffset ExpiresOn);
}
