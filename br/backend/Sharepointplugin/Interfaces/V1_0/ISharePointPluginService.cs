using Sharepoint_Plugin.Models.Request;
using Sharepoint_Plugin.Models.Response;

namespace Sharepoint_Plugin.Interfaces.V1_0;

public interface ISharePointPluginService
{
    Task<IReadOnlyList<Application.ApplicationType>> GetApplicationTypesAsync();
    Task<IReadOnlyList<Application>> GetApplicationsAsync(string? ownerKey, string? typeCode);
    Task<Application?> GetApplicationByIdAsync(Guid applicationId);
    Task<Application> SaveApplicationAsync(Application application);
    Task<bool> DeleteApplicationAsync(Guid applicationId);
    Task<IReadOnlyList<SharePointLibrary>> ListLibrariesAsync(WorkspaceCredentials credentials);
    Task<IReadOnlyList<SharePointItem>> BrowseFolderAsync(WorkspaceCredentials credentials, string? folderPath);
    Task<FileStreamContent> FetchFileAsync(WorkspaceCredentials credentials, string? filePath, string? rangeHeader);
    Task<TokenResponse> GenerateTokenAsync(TokenRequest request);
    Task<Application?> ResolveTokenAsync(string accessToken);
}
