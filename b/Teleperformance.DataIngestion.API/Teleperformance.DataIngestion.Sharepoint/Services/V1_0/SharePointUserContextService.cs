using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Teleperformance.DataIngestion.Sharepoint.Constants;
using Teleperformance.DataIngestion.Sharepoint.Interfaces.V1_0;
using Teleperformance.DataIngestion.Sharepoint.Models.Request;
using Teleperformance.DataIngestion.Sharepoint.Models.Response;
using Teleperformance.DataIngestion.Sharepoint.Utilities;

namespace Teleperformance.DataIngestion.Sharepoint.Services.V1_0;

public sealed class SharePointUserContextService : ISharePointUserContextService
{
    /// <summary>Bump when drive-discovery behavior changes (verify in API logs after restart).</summary>
    private const string DiscoveryBuildMarker = "sites-read-v4-2026-06-03";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SharePointUserContextService> _logger;
    private readonly GraphApiConfiguration _graph;

    public SharePointUserContextService(IHttpClientFactory httpClientFactory, ILogger<SharePointUserContextService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _graph = new GraphApiConfiguration();
    }

    public async Task<UserConnectSiteResult> ConnectSiteAsync(UserConnectSiteRequest request, string graphAccessToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var siteSlug = UserSiteUrlBuilder.ResolveSiteSlug(request.SiteUrl, request.SiteName, request.HostName);
        var hostName = ResolveHostName(request);
        _logger.LogInformation("UserConnectSite discovery build {BuildMarker}", DiscoveryBuildMarker);

        var (siteTitle, libraries) = await ListLibrariesViaSitesApiAsync(
            hostName, siteSlug, graphAccessToken, cancellationToken).ConfigureAwait(false);

        MeDrivesDiscoveryReport? discoveryReport = null;
        if (libraries.Count == 0)
        {
            (discoveryReport, libraries) = await DiscoverSiteLibrariesFromMeDrivesAsync(
                request, siteSlug, hostName, graphAccessToken, cancellationToken).ConfigureAwait(false);
            LogMeDrivesDiscoveryReport(discoveryReport);

            if (libraries.Count == 0)
            {
                _logger.LogInformation(
                    "UserConnectSite: /me/drives had no site libraries for siteSlug={SiteSlug}. Trying share-root fallback.",
                    siteSlug);
                libraries = await DiscoverLibrariesViaShareRootsAsync(request, siteSlug, hostName, graphAccessToken, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("UserConnectSite: share-root fallback found {Count} libraries.", libraries.Count);
            }
        }
        else
        {
            _logger.LogInformation("UserConnectSite: listed {Count} libraries via GET /sites/{{host}}:/sites/{{slug}} and /sites/{{id}}/drives.", libraries.Count);
        }

        if (libraries.Count == 0)
        {
            throw new UserConnectSiteFailedException(UserContextConstants.SiteLibraryNotFound, discoveryReport);
        }

        return new UserConnectSiteResult
        {
            SiteTitle = siteTitle ?? FormatSiteTitle(siteSlug),
            SiteSlug = siteSlug,
            HostName = hostName,
            Libraries = libraries
        };
    }

    public async Task<MeDrivesDiscoveryReport> GetMeDrivesDiscoveryReportAsync(
        UserConnectSiteRequest request,
        string graphAccessToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var siteSlug = UserSiteUrlBuilder.ResolveSiteSlug(request.SiteUrl, request.SiteName, request.HostName);
        var hostName = ResolveHostName(request);
        var (report, _) = await DiscoverSiteLibrariesFromMeDrivesAsync(
            request, siteSlug, hostName, graphAccessToken, cancellationToken).ConfigureAwait(false);
        LogMeDrivesDiscoveryReport(report);
        return report;
    }

    public async Task<IReadOnlyList<SharePointItem>> BrowseAsync(UserBrowseRequest request, string graphAccessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DriveId))
            throw new ArgumentException(UserContextConstants.BrowseTargetRequired);

        var folderPath = NormalizeFolderPath(request.FolderPath);
        var url = string.IsNullOrEmpty(folderPath)
            ? _graph.BuildUrl($"drives/{request.DriveId.Trim()}/root/children")
            : _graph.BuildUrl($"drives/{request.DriveId.Trim()}/root:/{CommonUtilities.EncodeGraphPath(folderPath)}:/children");

