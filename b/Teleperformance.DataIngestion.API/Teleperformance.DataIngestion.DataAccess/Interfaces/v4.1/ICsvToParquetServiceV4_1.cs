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

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1
{
    public interface ICsvToParquetServiceV4_1
    {
        // Task<ParquetFileResponseDtoV4_1> ConvertDataToParquet(ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto parameterConfig, FlpProcessTempFile fileProcessingConfig);
        Task<ParquetFileResponseDtoV4_1> ConvertCsvToParquetAsync(Stream txtStream, Stream parquetStream, Models.DTOs.v4._1.FileLoadingConfigurationProcess.ConfigurationTableMappingDto parameterConfig, FlpConfigurationResponseDtoV4_1 flpConfigurationResponseDto);
        Task<ParquetFileResponseDtoV4_1> ConvertCsvToParquetAsyncV2(Stream txtStream, Stream parquetStream, Models.DTOs.v1._0.FileLoadingConfigurationProcess.ConfigurationTableMappingDto parameterConfig, FlpConfigurationResponseDto flpConfigurationResponseDto);
    }
}
