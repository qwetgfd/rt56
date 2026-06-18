using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v1._0
{
    public class FileLoadingProcessConfigurationRepository : IFileLoadingProcessConfigurationRepository
    {
        private readonly ILogger<FileLoadingProcessConfigurationRepository> _logger;
        private readonly IDapperService _dapperService;

        public FileLoadingProcessConfigurationRepository(ILogger<FileLoadingProcessConfigurationRepository> logger, IDapperService dapperService)
        {
            _logger = logger;
            _dapperService = dapperService;
        }
        public async Task<IEnumerable<FlpConfiguration>> FlpConfigurationDetails(int processTypeId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@processTypeId", processTypeId);
                var csvConfiguration = await _dapperService.GetMultipleRowsAsync<FlpConfiguration>("[sel_flpConfigurationList]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return csvConfiguration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }


        public async Task<IEnumerable<FlpConfiguration>> FlpConfigurationDetailsToLandingLayer(int processTypeId,int fileProcessingServerTypeId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@processTypeId", processTypeId);
                dynamicParameters.Add("@fileProcessingServerTypeId", fileProcessingServerTypeId);
                var csvConfiguration = await _dapperService.GetMultipleRowsAsync<FlpConfiguration>("[sel_flpConfigurationList]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return csvConfiguration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }


        public async Task<DatabaseResponse> checkFileStatus(string flpConfigurationId, string fileName)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@fileName", fileName);
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[sel_checkUploadedFileStatus]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }


        public async Task<DatabaseResponse> checkLandingFileStatus(string flpConfigurationId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[sel_checkLandingLayerUploadedFileStatus]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }


        public async Task<UploadedFile> commitFileUploadByFunctionApp(string fileName, string flpConfigurationId, string backupFileName, string uploadedBy,int processTypeId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@fileName", fileName);
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@backupFileName", backupFileName);
                dynamicParameters.Add("@uploadedBy", uploadedBy);
                dynamicParameters.Add("@processTypeId", processTypeId);
                var dbResponse = await _dapperService.GetSingleRowAsync<UploadedFile>("[commit_fileUpload]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }


        public async Task<DatabaseResponse> AddBackUpFileDetails(string uploadFileId, string flpConfigurationId, string? backupFileName, string? tabName,int totalRows,int insertedRows, int duplicateRows,string blobName)
        {
            try
            {

                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@uploadFileId", uploadFileId);
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@backupFileName", backupFileName);
                dynamicParameters.Add("@blobName", blobName);
                dynamicParameters.Add("@tabName", tabName);
                dynamicParameters.Add("@totalRows", totalRows);
                dynamicParameters.Add("@insertedRows", insertedRows);
                dynamicParameters.Add("@duplicateRows", duplicateRows);
                var dbResponse = await _dapperService.GetSingleRowAsync<UploadedFile>("[commit_backUpFile]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<DatabaseResponse> AddBackUpFileDetails(string uploadFileId, string flpConfigurationId, string? backupFileName, string? tabName, int totalRows, int insertedRows, int duplicateRows, string blobName,string datalakeStorageAccountPath,float? fileSize,string? csvBlobName)
        {
            try
            {

                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@uploadFileId", uploadFileId);
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@backupFileName", backupFileName);
                dynamicParameters.Add("@blobName", blobName);
                dynamicParameters.Add("@tabName", tabName);
                dynamicParameters.Add("@totalRows", totalRows);
                dynamicParameters.Add("@insertedRows", insertedRows);
                dynamicParameters.Add("@duplicateRows", duplicateRows);
                dynamicParameters.Add("@datalakeStorageAccountPath", datalakeStorageAccountPath);
                if(fileSize > 0)
                 dynamicParameters.Add("@fileSize", fileSize);

                if(!string.IsNullOrWhiteSpace(csvBlobName))
                dynamicParameters.Add("@csvTempBlobName", csvBlobName);

                var dbResponse = await _dapperService.GetSingleRowAsync<UploadedFile>("[commit_backUpFile]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }



        public async Task<DatabaseResponse> commitFlpConfigProcessStatus(string fileUploadedId, int statusId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@fileUploadedId", fileUploadedId);
                dynamicParameters.Add("@statusId", statusId);
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_FlpConfigProcessStatus]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }
        public async Task<DatabaseResponse> commitProcessScheduler(string flpConfigurationId, int flpProcessStatusId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@flpProcessStatusId", flpProcessStatusId);
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_processScheduler]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<FlpConfiguration> FlpParameterConfigurationByConfigurationId(string processConfigId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", processConfigId);
                var result = await _dapperService.GetSingleRowAsync<FlpConfiguration>("[sel_flpConfigurationByFlpProcessConfigId]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<FlpConfiguration> FlpParameterConfigurationByConfigurationId(string processConfigId,string uploadedFileId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", processConfigId);
                dynamicParameters.Add("@uploadedFileId", uploadedFileId);
                var result = await _dapperService.GetSingleRowAsync<FlpConfiguration>("[sel_flpConfigurationByFlpProcessConfigId]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<DestinationStorageAccount> DestinationStorageAccountInfo(string flpConfigurationId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                var csvConfiguration = await _dapperService.GetSingleRowAsync<DestinationStorageAccount>("[sel_DestinationStorageAccountInfo]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return csvConfiguration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<DestinationStorageAccount> DatabricksStorageAccountInfo(string flpConfigurationId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                var csvConfiguration = await _dapperService.GetSingleRowAsync<DestinationStorageAccount>("[sel_DatabriksStorageAccountInfo]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return csvConfiguration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<DatabaseResponse> UpdateBackUpFileName(string backupFileName, string uploadedFileId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@backupFileName", backupFileName);
                dynamicParameters.Add("@uploadedFileId", uploadedFileId);
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_updateUploadFile]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<SharedLocDestinationServer> GetSharedLocationDestinationServer(string flpConfigurationId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                var dbResponse = await _dapperService.GetSingleRowAsync<SharedLocDestinationServer>("[sel_sharedLocationDestinationServer]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<SharedLocDestinationServer> GetSFTPServerDetailsDestinationServer(string flpConfigurationId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                var dbResponse = await _dapperService.GetSingleRowAsync<SharedLocDestinationServer>("[sel_sharedLocationDestinationServer]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<IEnumerable<ConfigurationTableMapping>> GetConfigurationTableMappings(string flpConfigurationId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                var dbResponse = await _dapperService.GetMultipleRowsAsync<ConfigurationTableMapping>("[sel_configurationTableMapping]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }



    }
}