        using var doc = await SendGraphGetAsync(url, graphAccessToken, cancellationToken).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("value", out var values))
            return [];

        return values.EnumerateArray().Select(e => CommonUtilities.MapSharePointItem(e, folderPath)).ToList();
    }

    public async Task<FileStreamContent> GetFileContentAsync(UserFileRequest request, string? rangeHeader, string graphAccessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DriveId) || string.IsNullOrWhiteSpace(request.FilePath))
            throw new ArgumentException(UserContextConstants.FileTargetRequired);

        var filePath = NormalizeFolderPath(request.FilePath) ?? request.FilePath.Trim();
        var url = _graph.BuildUrl($"drives/{request.DriveId.Trim()}/root:/{CommonUtilities.EncodeGraphPath(filePath)}:/content");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", graphAccessToken);
        if (!string.IsNullOrWhiteSpace(rangeHeader))
            httpRequest.Headers.TryAddWithoutValidation("Range", rangeHeader);

        var response = await _httpClientFactory.CreateClient().SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureGraphSuccessAsync(response, url, graphAccessToken, cancellationToken).ConfigureAwait(false);

        return new FileStreamContent
        {
            Content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
            ContentLength = response.Content.Headers.ContentLength,
            StatusCode = (int)response.StatusCode,
            ContentRange = response.Content.Headers.ContentRange?.ToString(),
            AcceptRanges = response.Headers.AcceptRanges.Count > 0 ? string.Join(", ", response.Headers.AcceptRanges) : "bytes",
            FileName = CommonUtilities.ExtractFileNameFromPath(filePath)
        };
    }

    private async Task<(string? SiteTitle, IReadOnlyList<SharePointLibrary> Libraries)> ListLibrariesViaSitesApiAsync(
        string hostName,
        string siteSlug,
        string graphAccessToken,
        CancellationToken cancellationToken)
    {
        var sitePath = $"sites/{siteSlug}";
        var siteUrl = _graph.BuildUrl($"sites/{hostName}:/{CommonUtilities.EncodeGraphPath(sitePath)}");
        _logger.LogInformation("UserConnectSite: GET {SiteUrl}", siteUrl);

        using var siteDoc = await SendGraphGetAsync(siteUrl, graphAccessToken, cancellationToken).ConfigureAwait(false);
        var siteRoot = siteDoc.RootElement;
        var siteId = CommonUtilities.JsonGetString(siteRoot, "id");
        if (string.IsNullOrWhiteSpace(siteId))
            return (null, []);

        var siteTitle = CommonUtilities.JsonGetStringOrNull(siteRoot, "displayName")
            ?? CommonUtilities.JsonGetStringOrNull(siteRoot, "name");

        var drivesUrl = _graph.BuildUrl($"sites/{siteId}/drives");
        _logger.LogInformation("UserConnectSite: GET {DrivesUrl}", drivesUrl);
        using var drivesDoc = await SendGraphGetAsync(drivesUrl, graphAccessToken, cancellationToken).ConfigureAwait(false);

        if (!drivesDoc.RootElement.TryGetProperty("value", out var values))
            return (siteTitle, []);

        var libraries = values.EnumerateArray()
            .Select(CommonUtilities.MapSharePointLibrary)
            .Where(l => l is not null)
            .Cast<SharePointLibrary>()
            .ToList();

        return (siteTitle, OrderLibraries(libraries));
    }

    private async Task<(MeDrivesDiscoveryReport Report, IReadOnlyList<SharePointLibrary> Libraries)> DiscoverSiteLibrariesFromMeDrivesAsync(
        UserConnectSiteRequest request,
        string siteSlug,
        string hostName,
        string graphAccessToken,
        CancellationToken cancellationToken)
    {
        var report = new MeDrivesDiscoveryReport
        {
            RequestSiteUrl = request.SiteUrl,
            RequestSiteName = request.SiteName,
            RequestHostName = request.HostName,
            ResolvedSiteSlug = siteSlug,
            ResolvedHostName = hostName,
            StrictPathMarker = $"/sites/{siteSlug}",
            LibrariesDiscoveryNote =
                "Primary path is GET /sites/{host}:/sites/{slug} (Sites.Read.All) + GET /sites/{siteId}/drives (Files.Read). This report covers the /me/drives fallback only.",
            FilteringSummary =
                "1) GET /me/drives (all pages). " +
                "2) drive candidate filter: exclude driveType 'personal' only (include documentLibrary, business, unknown). " +
                "3) Strict match: webUrl contains '/sites/{siteSlug}' (case-insensitive) AND (webUrl contains resolved hostName OR webUrl contains '.sharepoint.com'). " +
                "4) If no strict matches: use slug-only match (same '/sites/{siteSlug}' path, host rule not required). " +
                "5) If still empty: connect-site tries share URLs for 'Documents' and 'Shared Documents' (may 403 under Files.Read only)."
        };

        var strictMatch = new List<SharePointLibrary>();
        var siteSlugMatch = new List<SharePointLibrary>();
        var index = 0;

        string? nextUrl = _graph.BuildUrl("me/drives");
        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            using var doc = await SendGraphGetAsync(nextUrl, graphAccessToken, cancellationToken).ConfigureAwait(false);
            report.RawMeDrivesResponsePages.Add(doc.RootElement.GetRawText());

            if (!doc.RootElement.TryGetProperty("value", out var drives))
                break;

            foreach (var drive in drives.EnumerateArray())
            {
                index++;
                var name = CommonUtilities.JsonGetStringOrNull(drive, "name");
                var id = CommonUtilities.JsonGetStringOrNull(drive, "id");
                var webUrl = CommonUtilities.JsonGetStringOrNull(drive, "webUrl");
                var driveType = CommonUtilities.JsonGetStringOrNull(drive, "driveType");
                var passesDocLibFilter = IsSiteLibraryDriveCandidate(drive);
                var strict = passesDocLibFilter && SiteMatchesDriveWebUrl(webUrl ?? string.Empty, siteSlug, hostName);
                var slugOnly = passesDocLibFilter && SitePathContainsSlug(webUrl ?? string.Empty, siteSlug);

                var entry = new MeDrivesDiscoveryEntry
                {
                    Index = index,
                    Name = name,
                    Id = id,
                    WebUrl = webUrl,
                    DriveType = driveType,
                    PassesDocumentLibraryFilter = passesDocLibFilter,
                    StrictSiteAndHostMatch = strict,
                    SiteSlugPathMatch = slugOnly,
                    MatchResult = DescribeDriveMatchResult(passesDocLibFilter, driveType, webUrl, siteSlug, hostName, strict, slugOnly)
                };
                report.Drives.Add(entry);

                if (!passesDocLibFilter)
                    continue;

                var library = CommonUtilities.MapSharePointLibrary(drive);
                if (library is null)
                    continue;

                if (strict)
                    strictMatch.Add(library);
                else if (slugOnly)
                    siteSlugMatch.Add(library);
            }

            nextUrl = doc.RootElement.TryGetProperty("@odata.nextLink", out var link)
                ? link.GetString()
                : null;
        }

        report.TotalDriveCount = report.Drives.Count;
        report.DocumentLibraryCandidateCount = report.Drives.Count(d => d.PassesDocumentLibraryFilter);
        report.StrictMatchCount = strictMatch.Count;
        report.SiteSlugOnlyMatchCount = siteSlugMatch.Count;

        var libraries = strictMatch.Count > 0 ? OrderLibraries(strictMatch) : OrderLibraries(siteSlugMatch);
        return (report, libraries);
    }

    private void LogMeDrivesDiscoveryReport(MeDrivesDiscoveryReport report)
    {
        var log = new StringBuilder();
        log.AppendLine("========== UserConnectSite /me/drives discovery (temporary diagnostics) ==========");
        log.AppendLine($"Request siteUrl={report.RequestSiteUrl ?? "(null)"} siteName={report.RequestSiteName ?? "(null)"} hostName={report.RequestHostName ?? "(null)"}");
        log.AppendLine($"Resolved siteSlug={report.ResolvedSiteSlug} hostName={report.ResolvedHostName} pathMarker={report.StrictPathMarker}");
        log.AppendLine(report.FilteringSummary);
        log.AppendLine($"Total drives in /me/drives value[]: {report.TotalDriveCount}");
        log.AppendLine($"Document-library candidates: {report.DocumentLibraryCandidateCount}");
        log.AppendLine($"Strict matches: {report.StrictMatchCount}; Slug-only matches: {report.SiteSlugOnlyMatchCount}");

        for (var i = 0; i < report.RawMeDrivesResponsePages.Count; i++)
            log.AppendLine($"--- Raw GET /me/drives page {i + 1} ---").AppendLine(report.RawMeDrivesResponsePages[i]);

        foreach (var drive in report.Drives)
        {
            log.AppendLine(
                $"Drive #{drive.Index}: name={drive.Name ?? "(null)"} id={drive.Id ?? "(null)"} driveType={drive.DriveType ?? "(null)"}");
            log.AppendLine($"  webUrl={drive.WebUrl ?? "(null)"}");
            log.AppendLine($"  docLibFilter={drive.PassesDocumentLibraryFilter} strict={drive.StrictSiteAndHostMatch} slugPath={drive.SiteSlugPathMatch} => {drive.MatchResult}");
        }

        log.AppendLine("==================================================================================");
        _logger.LogInformation("{DiscoveryLog}", log.ToString());
    }

    private static string DescribeDriveMatchResult(
        bool passesDocLibFilter,
        string? driveType,
        string? webUrl,
        string siteSlug,
        string hostName,
        bool strict,
        bool slugOnly)
    {
        if (!passesDocLibFilter)
            return $"Excluded by driveType filter (driveType='{driveType ?? "(null)"}'; personal OneDrive drives are skipped).";

        if (string.IsNullOrWhiteSpace(webUrl))
            return "Excluded: webUrl is null or empty (cannot match site path).";

        var marker = $"/sites/{siteSlug}";
        if (!SitePathContainsSlug(webUrl, siteSlug))
            return $"Excluded: webUrl does not contain '{marker}'.";

        if (strict)
            return $"Included (strict): webUrl contains '{marker}' and host '{hostName}' or '.sharepoint.com'.";

        if (slugOnly)
            return $"Included (slug-only): webUrl contains '{marker}' but strict host rule failed (host expected '{hostName}').";

        return "Excluded: unexpected filter state.";
    }

    private async Task<IReadOnlyList<SharePointLibrary>> DiscoverLibrariesViaShareRootsAsync(
        UserConnectSiteRequest request,
        string siteSlug,
        string hostName,
        string graphAccessToken,
        CancellationToken cancellationToken)
    {
        var byDriveId = new Dictionary<string, SharePointLibrary>(StringComparer.OrdinalIgnoreCase);
        foreach (var libraryName in new[] { "Documents", "Shared Documents" })
        {
            try
            {
                var shareUrl = UserSiteUrlBuilder.BuildLibraryShareUrl(request.SiteUrl, request.SiteName, hostName, libraryName);
                var shareToken = CommonUtilities.ToGraphShareToken(shareUrl);
                _logger.LogInformation(
                    "UserConnectSite share fallback: libraryName={LibraryName} shareUrl={ShareUrl}",
                    libraryName,
                    shareUrl);
                using var itemDoc = await TrySendGraphGetAsync(
                    _graph.BuildUrl($"shares/{shareToken}/driveItem"),
                    graphAccessToken,
                    cancellationToken,
                    prefer: "redeemSharingLink").ConfigureAwait(false);

                if (itemDoc is null)
                {
                    _logger.LogInformation(
                        "UserConnectSite share fallback: Graph returned non-success for libraryName={LibraryName}",
                        libraryName);
                    continue;
                }

                var root = itemDoc.RootElement;
                if (!root.TryGetProperty("parentReference", out var parentRef))
                    continue;

                var driveId = CommonUtilities.JsonGetStringOrNull(parentRef, "driveId");
                if (string.IsNullOrWhiteSpace(driveId) || byDriveId.ContainsKey(driveId))
                    continue;

                using var driveDoc = await TrySendGraphGetAsync(
                    _graph.BuildUrl($"drives/{driveId}"),
                    graphAccessToken,
                    cancellationToken).ConfigureAwait(false);

                if (driveDoc is null)
                    continue;

                var driveEl = driveDoc.RootElement;
                var driveWebUrl = CommonUtilities.JsonGetStringOrNull(driveEl, "webUrl");
                if (!string.IsNullOrWhiteSpace(driveWebUrl) && !SiteMatchesDriveWebUrl(driveWebUrl, siteSlug, hostName))
                    continue;

                byDriveId[driveId] = new SharePointLibrary
                {
                    Id = driveId,
                    Name = CommonUtilities.JsonGetStringOrNull(driveEl, "name")
                        ?? CommonUtilities.JsonGetStringOrNull(root, "name")
                        ?? libraryName,
                    Description = CommonUtilities.JsonGetStringOrNull(driveEl, "description"),
                    WebUrl = driveWebUrl ?? CommonUtilities.JsonGetStringOrNull(root, "webUrl") ?? shareUrl
                };
            }
            catch (HttpRequestException)
            {
                // Try the next default library name.
            }
        }

        return byDriveId.Values.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool SiteMatchesDriveWebUrl(string webUrl, string siteSlug, string hostName)
    {
        if (!SitePathContainsSlug(webUrl, siteSlug))
            return false;

        if (string.IsNullOrWhiteSpace(hostName))
            return true;

        if (webUrl.IndexOf(hostName, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return webUrl.Contains(".sharepoint.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SitePathContainsSlug(string webUrl, string siteSlug)
    {
        if (string.IsNullOrWhiteSpace(webUrl) || string.IsNullOrWhiteSpace(siteSlug))
            return false;

        return webUrl.IndexOf($"/sites/{siteSlug}", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>Exclude only personal OneDrive; keep documentLibrary, business, and unknown driveType values.</summary>
    private static bool IsSiteLibraryDriveCandidate(JsonElement drive)
    {
        var driveType = CommonUtilities.JsonGetStringOrNull(drive, "driveType");
        return !string.Equals(driveType, "personal", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

    private static IReadOnlyList<SharePointLibrary> OrderLibraries(IEnumerable<SharePointLibrary> libraries) =>
        libraries.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase).ToList();

    private static string ResolveHostName(UserConnectSiteRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.HostName))
        {
            var host = request.HostName.Trim();
            if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                host = host["https://".Length..];
            else if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                host = host["http://".Length..];
            return host.TrimEnd('/');
        }

        if (!string.IsNullOrWhiteSpace(request.SiteUrl) &&
            Uri.TryCreate(NormalizeUrl(request.SiteUrl.Trim()), UriKind.Absolute, out var uri))
            return uri.Host;

        throw new ArgumentException("Host name is required when only a site name is provided.");
    }

    private static string FormatSiteTitle(string siteSlug) =>
        siteSlug.Replace('_', ' ');

    private static string? NormalizeFolderPath(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return null;
        return folderPath.Trim().TrimStart('/').Replace('\\', '/');
    }

    private static string NormalizeUrl(string input)
    {
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return input;
        return "https://" + input.TrimStart('/');
    }

    private async Task<JsonDocument> SendGraphGetAsync(string url, string graphAccessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", graphAccessToken);
        using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureGraphSuccessAsync(response, url, graphAccessToken, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonDocument?> TrySendGraphGetAsync(
        string url,
        string graphAccessToken,
        CancellationToken cancellationToken,
        string? prefer = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", graphAccessToken);
        if (!string.IsNullOrWhiteSpace(prefer))
            request.Headers.TryAddWithoutValidation("Prefer", prefer);

        using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning(
                "Graph non-success (optional call) {Status} {Url}: {Body}",
                (int)response.StatusCode,
                url,
                body);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureGraphSuccessAsync(
        HttpResponseMessage response,
        string requestUrl,
        string graphAccessToken,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var scopes = DescribeTokenScopes(graphAccessToken);
        _logger.LogError(
            "Graph failed {Method} {Url} Status={Status} TokenScopes={Scopes} Body={Body}",
            response.RequestMessage?.Method,
            requestUrl,
            (int)response.StatusCode,
            scopes,
            Truncate(body, 2000));

        throw new GraphCallFailedException(requestUrl, (int)response.StatusCode, body, scopes);
    }

    private static string DescribeTokenScopes(string? bearerToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken)) return "(no token)";
        try
        {
            var parts = bearerToken.Split('.');
            if (parts.Length < 2) return "(invalid jwt)";
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(parts[1])));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("scp", out var scp) && scp.ValueKind == JsonValueKind.String)
                return scp.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.String)
                return roles.GetString() ?? "";
        }
        catch
        {
            return "(could not decode jwt)";
        }

        return "(no scp or roles claim)";
    }

    private static string PadBase64(string payload)
    {
        var padded = payload.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return padded;
    }
}
