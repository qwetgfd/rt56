using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Models.v4._1;
using ConfigurationTableMappingDto = Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess.ConfigurationTableMappingDto;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1
{
    public interface IFlpProcessingServiceV4_1
    {
        Task<APIResponse<FlpProcessTabResponseDto>> ParquetFileProcessToBronzeTable(long processId, string fileType, string fileLocation, string connectionString, FlpActivityLogStatusEnum currentStatus, FlpProcessTempFileModel flpProcessTempFile, ParquetFileResponseDtoV4_1 resultResponse, Models.DTOs.v4._1.FileLoadingConfigurationProcess.ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDtoV4_1 flpConfigurationRequestDto, DestinationStorageAccountDtoV4_1 destinationStorageAccountDto, SharedLocationDestinationServerDtoV4_1 slDestinationServerDto);

        Task<APIResponse<FlpDatabricksProcessResponseDtoV4_1>> ParquetFileProcessToDataLake(long processId, string fileType, string fileLocation, string connectionString, FlpActivityLogStatusEnum currentStatus, FlpProcessTempFileModel flpProcessTempFile, ParquetFileResponseDtoV4_1 resultResponse, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDtoV4_1 flpConfigurationRequestDto, DestinationStorageAccountDtoV4_1 destinationStorageAccountDto, SharedLocationDestinationServerDtoV4_1 slDestinationServerDto, DatabricksStorageAccountDto4_1 databricksStorageAccountDto);
        Task<(FlpProcessTempFileModel?, bool)> MoveSourceExcelFileToTemporaryDestinationAndDelete(long processId, string fileType, string fileLocation, string fileUploadedId, string backUpFileName,
            FlpConfigurationResponseDtoV4_1 flpConfigurationRequestDto,DestinationStorageAccountDtoV4_1 destinationStorageAccountDto,
            SharedLocationDestinationServerDtoV4_1 slDestinationServerDto, List<FileSettings> fileSettings);
        Task AddFileProcessLosStatus(string tabName, string fileType, string loginId, string message, string messageType,
       long processId, string processName, string tableName, int totalRows, string flpConfigurationId, string fileUploadedId,
       FileStatusActivityEnum fileStatusActivityEnum, FlpActivityLogStatusEnum flpActivityLogStatusEnum, string databricksAPIResponse);
        Task<(bool, string)> DeleteTempFileFromTempLocation(int destinationLocationTypeId, string fileType, string processName, string flpConfigurationId, string uploadedFileId,
            FlpProcessTempFileModel flpProcessTempFile, List<FlpProcessTabResponseDto> fileSettings);
        string GetBearerToken();
    }
}
