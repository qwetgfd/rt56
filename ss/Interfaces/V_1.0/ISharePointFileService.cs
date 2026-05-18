using Sharepoint_Plugin.Models;

namespace Sharepoint_Plugin.Interfaces.V_1_0;

public interface ISharePointFileService
{
    // Site and drive discovery
    Task<SharePointSiteMetadata> GetSiteAsync(SiteRequest request);
    Task<IReadOnlyList<SharePointDriveMetadata>> ListDrivesAsync(string siteId);

    // Direct drive operations
    Task<SharePointFileMetadata> GetFileMetadataAsync(FilePathRequest request);
    Task<SharePointFileMetadata> GetFileMetadataByIdAsync(FileIdRequest request);
    Task<SharePointFileStreamContent> GetFileStreamAsync(FilePathRequest request);
    Task<SharePointFileStreamContent> GetFileStreamByIdAsync(FileIdRequest request);
    Task<byte[]> DownloadFileBytesAsync(FilePathRequest request);
    Task<IReadOnlyList<SharePointFileMetadata>> ListFilesAsync(ListChildrenRequest request);
    Task<SharePointFolderMetadata> CreateFolderAsync(CreateFolderRequest request);

    // Site-path operations (auto-resolves drive from hostName + sitePath)
    Task<SharePointFileMetadata> GetFileMetadataByPathAsync(FilePathRequest request, SitePathRequest site);
    Task<SharePointFileStreamContent> GetFileStreamByPathAsync(FilePathRequest request, SitePathRequest site);
    Task<byte[]> DownloadFileBytesByPathAsync(FilePathRequest request, SitePathRequest site);
    Task<IReadOnlyList<SharePointDriveItem>> ListChildrenByPathAsync(ListChildrenRequest request, SitePathRequest site);
    Task<SharePointFolderMetadata> CreateFolderByPathAsync(CreateFolderByPathRequest request);
}
