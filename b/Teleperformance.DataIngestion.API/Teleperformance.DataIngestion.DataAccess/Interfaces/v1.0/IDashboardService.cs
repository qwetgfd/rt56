using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IDashboardService
    {
        Task<APIResponse<GetProcessListResponse?>> GetProcessList(GetProcessListRequest getProcessListRequest);
        Task<APIResponse<GetFileListResponse?>> GetFileList(GetFileListRequest getFileListRequest);
        Task<APIResponse<GetClientListResponse?>> GetClientList(GetFileListRequest GetClientListRequest);
        Task<APIResponse<IEnumerable<DashboardRealTimeProcessingResponse?>>> DashboardRealTimeProcessingStatusList(GetFileListRequest getDashboardStatusList);
        Task<APIResponse<IEnumerable<CountFileUploadsByProcessTypeResponse?>>> CountFileUploadsByProcessType(GetFileListRequest countFileUploadsByProcessTypeRequest);
        Task<APIResponse<IEnumerable<DIFrameworkUtilizationResponse?>>> DIFrameworkUtilization();
        Task<APIResponse<IEnumerable<UtilizationByRegionResponseDto?>>> GetUtilizationByRegionList();




    }
}
