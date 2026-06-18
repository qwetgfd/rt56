using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v4._1;
using Teleperformance.DataIngestion.Models.Enums.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IFileLoadingProcessConfiguration
    {       
        Task<APIResponse<IEnumerable<FileLoadingConfigurationResponseDto>>> GetProcessList(int processTypeId);
        Task<bool> UpdateFlpProcessStatus(string flpConfigurationId, APIResultStatus apiResultStatus);
        Task<bool> UpdateFlpProcessStatus(string fileUploadedId, FlpProcessStatusEnum apiResultStatus);
        Task<bool> IsValidFile(string flpConfigurationId, string blobName, string extention);
        Task<APIResponse<FlpConfigurationResponseDto>> GetFlpProcessByConfigurationId(string flpConfigurationId,string uploadedFileId);
        Task<DestinationStorageAccountDto?> DestinationstorageAccountInfo(string flpConfigurationId);
        Task<bool> UpdateBackUpFileName(string backupFileName, string fileUploadedId);
        Task<SharedLocationDestinationServerDto?> SharedLocationDestinationServerDetails(string flpConfigurationId);
        Task<IEnumerable<ConfigurationTableMappingDto?>> ConfigurationTableMapping(string flpConfigurationId);
        Task<bool> AddBackUpFileDetails(string uploadFileId, string flpConfigurationId, string? backupFileName, string? tabName, int totalRows, int insertedRows, int duplicateRows,string blobName);
        Task<bool> AddBackUpFileDetails(string uploadFileId, string flpConfigurationId, string? backupFileName, string? tabName, int totalRows, int insertedRows, int duplicateRows,string blobName,string datalakeStorageAccountPath,float? fileSize,string? csvBlobName);
        Task<bool> UpdateProcessSchedulerStatus(string flpConfigurationId, APIResultStatus apiResultStatus);
        Task<bool> UpdateProcessSchedulerLastDate(string flpConfigurationId);
        Task<DatabricksStorageAccountDto?> DatabricksStorageAccountInfo(string flpConfigurationId);
        Task<APIResponse<IEnumerable<FileLoadingConfigurationResponseDto>>> GetProcessListToLandingLayer(int processTypeId);

    }
}
