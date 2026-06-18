using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs;
using Teleperformance.DataIngestion.Models.DTOs.v1;
using Teleperformance.DataIngestion.Models.DTOs.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IStatusService
    {
        Task<APIResponse<IEnumerable<FlpUploadedFileStatusResponseDto?>>> FileUploadStatus();
        Task<APIResponse<FileConfigurationStatusDto?>> FileUploadStatus(StatusRequest statusRequest);
        Task<APIResponse<UploadFileStatusResponseDto?>> FileUploadDetailedStatus(FileUploadDetailedStatusRequest fileUploadDetailedStatusRequest);

        Task<APIResponse<ProcessedFileResponse>> GetProcessedFileList(ProcessedFileListRequestDto request);
    }

    
}
