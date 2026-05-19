using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sharepoint_Api.Configuration;
using Sharepoint_Api.Models;
using Sharepoint_Plugin.Interfaces.V_1_0;
using Sharepoint_Plugin.Models;
using Sharepoint_Plugin.Utilities;

namespace Sharepoint_Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SharePointController : ControllerBase
{
    private readonly ISharePointFileService _fileService;
    private readonly SharePointOptions _sharePointOptions;

    public SharePointController(ISharePointFileService fileService, IOptions<SharePointOptions> sharePointOptions)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _sharePointOptions = sharePointOptions?.Value ?? new SharePointOptions();
    }

    /// <summary>TP-Internal defaults from appsettings (host, site, library, credentials).</summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var o = _sharePointOptions;
        return Ok(APIResponse.Ok(new SharePointConfigDto
        {
            TenantId = o.TenantId ?? string.Empty,
            ClientId = o.ClientId ?? string.Empty,
            ClientSecret = o.ClientSecret ?? string.Empty,
            SharepointHostName = o.SharePointHostName ?? string.Empty,
            SitePath = o.SitePath ?? string.Empty,
            DriveName = o.DriveName ?? string.Empty,
            FilePath = o.FilePath ?? string.Empty,
            DefaultDriveName = string.IsNullOrWhiteSpace(o.DefaultDriveName) ? "Documents" : o.DefaultDriveName,
        }));
    }

    /// <summary>Reads required auth headers from the incoming request.</summary>
    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return string.Empty;
    }

    private static (string TenantId, string ClientId, string ClientSecret) GetAuthHeaders(HttpRequest request)
    {
        var tenantId = FirstNonEmpty(request.Headers["tenantId"].ToString(), request.Query["tenantId"]);
        var clientId = FirstNonEmpty(request.Headers["clientId"].ToString(), request.Query["clientId"]);
        var clientSecret = FirstNonEmpty(request.Headers["clientSecret"].ToString(), request.Query["clientSecret"]);

        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new ArgumentException("tenantId, clientId, and clientSecret are required in headers or query string.");

        return (tenantId, clientId, clientSecret);
    }

    /// <summary>Sets environment variables for auth from headers before calling the service.</summary>
    private static void SetAuthEnvironment(HttpRequest request)
    {
        var (tenantId, clientId, clientSecret) = GetAuthHeaders(request);
        Environment.SetEnvironmentVariable("tenantId", tenantId);
        Environment.SetEnvironmentVariable("clientId", clientId);
        Environment.SetEnvironmentVariable("clientSecret", clientSecret);

        // Optional overrides
        if (request.Headers.TryGetValue("graphScope", out var scope))
            Environment.SetEnvironmentVariable("graphScope", scope.ToString());
        if (request.Headers.TryGetValue("grantType", out var grantType))
            Environment.SetEnvironmentVariable("grantType", grantType.ToString());
        if (request.Headers.TryGetValue("tokenEndpoint", out var tokenEndpoint))
            Environment.SetEnvironmentVariable("tokenEndpoint", tokenEndpoint.ToString());
    }

    private static string GetSharePointHostName(HttpRequest httpRequest) =>
        FirstNonEmpty(
            httpRequest.Headers["sharepointHostName"].FirstOrDefault(),
            httpRequest.Headers["hostName"].FirstOrDefault(),
            httpRequest.Query["sharepointHostName"]);

    private static string GetSitePath(HttpRequest httpRequest) =>
        FirstNonEmpty(httpRequest.Headers["sitePath"].FirstOrDefault(), httpRequest.Query["sitePath"]);

    private static string? GetDriveName(HttpRequest httpRequest)
    {
        var raw = FirstNonEmpty(httpRequest.Headers["driveName"].FirstOrDefault(), httpRequest.Query["driveName"]);
        var normalized = SharePointDriveNameHelper.Normalize(raw);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string ResolveStreamContentType(string filePath, string contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType) &&
            !contentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase))
            return contentType.Split(';')[0].Trim();

        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".avi" => "video/x-msvideo",
            ".mp4" => "video/mp4",
            ".m4v" => "video/mp4",
            ".mov" => "video/quicktime",
            ".webm" => "video/webm",
            ".mkv" => "video/x-matroska",
            ".wmv" => "video/x-ms-wmv",
            ".flv" => "video/x-flv",
            ".mpg" or ".mpeg" => "video/mpeg",
            ".ogv" => "video/ogg",
            ".3gp" => "video/3gpp",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".pdf" => "application/pdf",
            _ => contentType
        };
    }

    private IActionResult StreamFileResult(SharePointFileStreamContent result, string filePath)
    {
        Response.StatusCode = result.StatusCode;
        if (!string.IsNullOrWhiteSpace(result.ContentRange))
            Response.Headers["Content-Range"] = result.ContentRange;
        if (!string.IsNullOrWhiteSpace(result.AcceptRanges))
            Response.Headers["Accept-Ranges"] = result.AcceptRanges;
        if (result.ContentLength.HasValue)
            Response.Headers["Content-Length"] = result.ContentLength.Value.ToString();
        var contentType = ResolveStreamContentType(filePath, result.ContentType);
        return File(result.Content, contentType);
    }

    private async Task<IActionResult> StreamFileCoreAsync(string filePath, string hostName, string sitePath, string? driveName = null)
    {
        if (string.IsNullOrWhiteSpace(hostName) || string.IsNullOrWhiteSpace(sitePath) || string.IsNullOrWhiteSpace(filePath))
            return BadRequest(APIResponse.Fail("sharepointHostName, sitePath, and filePath are required."));

        var rangeHeader = Request.Headers["Range"].FirstOrDefault();
        var result = await _fileService.GetFileStreamByPathAsync(
            new FilePathRequest { FilePath = filePath, RangeHeader = rangeHeader },
            new SitePathRequest { HostName = hostName, SitePath = sitePath, DriveName = driveName ?? GetDriveName(Request) }
        ).ConfigureAwait(false);
        return StreamFileResult(result, filePath);
    }

    private static void ApplySiteHeaders(HttpRequest httpRequest, FileByPathRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.HostName))
            request.HostName = GetSharePointHostName(httpRequest);
        if (string.IsNullOrWhiteSpace(request.SitePath))
            request.SitePath = GetSitePath(httpRequest);
        request.DriveName = ResolveDriveName(httpRequest, request.DriveName);
    }

    private static void ApplySiteHeaders(HttpRequest httpRequest, ListChildrenByPathRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.HostName))
            request.HostName = GetSharePointHostName(httpRequest);
        if (string.IsNullOrWhiteSpace(request.SitePath))
            request.SitePath = GetSitePath(httpRequest);
        request.DriveName = ResolveDriveName(httpRequest, request.DriveName);
    }

    private static string? ResolveDriveName(HttpRequest httpRequest, string? bodyDriveName)
    {
        var fromBody = SharePointDriveNameHelper.Normalize(bodyDriveName);
        if (!string.IsNullOrWhiteSpace(fromBody))
            return fromBody;
        return GetDriveName(httpRequest);
    }

    // ── Auth ──────────────────────────────────────────────────────────────

    [HttpPost("token")]
    public async Task<IActionResult> GenerateAccessToken()
    {
        SetAuthEnvironment(Request);
        return Ok(APIResponse.Ok(new { message = "Auth configured. Token will be generated on first service call." }));
    }

    // ── Site and drive discovery ──────────────────────────────────────────

    [HttpPost("site")]
    public async Task<IActionResult> GetSite([FromBody] SiteRequest request)
    {
        SetAuthEnvironment(Request);
        var result = await _fileService.GetSiteAsync(request).ConfigureAwait(false);
        return Ok(APIResponse.Ok(result));
    }

    [HttpPost("drives")]
    public async Task<IActionResult> ListDrives([FromBody] DriveRequest request)
    {
        SetAuthEnvironment(Request);
        var result = await _fileService.ListDrivesAsync(request.SiteId).ConfigureAwait(false);
        return Ok(APIResponse.Ok(result));
    }

    // ── File operations (by site path) ────────────────────────────────────

    /// <summary>Streams a file with range support — for video/audio use GET so the browser can seek progressively.</summary>
    [HttpGet("StreamFile")]
    public async Task<IActionResult> StreamFileGet([FromQuery] string filePath)
    {
        SetAuthEnvironment(Request);
        return await StreamFileCoreAsync(filePath, GetSharePointHostName(Request), GetSitePath(Request)).ConfigureAwait(false);
    }

    /// <summary>Streams a file — host/site in headers, filePath in body.</summary>
    [HttpPost("StreamFile")]
    public async Task<IActionResult> StreamFile([FromBody] StreamFilePathRequest body)
    {
        SetAuthEnvironment(Request);
        var hostName = FirstNonEmpty(body.HostName, GetSharePointHostName(Request));
        var sitePath = FirstNonEmpty(body.SitePath, GetSitePath(Request));
        var driveName = ResolveDriveName(Request, body.DriveName);
        return await StreamFileCoreAsync(body.FilePath, hostName, sitePath, driveName).ConfigureAwait(false);
    }

    [HttpPost("file/metadata")]
    public async Task<IActionResult> GetFileMetadata([FromBody] FileByPathRequest request)
    {
        SetAuthEnvironment(Request);
        ApplySiteHeaders(Request, request);
        var result = await _fileService.GetFileMetadataByPathAsync(
            new FilePathRequest { FilePath = request.FilePath },
            new SitePathRequest { HostName = request.HostName, SitePath = request.SitePath, DriveId = request.DriveId, DriveName = request.DriveName }
        ).ConfigureAwait(false);
        return Ok(APIResponse.Ok(result));
    }

    [HttpPost("file/stream")]
    public async Task<IActionResult> GetFileStream([FromBody] FileByPathRequest request)
    {
        SetAuthEnvironment(Request);
        ApplySiteHeaders(Request, request);
        var result = await _fileService.GetFileStreamByPathAsync(
            new FilePathRequest { FilePath = request.FilePath, RangeHeader = Request.Headers["Range"].FirstOrDefault() },
            new SitePathRequest { HostName = request.HostName, SitePath = request.SitePath, DriveId = request.DriveId, DriveName = request.DriveName }
        ).ConfigureAwait(false);
        return StreamFileResult(result, request.FilePath);
    }

    [HttpPost("file/download")]
    public async Task<IActionResult> DownloadFile([FromBody] FileByPathRequest request)
    {
        SetAuthEnvironment(Request);
        ApplySiteHeaders(Request, request);
        var result = await _fileService.DownloadFileBytesByPathAsync(
            new FilePathRequest { FilePath = request.FilePath },
            new SitePathRequest { HostName = request.HostName, SitePath = request.SitePath, DriveId = request.DriveId, DriveName = request.DriveName }
        ).ConfigureAwait(false);
        return File(result, "application/octet-stream", request.FilePath.Split('/').Last());
    }

    // ── Folder operations ─────────────────────────────────────────────────

    [HttpPost("folder/list")]
    public async Task<IActionResult> ListChildren([FromBody] ListChildrenByPathRequest request)
    {
        SetAuthEnvironment(Request);
        ApplySiteHeaders(Request, request);
        var result = await _fileService.ListChildrenByPathAsync(
            new ListChildrenRequest { FolderPath = request.FolderPath },
            new SitePathRequest { HostName = request.HostName, SitePath = request.SitePath, DriveId = request.DriveId, DriveName = request.DriveName }
        ).ConfigureAwait(false);
        return Ok(APIResponse.Ok(result));
    }

    [HttpPost("folder/create")]
    public async Task<IActionResult> CreateFolder([FromBody] CreateFolderByPathApiRequest request)
    {
        SetAuthEnvironment(Request);
        var result = await _fileService.CreateFolderByPathAsync(new CreateFolderByPathRequest
        {
            HostName = request.HostName,
            SitePath = request.SitePath,
            FolderName = request.FolderName,
            ParentFolderPath = request.ParentFolderPath,
            DriveId = request.DriveId,
            DriveName = request.DriveName,
            ConflictBehavior = request.ConflictBehavior ?? "rename"
        }).ConfigureAwait(false);
        return Ok(APIResponse.Ok(result));
    }
}
