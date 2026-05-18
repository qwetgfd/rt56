using Sharepoint_Plugin.Interfaces.V_1_0;
using Sharepoint_Plugin.Models;
using Sharepoint_Plugin.Utilities;

namespace Sharepoint_Plugin.Services.V_1_0;

public class SharePointFileService : ISharePointFileService
{
    private readonly ISharePointRepository _sharePointPluginRepo;
    private readonly string _defaultDriveName;

    public SharePointFileService(ISharePointRepository sharePointPluginRepo, IAuthService auth)
    {
        _sharePointPluginRepo = sharePointPluginRepo ?? throw new ArgumentNullException(nameof(sharePointPluginRepo));
        _defaultDriveName = Environment.GetEnvironmentVariable("sharepointDefaultDriveName") ?? "Documents";
    }

    public async Task<SharePointSiteMetadata> GetSiteAsync(SiteRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(request.HostName);
        ArgumentException.ThrowIfNullOrEmpty(request.SitePath);
        return await _sharePointPluginRepo.GetSiteAsync(request).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SharePointDriveMetadata>> ListDrivesAsync(string siteId)
    {
        ArgumentException.ThrowIfNullOrEmpty(siteId);
        return await _sharePointPluginRepo.ListDrivesAsync(siteId).ConfigureAwait(false);
    }

    public async Task<SharePointFileMetadata> GetFileMetadataAsync(FilePathRequest request)
    {
        ValidateDriveRequest(request);
        ArgumentException.ThrowIfNullOrEmpty(request.FilePath);
        return await _sharePointPluginRepo.GetFileMetadataAsync(request).ConfigureAwait(false);
    }

    public async Task<SharePointFileMetadata> GetFileMetadataByIdAsync(FileIdRequest request)
    {
        ValidateDriveRequest(request);
        ArgumentException.ThrowIfNullOrEmpty(request.FileId);
        return await _sharePointPluginRepo.GetFileMetadataByIdAsync(request).ConfigureAwait(false);
    }

    public async Task<SharePointFileStreamContent> GetFileStreamAsync(FilePathRequest request)
    {
        ValidateDriveRequest(request);
        ArgumentException.ThrowIfNullOrEmpty(request.FilePath);
        return await _sharePointPluginRepo.GetFileStreamAsync(request).ConfigureAwait(false);
    }

    public async Task<SharePointFileStreamContent> GetFileStreamByIdAsync(FileIdRequest request)
    {
        ValidateDriveRequest(request);
        ArgumentException.ThrowIfNullOrEmpty(request.FileId);
        return await _sharePointPluginRepo.GetFileStreamByIdAsync(request).ConfigureAwait(false);
    }

    public async Task<byte[]> DownloadFileBytesAsync(FilePathRequest request)
    {
        var stream = await GetFileStreamAsync(request).ConfigureAwait(false);
        return await StreamHelpers.ToBytesAsync(stream.Content).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SharePointFileMetadata>> ListFilesAsync(ListChildrenRequest request)
    {
        ValidateDriveRequest(request);
        return await _sharePointPluginRepo.ListFilesAsync(request).ConfigureAwait(false);
    }

    public async Task<SharePointFolderMetadata> CreateFolderAsync(CreateFolderRequest request)
    {
        ValidateDriveRequest(request);
        ArgumentException.ThrowIfNullOrEmpty(request.FolderName);
        return await _sharePointPluginRepo.CreateFolderAsync(request).ConfigureAwait(false);
    }

    public async Task<SharePointFileMetadata> GetFileMetadataByPathAsync(FilePathRequest request, SitePathRequest site)
    {
        var (siteId, driveId) = await ResolveSiteDriveAsync(site).ConfigureAwait(false);
        request.SiteId = siteId;
        request.DriveId = driveId;
        return await _sharePointPluginRepo.GetFileMetadataAsync(request).ConfigureAwait(false);
    }

    public async Task<SharePointFileStreamContent> GetFileStreamByPathAsync(FilePathRequest request, SitePathRequest site)
    {
        var (siteId, driveId) = await ResolveSiteDriveAsync(site).ConfigureAwait(false);
        request.SiteId = siteId;
        request.DriveId = driveId;
        return await _sharePointPluginRepo.GetFileStreamAsync(request).ConfigureAwait(false);
    }

    public async Task<byte[]> DownloadFileBytesByPathAsync(FilePathRequest request, SitePathRequest site)
    {
        var stream = await GetFileStreamByPathAsync(request, site).ConfigureAwait(false);
        return await StreamHelpers.ToBytesAsync(stream.Content).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SharePointDriveItem>> ListChildrenByPathAsync(ListChildrenRequest request, SitePathRequest site)
    {
        var (siteId, driveId) = await ResolveSiteDriveAsync(site).ConfigureAwait(false);
        request.SiteId = siteId;
        request.DriveId = driveId;
        return await _sharePointPluginRepo.ListChildrenAsync(request).ConfigureAwait(false);
    }

    public async Task<SharePointFolderMetadata> CreateFolderByPathAsync(CreateFolderByPathRequest request)
    {
        var (siteId, driveId) = await ResolveSiteDriveAsync(new SitePathRequest { HostName = request.HostName, SitePath = request.SitePath, DriveId = request.DriveId, DriveName = request.DriveName }).ConfigureAwait(false);
        return await _sharePointPluginRepo.CreateFolderAsync(new CreateFolderRequest { SiteId = siteId, DriveId = driveId, FolderName = request.FolderName, ParentFolderPath = request.ParentFolderPath, ConflictBehavior = request.ConflictBehavior }).ConfigureAwait(false);
    }

    private static void ValidateDriveRequest(DriveRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(request.SiteId);
        ArgumentException.ThrowIfNullOrEmpty(request.DriveId);
    }

    private async Task<(string SiteId, string DriveId)> ResolveSiteDriveAsync(SitePathRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(request.HostName);
        ArgumentException.ThrowIfNullOrEmpty(request.SitePath);

        var site = await _sharePointPluginRepo.GetSiteAsync(new SiteRequest { HostName = request.HostName, SitePath = request.SitePath }).ConfigureAwait(false);
        var driveId = string.IsNullOrWhiteSpace(request.DriveId) ? await ResolveDriveIdAsync(site.Id, request.DriveName).ConfigureAwait(false) : request.DriveId;
        return (site.Id, driveId);
    }

    private async Task<string> ResolveDriveIdAsync(string siteId, string? driveName)
    {
        var target = string.IsNullOrWhiteSpace(driveName) ? _defaultDriveName : driveName;
        var drives = await _sharePointPluginRepo.ListDrivesAsync(siteId).ConfigureAwait(false);
        var drive = drives.FirstOrDefault(d => string.Equals(d.Name, target, StringComparison.OrdinalIgnoreCase));

        if (drive is not null) return drive.Id;

        var available = string.Join(", ", drives.Select(d => d.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
        throw new ArgumentException($"Drive '{target}' not found for site '{siteId}'. Available: {available}");
    }
}
