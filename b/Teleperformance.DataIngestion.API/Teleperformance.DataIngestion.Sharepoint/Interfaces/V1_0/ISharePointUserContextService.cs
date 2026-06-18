using Teleperformance.DataIngestion.Sharepoint.Models.Request;
using Teleperformance.DataIngestion.Sharepoint.Models.Response;

namespace Teleperformance.DataIngestion.Sharepoint.Interfaces.V1_0;

public interface ISharePointUserContextService
{
    Task<UserConnectSiteResult> ConnectSiteAsync(UserConnectSiteRequest request, string graphAccessToken, CancellationToken cancellationToken = default);
    /// <summary>Temporary: raw /me/drives diagnostics for site/library matching. Remove after verification.</summary>
    Task<MeDrivesDiscoveryReport> GetMeDrivesDiscoveryReportAsync(UserConnectSiteRequest request, string graphAccessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SharePointItem>> BrowseAsync(UserBrowseRequest request, string graphAccessToken, CancellationToken cancellationToken = default);
    Task<FileStreamContent> GetFileContentAsync(UserFileRequest request, string? rangeHeader, string graphAccessToken, CancellationToken cancellationToken = default);
}
