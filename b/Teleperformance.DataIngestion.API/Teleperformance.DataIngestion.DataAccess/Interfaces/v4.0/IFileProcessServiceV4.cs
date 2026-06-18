using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0
{
    public interface IFileProcessServiceV4
    {
        Task<APIResponse<FlpDatabricksProcessResponseDto>> ProcessExcelFile(FlpRequestDto flpRequestDto);
        Task<APIResponse<FlpDatabricksProcessResponseDto>> ProcessTxtFile(FlpRequestDto flpRequestDto);
        Task<APIResponse<FlpDatabricksProcessResponseDto>> ProcessCsvFile(FlpRequestDto flpRequestDto);
    }
}
