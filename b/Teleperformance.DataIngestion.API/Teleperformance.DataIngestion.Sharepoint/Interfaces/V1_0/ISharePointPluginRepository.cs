using Teleperformance.DataIngestion.Sharepoint.Models.Response;

namespace Teleperformance.DataIngestion.Sharepoint.Interfaces.V1_0;

public interface ISharePointPluginRepository
{
    Task<IReadOnlyList<Application.ApplicationType>> GetApplicationTypesAsync();
    Task<IReadOnlyList<Application>> GetApplicationsAsync(string? ownerKey, int? typeId);
    Task<Application?> GetApplicationByIdAsync(Guid applicationId);
    Task<Application> SaveApplicationAsync(Application application);
    Task<IReadOnlyList<ApplicationSite>> GetApplicationSitesAsync(Guid? applicationId = null);
    Task<IReadOnlyList<ApplicationSite>> SaveApplicationSitesAsync(Guid applicationId, IReadOnlyList<ApplicationSite> sites);
    Task<bool> DeleteApplicationAsync(Guid applicationId);
}
