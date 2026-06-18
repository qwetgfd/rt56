using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IFileLoadingProcessService
    {
        Task<APIResponse<FlpProcessResponseDto>> ProcessCsvFile(FlpRequestDto flpRequestDto);
        Task<APIResponse<FlpProcessResponseDto>> ProcessTxtFile(FlpRequestDto flpRequestDto);
        Task<APIResponse<FlpProcessResponseDto>> ProcessExcelFile(FlpRequestDto flpRequestDto);
        Task<APIResponse<bool>> UpdateProcessSchedulerLastDate(string flpConfigurationId);
    }
}
