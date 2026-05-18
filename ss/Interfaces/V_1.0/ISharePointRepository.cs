using Sharepoint_Plugin.Models;

namespace Sharepoint_Plugin.Interfaces.V_1_0;

public interface ISharePointRepository
{
    Task<SharePointSiteMetadata> GetSiteAsync(SiteRequest request);
    Task<IReadOnlyList<SharePointDriveMetadata>> ListDrivesAsync(string siteId);
    Task<SharePointFileMetadata> GetFileMetadataAsync(FilePathRequest request);
    Task<SharePointFileMetadata> GetFileMetadataByIdAsync(FileIdRequest request);
    Task<SharePointFileStreamContent> GetFileStreamAsync(FilePathRequest request);
    Task<SharePointFileStreamContent> GetFileStreamByIdAsync(FileIdRequest request);
    Task<SharePointFolderMetadata> CreateFolderAsync(CreateFolderRequest request);
    Task<IReadOnlyList<SharePointFileMetadata>> ListFilesAsync(ListChildrenRequest request);
    Task<IReadOnlyList<SharePointDriveItem>> ListChildrenAsync(ListChildrenRequest request);
}
