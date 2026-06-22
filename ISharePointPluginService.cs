using Teleperformance.DataIngestion.Sharepoint.Models.Request;
using Teleperformance.DataIngestion.Sharepoint.Models.Response;

namespace Teleperformance.DataIngestion.Sharepoint.Interfaces.V1_0;

public interface ISharePointPluginService
{
    Task<IReadOnlyList<Application.ApplicationType>> GetApplicationTypesAsync();
    Task<IReadOnlyList<Application>> GetApplicationsAsync(string? ownerKey, string? typeCode);
    Task<Application?> GetApplicationByIdAsync(Guid applicationId);
    Task<Application> SaveApplicationAsync(Application application);
    Task<ApplicationUsageEntry> RecordApplicationUsageAsync(Guid applicationId, RecordApplicationUsageRequest request);
    Task<ExternalSiteConnectivityResult> ValidateExternalSiteConnectivityAsync(ExternalSiteConnectivityRequest request);
    Task<bool> DeleteApplicationAsync(Guid applicationId);
    Task<IReadOnlyList<SharePointLibrary>> ListLibrariesAsync(WorkspaceCredentials credentials);
    Task<IReadOnlyList<SharePointItem>> BrowseFolderAsync(WorkspaceCredentials credentials, string? folderPath);
    Task<FileStreamContent> FetchFileAsync(WorkspaceCredentials credentials, string? filePath, string? rangeHeader);
    Task<bool> MoveFileAsync(WorkspaceCredentials credentials, string sourceFilePath, string destinationFolderPath, string newFileName);
    Task<TokenResponse> GenerateTokenAsync(TokenRequest request);
    Task<Application?> ResolveTokenAsync(string accessToken);
}
