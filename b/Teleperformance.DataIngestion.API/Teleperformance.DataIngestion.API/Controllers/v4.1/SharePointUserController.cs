using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Teleperformance.DataIngestion.Sharepoint.Constants;
using Teleperformance.DataIngestion.Sharepoint.Interfaces.V1_0;
using Teleperformance.DataIngestion.Sharepoint.Models.Request;
using Teleperformance.DataIngestion.Sharepoint.Models.Response;
using Teleperformance.DataIngestion.Sharepoint.Utilities;

namespace Teleperformance.DataIngestion.API.Controllers.v4._1;

[ApiController]
[Route("api/workspace/user")]
public sealed class SharePointUserController : ControllerBase
{
    private readonly ISharePointUserContextService _userContext;
    private readonly IAzureService _azureService;

    public SharePointUserController(ISharePointUserContextService userContext, IAzureService azureService)
    {
        _userContext = userContext;
        _azureService = azureService;
    }

    [HttpPost("connect-site")]
    public async Task<IActionResult> ConnectSite()
    {
        var request = await ConnectSiteBodyReader.ReadAsync(Request, HttpContext.RequestAborted).ConfigureAwait(false);
        return await RunAsync(
            NormalizeConnectSiteRequest(request),
            (resolved, token) => _userContext.ConnectSiteAsync(resolved, token, HttpContext.RequestAborted)).ConfigureAwait(false);
    }

    /// <summary>Temporary diagnostics: GET /me/drives payload and per-drive filter results. Same body/auth as connect-site.</summary>
    [HttpPost("debug-me-drives")]
    public async Task<IActionResult> DebugMeDrives()
    {
        var request = await ConnectSiteBodyReader.ReadAsync(Request, HttpContext.RequestAborted).ConfigureAwait(false);
        return await RunAsync(
            NormalizeConnectSiteRequest(request),
            (resolved, token) => _userContext.GetMeDrivesDiscoveryReportAsync(resolved, token, HttpContext.RequestAborted)).ConfigureAwait(false);
    }

    [HttpPost("browse")]
    public Task<IActionResult> Browse([FromBody] UserBrowseRequest? request)
        => RunAsync(request, (body, token) => _userContext.BrowseAsync(body, token, HttpContext.RequestAborted));

    [HttpPost("fetchfile")]
    public Task<IActionResult> FetchFile([FromBody] UserFileRequest? request)
        => StreamAsync(request, inline: false);

    [HttpPost("file")]
    public Task<IActionResult> StreamFile([FromBody] UserFileRequest? request)
        => StreamAsync(request, inline: true);

