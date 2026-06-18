using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
//using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1
{
    public interface IFileProcessServiceV4_1
    {
        Task<APIResponse<FlpProcessResponseDto>> ProcessExcelFile(FlpRequestDto4_1 flpRequestDto);
        Task<APIResponse<FlpProcessResponseDto>> ProcessLandingLayerFile(FlpRequestDto4_1 flpRequestDto);
        Task<APIResponse<FlpProcessResponseDto>> MoveUploadFilesToLayerFile(List<IFormFile> files, string processName, string flpConfigurationId, string uploadFileId, string loginId);

    }
}
