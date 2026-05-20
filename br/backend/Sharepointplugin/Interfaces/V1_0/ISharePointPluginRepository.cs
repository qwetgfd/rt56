using Sharepoint_Plugin.Models.Response;

namespace Sharepoint_Plugin.Interfaces.V1_0;

public interface ISharePointPluginRepository
{
    Task<IReadOnlyList<Application.ApplicationType>> GetApplicationTypesAsync();
    Task<IReadOnlyList<Application>> GetApplicationsAsync(string? ownerKey, int? typeId);
    Task<Application?> GetApplicationByIdAsync(Guid applicationId);
    Task<Application> SaveApplicationAsync(Application application);
    Task<bool> DeleteApplicationAsync(Guid applicationId);
}
