using Sharepoint_Plugin.Models;

namespace Sharepoint_Plugin.Interfaces.V_1_0;

public interface ISharePointFileService
{
    Task<SharePointSiteMetadata> GetSiteAsync(string hostName, string sitePath);
    Task<IReadOnlyList<SharePointDriveMetadata>> ListDrivesAsync(string siteId);
    Task<SharePointFolderMetadata> CreateFolderAsync(string siteId, string driveId, string folderName, string? parentFolderPath = null, string? conflictBehavior = null);
    Task<SharePointFileStreamContent> GetDashboardFileStreamAsync(string hostName, string sitePath, string filePath, string? driveId = null, string? driveName = null);
    Task<SharePointFileMetadata> GetDashboardFileMetadataAsync(string hostName, string sitePath, string filePath, string? driveId = null, string? driveName = null);
    Task<SharePointFolderMetadata> CreateDashboardFolderAsync(string hostName, string sitePath, string folderName, string? parentFolderPath = null, string? driveId = null, string? driveName = null, string? conflictBehavior = null);
    Task<byte[]> DownloadDashboardFileBytesAsync(string hostName, string sitePath, string filePath, string? driveId = null, string? driveName = null);
    Task<SharePointFileMetadata> GetFileMetadataAsync(string siteId, string driveId, string filePath);
    Task<SharePointFileMetadata> GetFileMetadataByIdAsync(string siteId, string driveId, string fileId);
    Task<SharePointFileStreamContent> GetFileStreamAsync(string siteId, string driveId, string filePath);
    Task<SharePointFileStreamContent> GetFileStreamByIdAsync(string siteId, string driveId, string fileId);
    Task<byte[]> DownloadFileBytesAsync(string siteId, string driveId, string filePath);
    Task<IReadOnlyList<SharePointFileMetadata>> ListFilesAsync(string siteId, string driveId, string? folderPath = null);
    Task<IReadOnlyList<SharePointDriveItem>> ListChildrenAsync(string hostName, string sitePath, string? folderPath = null, string? driveId = null, string? driveName = null);
}
