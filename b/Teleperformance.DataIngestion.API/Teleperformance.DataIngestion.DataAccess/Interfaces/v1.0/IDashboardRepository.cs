
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard;
using Teleperformance.DataIngestion.Models.Entities.v1._0.Dashboard;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IDashboardRepository
    {
        Task<GetProcessListResponse?> GetProcessList(GetProcessListRequest getProcessListRequest);
        Task<GetFileListResponse?> GetFileList(GetFileListRequest getFileListRequest);
        Task<GetClientListResponse?> GetClientList(GetFileListRequest GetClientListRequest);
        Task<IEnumerable<DashboardRealTimeProcessingResponse?>?> DashboardRealTimeProcessingStatusList(GetFileListRequest getDashboardStatusList);
        Task<IEnumerable<CountFileUploadsByProcessTypeResponse?>> CountFileUploadsByProcessType(GetFileListRequest countFileUploadsByProcessTypeRequest);
        Task<IEnumerable<DIFrameworkUtilizationResponse?>> DIFrameworkUtilization(string securityGroupId);        
        Task<IEnumerable<UtilizationByRegion?>> GetUtilizationByRegionList(string securityGroupId);

    }
}
