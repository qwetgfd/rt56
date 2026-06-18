using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Models.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0
{
    public interface IExcelToParquetService
    {
        Task<ParquetFileResponseDto> ConvertDataToParquet(ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto parameterConfig, FlpProcessTempFile fileProcessingConfig);

        Task<ParquetFileResponseDto> ConvertDataToParquetOnPremSharedLocation(string csvTempPath, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto parameterConfig, CheckConnectivitySMBLibraryModel destinationServerModel);
    }
}
