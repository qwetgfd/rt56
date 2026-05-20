using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.IdentityModel.Tokens;
using Sharepoint_Plugin.Constants;
using static Sharepoint_Plugin.Constants.Constants;
using Sharepoint_Plugin.Interfaces.V1_0;
using Sharepoint_Plugin.Models.Request;
using Sharepoint_Plugin.Models.Response;
using Sharepoint_Plugin.Utilities;

namespace SharepointApi.Controllers.V1_0;

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

        var signingKey = Environment.GetEnvironmentVariable("authsigningkey") ?? throw new InvalidOperationException(SigningKeyNotConfigured);
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
            claims: new[] { new Claim("client_id", validClientId) },
            expires: expires,
            signingCredentials: credentials);

        return Ok(ApiResponse.Ok(new
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            TokenType = "Bearer",
            ExpiresInSeconds = (int)(expires - DateTime.UtcNow).TotalSeconds
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
        var credentials = MergeCredentials(request, CommonUtilities.ExtractCredentials(Request.Headers));
        var libraries = await _sharePointService.ListLibrariesAsync(credentials).ConfigureAwait(false);
        return Ok(ApiResponse.Ok(libraries));
    }

    #endregion

    #region Workspace

    [HttpPost("workspace/browse")]
    public async Task<IActionResult> BrowseFolder([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] WorkspaceBrowseRequest? request)
    {
        var credentials = MergeCredentials(request?.Credentials, CommonUtilities.ExtractCredentials(Request.Headers));
        var folderPath = request?.FolderPath ?? CommonUtilities.GetHeader(Request.Headers, "X-Folder-Path");
        var items = await _sharePointService.BrowseFolderAsync(credentials, folderPath).ConfigureAwait(false);
        return Ok(ApiResponse.Ok(items));
    }

    [HttpPost("workspace/fetchfile")]
    public async Task<IActionResult> FetchFile([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] WorkspaceFetchFileRequest? request)
    {
        var credentials = MergeCredentials(request?.Credentials, CommonUtilities.ExtractCredentials(Request.Headers));
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
        var credentials = MergeCredentials(
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

        var credentials = MergeCredentials(
            CommonUtilities.ExtractCredentials(Request.Headers),
            ToWorkspaceCredentials(resolvedApp!));

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

        var credentials = MergeCredentials(
            CommonUtilities.ExtractCredentialsFromQuery(Request.Query),
            ToWorkspaceCredentials(resolvedApp!));
        credentials = MergeCredentials(CommonUtilities.ExtractCredentials(Request.Headers), credentials);

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

        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Unauthorized(ApiResponse.Fail("Missing or invalid Authorization header. Expected: Bearer {token}"));

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized(ApiResponse.Fail("Token is empty."));

        var resolvedApp = await _sharePointService.ResolveTokenAsync(token).ConfigureAwait(false);
        if (resolvedApp == null)
            return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

        var folderPath = Request.Query["path"].FirstOrDefault() ?? string.Empty;
        var credentials = ToWorkspaceCredentials(resolvedApp);

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

    private string? ResolveBearerToken(bool authorizationHeaderOnly = false)
    {
        if (!authorizationHeaderOnly)
        {
            var fromQuery = Request.Query["access_token"].FirstOrDefault()
                ?? Request.Query["token"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fromQuery))
                return fromQuery.Trim();
        }

        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authHeader["Bearer ".Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private async Task<(Application? App, IActionResult? Error)> ResolveApplicationForFileRequestAsync(bool requireAuthorizationHeader)
    {
        var apiVersionError = ValidateApiVersion();
        if (apiVersionError != null)
            return (null, apiVersionError);

        var token = ResolveBearerToken(requireAuthorizationHeader);
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

    private static WorkspaceCredentials ToWorkspaceCredentials(Application app) => new()
    {
        ApplicationId = app.ApplicationId,
        TenantId = app.TenantId,
        ClientId = app.ClientId,
        ClientSecret = app.ClientSecret,
        HostName = app.HostName,
        SiteName = app.SiteName,
        LibraryName = app.LibraryName
    };

    private static WorkspaceCredentials MergeCredentials(WorkspaceCredentials? primary, WorkspaceCredentials fallback)
    {
        if (primary is null) return fallback;

        return new WorkspaceCredentials
        {
            ApplicationId = primary.ApplicationId ?? fallback.ApplicationId,
            TenantId = string.IsNullOrWhiteSpace(primary.TenantId) ? fallback.TenantId : primary.TenantId,
            ClientId = string.IsNullOrWhiteSpace(primary.ClientId) ? fallback.ClientId : primary.ClientId,
            ClientSecret = string.IsNullOrWhiteSpace(primary.ClientSecret) ? fallback.ClientSecret : primary.ClientSecret,
            HostName = string.IsNullOrWhiteSpace(primary.HostName) ? fallback.HostName : primary.HostName,
            SiteName = string.IsNullOrWhiteSpace(primary.SiteName) ? fallback.SiteName : primary.SiteName,
            LibraryName = string.IsNullOrWhiteSpace(primary.LibraryName) ? fallback.LibraryName : primary.LibraryName
        };
    }

    private async Task<IActionResult> StreamFileAsync(WorkspaceCredentials credentials, string? filePath, bool inline)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return BadRequest(ApiResponse.Fail(FilePathRequired));

        var rangeHeader = Request.Headers["Range"].FirstOrDefault();
        var content = await _sharePointService.FetchFileAsync(credentials, filePath, rangeHeader).ConfigureAwait(false);
        var contentType = CommonUtilities.ResolveContentType(content.FileName, content.ContentType);

        Response.StatusCode = content.StatusCode;
        Response.ContentType = contentType;
        Response.Headers["Accept-Ranges"] = !string.IsNullOrWhiteSpace(content.AcceptRanges)
            ? content.AcceptRanges
            : "bytes";
        if (!string.IsNullOrWhiteSpace(content.ContentRange))
            Response.Headers["Content-Range"] = content.ContentRange;
        if (content.ContentLength.HasValue)
            Response.Headers["Content-Length"] = content.ContentLength.Value.ToString();

        if (inline)
            Response.Headers["Content-Disposition"] = "inline";
        else if (!string.IsNullOrWhiteSpace(content.FileName))
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{content.FileName}\"";

        await content.Content.CopyToAsync(Response.Body, HttpContext.RequestAborted).ConfigureAwait(false);
        return new EmptyResult();
    }
}
