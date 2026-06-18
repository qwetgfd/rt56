using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0
{
    public interface IFileProcessService
    {
        Task<APIResponse<FlpProcessResponseDto>> ProcessExcelFile(FlpRequestDto flpRequestDto);
        Task<APIResponse<FlpProcessResponseDto>> ProcessTxtFile(FlpRequestDto flpRequestDto);
        Task<APIResponse<FlpProcessResponseDto>> ProcessCsvFile(FlpRequestDto flpRequestDto);
    }
}
