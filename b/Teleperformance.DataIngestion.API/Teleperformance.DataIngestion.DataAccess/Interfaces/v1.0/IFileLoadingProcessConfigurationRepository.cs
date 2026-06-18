using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IFileLoadingProcessConfigurationRepository
    {
    
        Task<IEnumerable<FlpConfiguration>> FlpConfigurationDetails(int processTypeId);
        Task<IEnumerable<FlpConfiguration>> FlpConfigurationDetailsToLandingLayer(int processTypeId, int fileProcessingServerTypeId);
        Task<DatabaseResponse> checkFileStatus(string flpConfigurationId, string fileName);
        Task<UploadedFile> commitFileUploadByFunctionApp(string fileName, string flpConfigurationId, string backupFileName, string uploadedBy, int processTypeId);
        Task<DatabaseResponse> commitFlpConfigProcessStatus(string fileUploadedId, int statusId);
        Task<FlpConfiguration> FlpParameterConfigurationByConfigurationId(string flpConfigurationId);
        Task<DestinationStorageAccount> DestinationStorageAccountInfo(string flpConfigurationId);
        Task<DatabaseResponse> UpdateBackUpFileName(string backupFileName, string uploadedFileId);
        Task<SharedLocDestinationServer> GetSharedLocationDestinationServer(string flpConfigurationId);
        Task<IEnumerable<ConfigurationTableMapping>> GetConfigurationTableMappings(string flpConfigurationId);
        Task<DatabaseResponse> AddBackUpFileDetails(string uploadFileId, string flpConfigurationId, string? backupFileName, string? tabName, int totalRows, int insertedRows, int duplicateRows, string blobName);
        Task<DatabaseResponse> AddBackUpFileDetails(string uploadFileId, string flpConfigurationId, string? backupFileName, string? tabName, int totalRows, int insertedRows, int duplicateRows, string blobName, string datalakeStorageAccountPath, float? fileSize, string? csvBlobName);
        Task<DatabaseResponse> commitProcessScheduler(string flpConfigurationId, int flpProcessStatusId);
        Task<DestinationStorageAccount> DatabricksStorageAccountInfo(string flpConfigurationId);
        Task<FlpConfiguration> FlpParameterConfigurationByConfigurationId(string processConfigId, string uploadedFileId);
        Task<DatabaseResponse> checkLandingFileStatus(string flpConfigurationId);
    }
}
