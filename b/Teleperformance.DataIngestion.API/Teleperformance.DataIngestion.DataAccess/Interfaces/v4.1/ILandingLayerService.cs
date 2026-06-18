using Microsoft.AspNetCore.Http;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1
{
    public interface ILandingLayerService
    {
        // Task<(bool, string)> MoveFileInLandingLayerFolder(List<IFormFile> files, string processName, string flpConfigurationId, string uploadFileId, string loginId);
        Task<APIResponse<FlpProcessResponseDto>> MoveUploadFilesToLayerFile(List<IFormFile> files, string processName, string flpConfigurationId, string uploadFileId, string loginId);
        Task<(bool,string)> MoveFileInLandingLayerFolder(List<IFormFile> files, string processName, string flpConfigurationId, string uploadFileId, string loginId);
        Task<APIResponse<FlpProcessResponseDto>> ProcessFileFromRemoteToLandingLayer(FlpRequestDto4_1 flpRequestDto);
        Task<APIResponse<FlpProcessResponseDto>> ProcessFileFromBlobToLandingLayer(FlpRequestDto4_1 flpRequestDto);
    }
}
