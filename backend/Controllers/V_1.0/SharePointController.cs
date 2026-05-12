using Microsoft.AspNetCore.Mvc;
using Sharepoint_Plugin.Interfaces.V_1_0;
using Sharepoint_Plugin.Models;
using Sharepoint_Plugin.Utility;

namespace Sharepoint_Plugin.Controllers.V_1_0;

[ApiController]
[Route("api/[controller]/[action]")]
[ApiVersion("1.0")]
public class SharePointController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ISharePointFileService _fileService;

    public SharePointController(IAuthService authService, ISharePointFileService fileService)
    {
        _authService = authService;
        _fileService = fileService;
    }

    #region Microsoft Graph File APIs

    [HttpPost]
    public async Task<IActionResult> GenerateAccessToken()
    {
        var accessToken = await _authService
            .GetAccessTokenAsync()
            .ConfigureAwait(false);

        return Ok(APIResponse.Ok(new { accessToken }));
    }

    [HttpPost]
    public async Task<IActionResult> StreamFile(SharePointDashboardFileStreamRequest request)
    {
        var streamContent = await _fileService
            .GetDashboardFileStreamAsync(
                RequestHeaderUtility.GetOptional(Request, "hostName", Environment.GetEnvironmentVariable("hostName")),
                RequestHeaderUtility.GetRequired(Request, "sitePath"),
                request.FilePath,
                RequestHeaderUtility.GetOptional(Request, "driveId"),
                RequestHeaderUtility.GetOptional(Request, "driveName"))
            .ConfigureAwait(false);

        return File(streamContent.Content, streamContent.ContentType);
    }

    [HttpGet]
    public async Task<IActionResult> StreamFileInline([FromQuery] string filePath)
    {
        var streamContent = await _fileService
            .GetDashboardFileStreamAsync(
                RequestHeaderUtility.GetOptionalHeaderOrQuery(Request, "hostName", Environment.GetEnvironmentVariable("hostName")),
                RequestHeaderUtility.GetRequiredHeaderOrQuery(Request, "sitePath"),
                filePath,
                RequestHeaderUtility.GetOptionalHeaderOrQuery(Request, "driveId"),
                RequestHeaderUtility.GetOptionalHeaderOrQuery(Request, "driveName"))
            .ConfigureAwait(false);

        return File(streamContent.Content, streamContent.ContentType);
    }

    [HttpPost]
    public async Task<IActionResult> DownloadFile(SharePointDashboardFileStreamRequest request)
    {
        var bytes = await _fileService
            .DownloadDashboardFileBytesAsync(
                RequestHeaderUtility.GetOptional(Request, "hostName", Environment.GetEnvironmentVariable("hostName")),
                RequestHeaderUtility.GetRequired(Request, "sitePath"),
                request.FilePath,
                RequestHeaderUtility.GetOptional(Request, "driveId"),
                RequestHeaderUtility.GetOptional(Request, "driveName"))
            .ConfigureAwait(false);

        return File(bytes, SharePointFileUtility.FileContentType);
    }

    [HttpPost]
    public async Task<IActionResult> GetFileInfo(SharePointDashboardFileStreamRequest request)
    {
        var metadata = await _fileService
            .GetDashboardFileMetadataAsync(
                RequestHeaderUtility.GetOptional(Request, "hostName", Environment.GetEnvironmentVariable("hostName")),
                RequestHeaderUtility.GetRequired(Request, "sitePath"),
                request.FilePath,
                RequestHeaderUtility.GetOptional(Request, "driveId"),
                RequestHeaderUtility.GetOptional(Request, "driveName"))
            .ConfigureAwait(false);

        return Ok(APIResponse.Ok(metadata));
    }

    [HttpPost]
    public async Task<IActionResult> CreateFolder(SharePointFolderRequest request)
    {
        var folder = await _fileService
            .CreateDashboardFolderAsync(
                RequestHeaderUtility.GetOptional(Request, "hostName", Environment.GetEnvironmentVariable("hostName")),
                RequestHeaderUtility.GetRequired(Request, "sitePath"),
                request.FolderName,
                request.ParentFolderPath,
                RequestHeaderUtility.GetOptional(Request, "driveId"),
                RequestHeaderUtility.GetOptional(Request, "driveName"),
                request.ConflictBehavior)
            .ConfigureAwait(false);

        return Ok(APIResponse.Ok(folder));
    }

    [HttpPost]
    public async Task<IActionResult> GetSite()
    {
        var site = await _fileService
            .GetSiteAsync(
                RequestHeaderUtility.GetOptional(Request, "hostName", Environment.GetEnvironmentVariable("hostName")),
                RequestHeaderUtility.GetRequired(Request, "sitePath"))
            .ConfigureAwait(false);

        return Ok(APIResponse.Ok(site));
    }

    [HttpPost]
    public async Task<IActionResult> ListDrives()
    {
        var site = await _fileService
            .GetSiteAsync(
                RequestHeaderUtility.GetOptional(Request, "hostName", Environment.GetEnvironmentVariable("hostName")),
                RequestHeaderUtility.GetRequired(Request, "sitePath"))
            .ConfigureAwait(false);

        var drives = await _fileService
            .ListDrivesAsync(site.Id)
            .ConfigureAwait(false);

        return Ok(APIResponse.Ok(drives));
    }

    [HttpPost]
    public async Task<IActionResult> ListChildren(ListChildrenRequest request)
    {
        var items = await _fileService
            .ListChildrenAsync(
                RequestHeaderUtility.GetOptional(Request, "hostName", Environment.GetEnvironmentVariable("hostName")),
                RequestHeaderUtility.GetRequired(Request, "sitePath"),
                request.FolderPath,
                RequestHeaderUtility.GetOptional(Request, "driveId"),
                RequestHeaderUtility.GetOptional(Request, "driveName"))
            .ConfigureAwait(false);

        return Ok(APIResponse.Ok(items));
    }

    #endregion
}
