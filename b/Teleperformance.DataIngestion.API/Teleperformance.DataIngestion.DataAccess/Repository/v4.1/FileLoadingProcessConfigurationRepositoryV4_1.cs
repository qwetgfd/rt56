using Dapper;
using Microsoft.Extensions.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v4._0.DataBricksAPIJobStatus;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.ValidateFileRules;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v4._1;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using static Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus.StatusResponseDto;


namespace Teleperformance.DataIngestion.DataAccess.Repository.v4._1
{
    public class FileLoadingProcessConfigurationRepositoryV4_1: IFileLoadingProcessConfigurationRepositoryV4_1
    {

        private readonly ILogger<FileLoadingProcessConfigurationRepositoryV4_1> _logger;
        private readonly IDapperService _dapperService;

        public FileLoadingProcessConfigurationRepositoryV4_1(ILogger<FileLoadingProcessConfigurationRepositoryV4_1> logger, IDapperService dapperService)
        {
            this._logger = logger;
            this._dapperService = dapperService;
        }
        public async Task<FlpConfiguration4_1> GetMultisheetConfiguration(string flpConfigurationId,
            string uploadedFileId,string tabName)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@uploadedFileId", uploadedFileId);
                if (!string.IsNullOrWhiteSpace(tabName))
                {
                    dynamicParameters.Add("@tabName", tabName);
                }                
                var result = await _dapperService.GetSingleRowAsync<FlpConfiguration4_1>("[sel_flpMultiSheetConfiguration]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return result;
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
                _logger.LogError(ex, $"{ex.Message} for fileUploadedId {fileUploadedId}");
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
                _logger.LogError(ex,$"{ex.Message} for {flpConfigurationId}");
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
                _logger.LogError(ex, $"{ex.Message} for flpConfigurationId {flpConfigurationId}");
                throw;
            }
        }
        public async Task<DestinationStorageAccount> DatabricksStorageAccountInfo(string flpConfigurationId,string tabName)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                if(!string.IsNullOrWhiteSpace(tabName))
                  dynamicParameters.Add("@tabName", tabName);
                var csvConfiguration = await _dapperService.GetSingleRowAsync<DestinationStorageAccount>("[sel_MultiSheetDatabriksStorageAccount]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return csvConfiguration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }
        public async Task<bool> InsertFileProcessStatus(FileProcessLogHistoryDto logHistory)
        {

            var dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@processId", logHistory.processId);
            dynamicParameters.Add("@tableName", logHistory.tableName);
            dynamicParameters.Add("@messageType", logHistory.messageType);
            dynamicParameters.Add("@message", logHistory.message);
            dynamicParameters.Add("@totalRows", logHistory.totalRows);
            dynamicParameters.Add("@dateTimeReceived", logHistory.dateTimeReceived);
            dynamicParameters.Add("@processName", logHistory.processName);
            dynamicParameters.Add("@loginid", logHistory.loginid);
            dynamicParameters.Add("@fileType", logHistory.fileType);
            dynamicParameters.Add("@flpConfigurationId", logHistory.flpConfigurationId);
            dynamicParameters.Add("@flpFileLogStatusId", logHistory.flpFileLogStatusId);
            dynamicParameters.Add("@activityProcessStatusId", logHistory.activityProcessStatusId);
            dynamicParameters.Add("@fileUploadedId", logHistory.fileUploadedId);
            dynamicParameters.Add("@processTypeId", logHistory.processTypeId);
            dynamicParameters.Add("@databricksAPIResponse", logHistory.databricksAPIResponse);
            dynamicParameters.Add("@tabName", logHistory.tabName);

            var dbResponse = await _dapperService.GetSingleRowAsync<bool>("[commit_flpProcessLogHistory]", dynamicParameters, commandType: CommandType.StoredProcedure);
            return dbResponse;
        }
       

       


        public async Task<DatabaseResponse> AddBackUpFileDetails(string uploadFileId, string flpConfigurationId, string? backupFileName, string? tabName, int totalRows, int insertedRows, int duplicateRows, string blobName)
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
                var dbResponse = await _dapperService.GetSingleRowAsync<UploadFileV4_1>("[commit_backUpFile]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<List<RuleSetV4_1>> GetDIRuleSetByRuleSetNameId(string ruleSetNameId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@ruleSetNameId", ruleSetNameId);

                var dbResponse = await _dapperService.GetMultipleRowsAsync<RuleSetV4_1>("[sel_flpRuleSetsByRuleSetId]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }


        public async Task<DatabaseResponse> AddDatabricksJsonFileColumn(string fileURL,string columns,string flpConfigurationId,string uploadFileId,string tabName)
        {

            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@fileURL", fileURL);
                dynamicParameters.Add("@columns", columns);
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@uploadFileId", uploadFileId);
                dynamicParameters.Add("@tabName", tabName);
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_databricksJsonFileColumn]", dynamicParameters, commandType: CommandType.StoredProcedure);
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
