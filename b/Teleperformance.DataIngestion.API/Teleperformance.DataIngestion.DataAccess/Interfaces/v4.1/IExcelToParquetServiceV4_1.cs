using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using Teleperformance.DataIngestion.Models.Models.v4._1;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1
{
    public interface IExcelToParquetServiceV4_1
    {
        Task<ParquetFileResponseDtoV4_1> ConvertDataToParquet(Models.DTOs.v4._1.FileLoadingConfigurationProcess.ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDtoV4_1 flpConfigurationResponseDto, FlpProcessTempFileModel fileProcessingConfig);



    }
}
