using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Models.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0
{
    public interface ICsvToParquetServiceV4
    {
        Task<ParquetFileResponseDto> ConvertDataToParquet(ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto parameterConfig, FlpProcessTempFile fileProcessingConfig);
        Task<ParquetFileResponseDto> ConvertDataToParquetExcel(ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationResponseDto, FlpProcessTempFile fileProcessingConfig);
        Task<ParquetFileResponseDto> ConvertDataToParquetOnPremSharedLocation(string csvTempPath, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto parameterConfig, CheckConnectivitySMBLibraryModel destinationServerModel);
    }
}
