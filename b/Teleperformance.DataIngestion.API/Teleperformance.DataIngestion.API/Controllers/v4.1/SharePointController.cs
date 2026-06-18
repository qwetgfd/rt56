using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Teleperformance.DataIngestion.Sharepoint.Constants;
using static Teleperformance.DataIngestion.Sharepoint.Constants.Constants;
using Teleperformance.DataIngestion.Sharepoint.Interfaces.V1_0;
using Teleperformance.DataIngestion.Sharepoint.Models.Request;
using Teleperformance.DataIngestion.Sharepoint.Models.Response;
using Teleperformance.DataIngestion.Sharepoint.Utilities;

namespace Teleperformance.DataIngestion.API.Controllers.v4._1;

[ApiController]
[Route("api")]
public sealed class SharePointController : ControllerBase
{
    private readonly ISharePointPluginService _sharePointService;

    public SharePointController(ISharePointPluginService sharePointService)
    {
        _sharePointService = sharePointService ?? throw new ArgumentNullException(nameof(sharePointService));
    }

    #region Auth

    /// <summary>
    /// Legacy token endpoint — validates x-client-id and x-client-secret headers.
    /// </summary>
    [HttpPost("auth/requesttoken")]
    public IActionResult RequestToken()
    {
        var apiVersion = CommonUtilities.GetHeader(Request.Headers, "x-tpdi-api-version");
        if (string.IsNullOrWhiteSpace(apiVersion))
            return BadRequest(ApiResponse.Fail("Missing required header: x-tpdi-api-version"));

        var clientId = CommonUtilities.GetHeader(Request.Headers, "x-client-id");
        var clientSecret = CommonUtilities.GetHeader(Request.Headers, "x-client-secret");

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return BadRequest(ApiResponse.Fail(ClientIdAndSecretRequired));

        var validClientId = Environment.GetEnvironmentVariable("authclientid");
        var validClientSecret = Environment.GetEnvironmentVariable("authclientsecret");

        if (string.IsNullOrWhiteSpace(validClientId) || string.IsNullOrWhiteSpace(validClientSecret))
            return StatusCode(500, ApiResponse.Fail(AuthNotConfigured));

        if (!string.Equals(clientId, validClientId, StringComparison.Ordinal) ||
            !string.Equals(clientSecret, validClientSecret, StringComparison.Ordinal))
        {
            return Unauthorized(ApiResponse.Fail(InvalidClientCredentials));
        }

        var (token, expires) = AuthConfiguration.CreateJwt([new Claim("client_id", validClientId)]);

        return Ok(ApiResponse.Ok(new
        {
            AccessToken = AuthConfiguration.WriteToken(token),
            TokenType = "Bearer",
            ExpiresInSeconds = AuthConfiguration.ExpiresInSeconds(expires)
        }));
    }