    /// <summary>
    /// Browser media preview (video/audio/img). HTML5 elements cannot send Authorization headers;
    /// pass the delegated Graph token as <c>?access_token=</c>. Supports HTTP Range (206).
    /// </summary>
    [HttpGet("file")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    public Task<IActionResult> GetFileStream()
        => StreamFromQueryAsync(inline: true);

    [HttpPost("search-users")]
    public async Task<IActionResult> SearchADUsers([FromBody] ADUserSearchRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Term))
            return BadRequest(ApiResponse.Fail("Search term is required."));
        try
        {
            var result = await _azureService.SearchADUsersAsync(request.Term, request.SearchType).ConfigureAwait(false);
            return Ok(ApiResponse.Ok(result));
        }
        catch (Exception ex) when (TryMapGraphException(ex, out var result)) { return result!; }
    }

    private async Task<IActionResult> RunAsync<TRequest, TResult>(TRequest? body, Func<TRequest, string, Task<TResult>> action)
        where TRequest : class
    {
        if (body is null)
            return BadRequest(ApiResponse.Fail(UserContextConstants.SiteTargetRequired));
        try
        {
            var token = CommonUtilities.ResolveBearerToken(Request.Headers, allowQueryToken: false);
            if (token is null)
                return Unauthorized(ApiResponse.Fail("Missing Authorization: Bearer {token}"));
            return Ok(ApiResponse.Ok(await action(body, token).ConfigureAwait(false)));
        }
        catch (Exception ex) when (TryMapGraphException(ex, out var result))
        {
            return result!;
        }
    }

    private async Task<IActionResult> StreamAsync(UserFileRequest? request, bool inline)
    {
        if (request is null)
            return BadRequest(ApiResponse.Fail(UserContextConstants.FileTargetRequired));
        try
        {
            var token = CommonUtilities.ResolveBearerToken(Request.Headers, allowQueryToken: false);
            if (token is null)
                return Unauthorized(ApiResponse.Fail("Missing Authorization: Bearer {token}"));
            var content = await _userContext.GetFileContentAsync(request, Request.Headers["Range"].FirstOrDefault(), token, HttpContext.RequestAborted).ConfigureAwait(false);
            await CommonUtilities.WriteFileStreamAsync(Response, content, inline, HttpContext.RequestAborted).ConfigureAwait(false);
            return new EmptyResult();
        }
        catch (Exception ex) when (TryMapGraphException(ex, out var result))
        {
            return result!;
        }
    }

    private async Task<IActionResult> StreamFromQueryAsync(bool inline)
    {
        var driveId = Request.Query["driveId"].FirstOrDefault()?.Trim();
        var filePath = CommonUtilities.GetDecodedFilePathFromQuery(Request.Query);
        if (string.IsNullOrWhiteSpace(driveId))
            return BadRequest(ApiResponse.Fail("driveId query parameter is required."));
        if (string.IsNullOrWhiteSpace(filePath))
            return BadRequest(ApiResponse.Fail("File path is required. Use ?path= or ?path64= (base64url)."));

        var request = new UserFileRequest { DriveId = driveId, FilePath = filePath };
        try
        {
            var token = CommonUtilities.ResolveBearerToken(Request.Headers, Request.Query, allowQueryToken: true);
            if (token is null)
                return Unauthorized(ApiResponse.Fail("Missing token. Use Authorization: Bearer {token} or ?access_token={token}"));
            var content = await _userContext.GetFileContentAsync(request, Request.Headers["Range"].FirstOrDefault(), token, HttpContext.RequestAborted).ConfigureAwait(false);
            await CommonUtilities.WriteFileStreamAsync(Response, content, inline, HttpContext.RequestAborted).ConfigureAwait(false);
            return new EmptyResult();
        }
        catch (Exception ex) when (TryMapGraphException(ex, out var result))
        {
            return result!;
        }
    }

    private static UserConnectSiteRequest? NormalizeConnectSiteRequest(UserConnectSiteRequest? request)
        => UserConnectSiteRequestNormalizer.Normalize(request);

    private static bool TryMapGraphException(Exception ex, out IActionResult? result)
    {
        switch (ex)
        {
            case UnauthorizedAccessException:
                result = new ObjectResult(ApiResponse.Fail(UserContextConstants.GraphAccessDenied)) { StatusCode = 403 };
                return true;
            case UserConnectSiteFailedException siteFailed:
                result = new ObjectResult(ApiResponse.Fail(siteFailed.Message, siteFailed.DiscoveryReport)) { StatusCode = 404 };
                return true;
            case KeyNotFoundException notFound:
                result = new ObjectResult(ApiResponse.Fail(
                    string.IsNullOrWhiteSpace(notFound.Message) ? UserContextConstants.GraphResourceNotFound : notFound.Message)) { StatusCode = 404 };
                return true;
            case ArgumentException arg:
                result = new BadRequestObjectResult(ApiResponse.Fail(arg.Message));
                return true;
            case HttpRequestException http:
                result = new BadRequestObjectResult(ApiResponse.Fail(http.Message));
                return true;
            case GraphCallFailedException graphFailed:
                result = new ObjectResult(ApiResponse.Fail(graphFailed.Message, graphFailed.ToDiagnostics()))
                {
                    StatusCode = graphFailed.StatusCode is 401 or 403 ? 403 : graphFailed.StatusCode == 404 ? 404 : 502
                };
                return true;
            default:
                result = null;
                return false;
        }
    }
}

internal static class UserConnectSiteRequestNormalizer
{
    public static UserConnectSiteRequest? Normalize(UserConnectSiteRequest? request)
    {
        if (request is null)
            return null;
        if (string.IsNullOrWhiteSpace(request.SiteUrl) && string.IsNullOrWhiteSpace(request.SiteName))
            return null;
        return request;
    }
}

/// <summary>
/// Reads connect-site JSON explicitly (Newtonsoft). Avoids System.Text.Json JsonElement binding issues.
/// </summary>
internal static class ConnectSiteBodyReader
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };

    public static async Task<UserConnectSiteRequest?> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        string json;
        using (var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true))
        {
            json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            request.Body.Position = 0;
        }

        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var fromDeserializer = JsonConvert.DeserializeObject<UserConnectSiteRequest>(json, SerializerSettings);
            if (fromDeserializer is not null &&
                (!string.IsNullOrWhiteSpace(fromDeserializer.SiteUrl) || !string.IsNullOrWhiteSpace(fromDeserializer.SiteName)))
            {
                return fromDeserializer;
            }

            var root = JObject.Parse(json);
            return new UserConnectSiteRequest
            {
                SiteUrl = ReadString(root, "siteUrl", "SiteUrl"),
                SiteName = ReadString(root, "siteName", "SiteName"),
                HostName = ReadString(root, "hostName", "HostName"),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JObject root, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (!root.TryGetValue(name, out var token))
                continue;
            var value = token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return null;
    }
}
