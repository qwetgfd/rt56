using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._0;
using Teleperformance.DataIngestion.Models.DTOs.v4._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Enums.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0
{
    public interface IFlpProcessingServiceV4
    {
        Task<APIResponse<FlpDatabricksProcessResponseDto>> ParquetFileProcessToDataLake(long processId, string fileType, string fileLocation, string? tabName, string connectionString, FlpActivityLogStatusEnum currentStatus, FlpProcessTempFile flpProcessTempFile, ParquetFileResponseDto resultResponse, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto, DatabricksStorageAccountDto databricksStorageAccountDto);

        Task<(FlpProcessTempFile?, bool)> MoveSourceFileToTemporaryDestinationAndDelete(long processId, string fileType, string fileLocation, string fileUploadedId, string backUpFileName, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto);
        Task AddFileProcessLosStatus(string fileType, string loginId, string message, string messageType,long processId, string processName, string tableName, int totalRows, string flpConfigurationId, string fileUploadedId,
               FileStatusActivityEnum fileStatusActivityEnum, FlpActivityLogStatusEnum flpActivityLogStatusEnum);

        Task AddSecurityGroup(SecurityGroup securityGroups);
        Task<(FlpProcessTempFile?, bool)> MoveSourceExcelFileToTemporaryDestinationAndDelete(long processId, string fileType, string fileLocation, string fileUploadedId, string backUpFileName, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto);

        string GetBearerToken();

    }
}