    /// <summary>
    /// Streaming API token. Requires <c>x-tpdi-api-version</c>, <c>x-application-id</c> (registered app GUID from this system),
    /// and <c>x-client-secret</c> (API secret issued when the app was registered). Not Microsoft Entra.
    /// Returns a JWT whose <c>application_id</c> claim identifies the app; SharePoint/Graph details are resolved internally.
    /// </summary>
    [HttpPost("auth/token")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    [ProducesResponseType(typeof(ApiResponse), 500)]
    public async Task<IActionResult> GenerateToken()
    {
        var apiVersionError = ValidateApiVersion();
        if (apiVersionError != null)
            return apiVersionError;

        var applicationId = CommonUtilities.GetHeader(Request.Headers, "x-application-id");
        var clientSecret = CommonUtilities.GetHeader(Request.Headers, "x-client-secret");

        if (string.IsNullOrWhiteSpace(applicationId) || string.IsNullOrWhiteSpace(clientSecret))
            return BadRequest(ApiResponse.Fail("x-application-id (registered app GUID) and x-client-secret (API secret) headers are required."));

        var tokenRequest = new TokenRequest
        {
            ApplicationId = applicationId,
            ClientSecret = clientSecret
        };

        try
        {
            var result = await _sharePointService.GenerateTokenAsync(tokenRequest).ConfigureAwait(false);
            return Ok(ApiResponse.Ok(result));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse.Fail(InvalidClientCredentials));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, ApiResponse.Fail(ex.Message));
        }
    }

    #endregion

    #region Applications

    [HttpGet("applications/types")]
    public async Task<IActionResult> GetApplicationTypes()
    {
        var types = await _sharePointService.GetApplicationTypesAsync().ConfigureAwait(false);
        return Ok(ApiResponse.Ok(types));
    }

    [HttpGet("applications")]
    public async Task<IActionResult> GetApplications()
    {
        var apps = await _sharePointService.GetApplicationsAsync(null, null).ConfigureAwait(false);
        return Ok(ApiResponse.Ok(apps));
    }

    [HttpGet("applications/{id:guid}")]
    public async Task<IActionResult> GetApplicationById(Guid id)
    {
        var app = await _sharePointService.GetApplicationByIdAsync(id).ConfigureAwait(false);
        return app is null
            ? NotFound(ApiResponse.Fail(ApplicationNotFound))
            : Ok(ApiResponse.Ok(app));
    }

    [HttpPost("applications")]
    public async Task<IActionResult> SaveApplication(Application application)
    {
        var saved = await _sharePointService.SaveApplicationAsync(application).ConfigureAwait(false);
        return Ok(ApiResponse.Ok(saved));
    }

    /// <summary>Records that a user opened a registered application in the workspace.</summary>
    [HttpPost("applications/{id:guid}/use")]
    public async Task<IActionResult> RecordApplicationUse(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RecordApplicationUsageRequest? request)
    {
        try
        {
            var entry = await _sharePointService
                .RecordApplicationUsageAsync(id, request ?? new RecordApplicationUsageRequest())
                .ConfigureAwait(false);

            var log = LogManager.GetCurrentClassLogger();
            var logEvent = new LogEventInfo(
                NLog.LogLevel.Info,
                log.Name,
                $"Application used: {entry.DisplayName}");
            logEvent.Properties["applicationId"] = entry.ApplicationId;
            logEvent.Properties["applicationName"] = entry.DisplayName;
            if (!string.IsNullOrEmpty(entry.UsedByUpn)) logEvent.Properties["usedByUpn"] = entry.UsedByUpn;
            if (!string.IsNullOrEmpty(entry.UsedByDisplayName)) logEvent.Properties["usedByDisplayName"] = entry.UsedByDisplayName;
            log.Log(logEvent);

            return Ok(ApiResponse.Ok(entry));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse.Fail(ApplicationNotFound));
        }
    }

    [HttpDelete("applications/{id:guid}")]
    public async Task<IActionResult> DeleteApplication(Guid id)
    {
        var deleted = await _sharePointService.DeleteApplicationAsync(id).ConfigureAwait(false);
        return deleted
            ? Ok(ApiResponse.Ok(new { ApplicationId = id }))
            : NotFound(ApiResponse.Fail(ApplicationNotFound));
    }

    [HttpPost("applications/libraries")]
    public async Task<IActionResult> ListLibraries([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] WorkspaceCredentials? request)
    {
        var credentials = WorkspaceCredentials.Merge(request, CommonUtilities.ExtractCredentials(Request.Headers));
        var libraries = await _sharePointService.ListLibrariesAsync(credentials).ConfigureAwait(false);
        return Ok(ApiResponse.Ok(libraries));
    }

    /// <summary>
    /// Checks whether the configured internal application can reach the given SharePoint site via Microsoft Graph.
    /// </summary>
    [HttpPost("applications/validate-external-site")]
    public async Task<IActionResult> ValidateExternalSite([FromBody] ExternalSiteConnectivityRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SiteName))
            return BadRequest(ApiResponse.Fail(ExternalSiteNameRequired));

        try
        {
            var result = await _sharePointService
                .ValidateExternalSiteConnectivityAsync(request)
                .ConfigureAwait(false);
            return result.IsConnected
                ? Ok(ApiResponse.Ok(result))
                : Ok(ApiResponse.Fail(result.Message, result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, ApiResponse.Fail(ex.Message));
        }
    }

    #endregion

    #region Workspace

    [HttpPost("workspace/browse")]
    public async Task<IActionResult> BrowseFolder([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] WorkspaceBrowseRequest? request)
    {
        var credentials = WorkspaceCredentials.Merge(request?.Credentials, CommonUtilities.ExtractCredentials(Request.Headers));
        var folderPath = request?.FolderPath ?? CommonUtilities.GetHeader(Request.Headers, "X-Folder-Path");
        var items = await _sharePointService.BrowseFolderAsync(credentials, folderPath).ConfigureAwait(false);
        return Ok(ApiResponse.Ok(items));
    }

    [HttpPost("workspace/fetchfile")]
    public async Task<IActionResult> FetchFile([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] WorkspaceFetchFileRequest? request)
    {
        var credentials = WorkspaceCredentials.Merge(request?.Credentials, CommonUtilities.ExtractCredentials(Request.Headers));
        var filePath = request?.FilePath ?? CommonUtilities.GetHeader(Request.Headers, "X-File-Path");
        return await StreamFileAsync(credentials, filePath, inline: false).ConfigureAwait(false);
    }

    /// <summary>
    /// GET streaming endpoint for HTML5 media elements (video/audio) and inline previews.
    /// Credentials and file path are passed as query parameters; supports Range requests.
    /// </summary>
    [HttpGet("workspace/fetchfile")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> FetchFileGet()
    {
        var credentials = WorkspaceCredentials.Merge(
            CommonUtilities.ExtractCredentialsFromQuery(Request.Query),
            CommonUtilities.ExtractCredentials(Request.Headers));
        var filePath = CommonUtilities.GetDecodedFilePathFromQuery(Request.Query);
        return await StreamFileAsync(credentials, filePath, inline: true).ConfigureAwait(false);
    }

    /// <summary>
    /// Stream or download a file (primary API). Bearer token in header; file path in JSON body.
    /// Supports HTTP Range for partial content (video/audio seeking).
    /// </summary>
    [HttpPost("workspace/file")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    public async Task<IActionResult> PostFileByToken([FromBody] FileStreamRequest? request)
    {
        var (resolvedApp, error) = await ResolveApplicationForFileRequestAsync(requireAuthorizationHeader: true).ConfigureAwait(false);
        if (error != null)
            return error;

        var filePath = request?.FilePath?.Trim();
        if (string.IsNullOrWhiteSpace(filePath))
            return BadRequest(ApiResponse.Fail("filePath is required in the JSON body, e.g. {\"filePath\":\"Documents/video.mp4\"}."));

        var credentials = WorkspaceCredentials.Merge(
            CommonUtilities.ExtractCredentials(Request.Headers),
            WorkspaceCredentials.FromApplication(resolvedApp!));

        return await StreamFileAsync(credentials, filePath, inline: true).ConfigureAwait(false);
    }

    /// <summary>
    /// Browser-only fallback: HTML5 media cannot send Authorization headers or a JSON body.
    /// Use <c>?path=</c> and <c>?access_token=</c> (from step 1). API clients should use POST <c>/workspace/file</c>.
    /// </summary>
    [HttpGet("workspace/file")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetFileByToken()
    {
        var (resolvedApp, error) = await ResolveApplicationForFileRequestAsync(requireAuthorizationHeader: false).ConfigureAwait(false);
        if (error != null)
            return error;

        var filePath = CommonUtilities.GetDecodedFilePathFromQuery(Request.Query);
        if (string.IsNullOrWhiteSpace(filePath))
            return BadRequest(ApiResponse.Fail("File path is required. Use ?path={filePath} or ?path64={base64url}, or POST /workspace/file with a JSON body."));

        var credentials = WorkspaceCredentials.Merge(
            CommonUtilities.ExtractCredentialsFromQuery(Request.Query),
            WorkspaceCredentials.FromApplication(resolvedApp!));
        credentials = WorkspaceCredentials.Merge(CommonUtilities.ExtractCredentials(Request.Headers), credentials);

        return await StreamFileAsync(credentials, filePath, inline: true).ConfigureAwait(false);
    }

    /// <summary>
    /// Simplified browse endpoint using token.
    /// Requires only a Bearer token and optional folder path.
    /// </summary>
    [HttpGet("workspace/browse")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    public async Task<IActionResult> BrowseByToken()
    {
        var apiVersionError = ValidateApiVersion();
        if (apiVersionError != null)
            return apiVersionError;

        var token = CommonUtilities.ResolveBearerToken(Request.Headers, Request.Query, allowQueryToken: false);
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized(ApiResponse.Fail("Missing or invalid Authorization header. Expected: Bearer {token}"));

        var resolvedApp = await _sharePointService.ResolveTokenAsync(token).ConfigureAwait(false);
        if (resolvedApp == null)
            return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

        var folderPath = Request.Query["path"].FirstOrDefault() ?? string.Empty;
        var credentials = WorkspaceCredentials.FromApplication(resolvedApp);

        var items = await _sharePointService.BrowseFolderAsync(credentials, folderPath).ConfigureAwait(false);
        return Ok(ApiResponse.Ok(items));
    }

    #endregion

    private IActionResult? ValidateApiVersion()
    {
        var apiVersion = CommonUtilities.GetHeader(Request.Headers, "x-tpdi-api-version")
            ?? Request.Query["apiVersion"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(apiVersion)
            ? BadRequest(ApiResponse.Fail("Missing required header: x-tpdi-api-version"))
            : null;
    }

    private async Task<(Application? App, IActionResult? Error)> ResolveApplicationForFileRequestAsync(bool requireAuthorizationHeader)
    {
        var apiVersionError = ValidateApiVersion();
        if (apiVersionError != null)
            return (null, apiVersionError);

        var token = CommonUtilities.ResolveBearerToken(
            Request.Headers,
            Request.Query,
            allowQueryToken: !requireAuthorizationHeader);
        if (string.IsNullOrWhiteSpace(token))
        {
            var message = requireAuthorizationHeader
                ? "Missing Authorization header. Expected: Bearer {token}"
                : "Missing token. Use Authorization: Bearer {token} or ?access_token={token}";
            return (null, Unauthorized(ApiResponse.Fail(message)));
        }

        var resolvedApp = await _sharePointService.ResolveTokenAsync(token).ConfigureAwait(false);
        return resolvedApp == null
            ? (null, Unauthorized(ApiResponse.Fail("Invalid or expired token.")))
            : (resolvedApp, null);
    }

    private async Task<IActionResult> StreamFileAsync(WorkspaceCredentials credentials, string? filePath, bool inline)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return BadRequest(ApiResponse.Fail(FilePathRequired));

        var rangeHeader = Request.Headers["Range"].FirstOrDefault();
        var content = await _sharePointService.FetchFileAsync(credentials, filePath, rangeHeader).ConfigureAwait(false);
        await CommonUtilities.WriteFileStreamAsync(Response, content, inline, HttpContext.RequestAborted).ConfigureAwait(false);
        return new EmptyResult();
    }
}
