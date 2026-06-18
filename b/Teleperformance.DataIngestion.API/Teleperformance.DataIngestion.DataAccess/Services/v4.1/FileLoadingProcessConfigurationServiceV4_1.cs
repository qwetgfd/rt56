using Dapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._0;
using Teleperformance.DataIngestion.Models.DTOs.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Enums.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Services.v4._1
{
    public class FileLoadingProcessConfigurationServiceV4_1 : IFileLoadingProcessConfigurationServiceV4_1
    {
        private readonly ILogger<FileLoadingProcessConfigurationServiceV4_1> _logger;
        private readonly IFileLoadingProcessConfigurationRepositoryV4_1 _fileLoadingProcessConfigurationRepository;
        public FileLoadingProcessConfigurationServiceV4_1(ILogger<FileLoadingProcessConfigurationServiceV4_1> logger,IFileLoadingProcessConfigurationRepositoryV4_1 fileLoadingProcessConfigurationRepository)
        {
            _logger = logger;
            _fileLoadingProcessConfigurationRepository = fileLoadingProcessConfigurationRepository;
        }

        public async Task<APIResponse<FlpConfigurationResponseDtoV4_1>> GetMultisheetConfiguration(string flpConfigurationId, string uploadedFileId,string tabName)
        {
            var flpConfigurations = await _fileLoadingProcessConfigurationRepository.GetMultisheetConfiguration(flpConfigurationId, uploadedFileId, tabName);

            if (flpConfigurations == null)
            {
                _logger.LogInformation($"No records found flpConfigurations for {flpConfigurationId}");
                return await Task.FromResult(new APIResponse<FlpConfigurationResponseDtoV4_1>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "No records found flpConfigurations" },
                    Result = null
                });
            }


            if (string.Compare(flpConfigurations.Result, "Failure", true) == 0)
            {
                return await Task.FromResult(new APIResponse<FlpConfigurationResponseDtoV4_1>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Internal server error" },
                    Result = null
                });
            }
            //Records found 
            var flpConfigurationList = new FlpConfigurationResponseDtoV4_1
            {
                FlpConfigurationId = flpConfigurations.flpConfigurationId,
                BillingClientName = flpConfigurations.billing_client_name,
                SourcePath = flpConfigurations.sourcePath,
                DestinationPath = flpConfigurations.destinationPath,
                LocationTypeId = flpConfigurations.locationTypeId,
                DestinationLocationTypeId = flpConfigurations.destinationLocationTypeId,
                LoginId = flpConfigurations.loginid,
                ProcessTypeId = flpConfigurations.processTypeId,
                ProcessName = flpConfigurations.process_name,
                SearchStringInFileName = flpConfigurations.search_string_in_file_name,
                SenderCommunicationEmail = flpConfigurations.sender_communication_email,
                SupportCommunicationEmail = flpConfigurations.support_communication_email,
                SourceContainerName = flpConfigurations.sourceContainerName,
                SourceServerName = flpConfigurations.serverName,
                SourceStorageAccountKey = flpConfigurations.sourceStorageAccountKey,
                SourceStorageAccount = flpConfigurations.sourceStorageAccount,
                SourceAppFolder = flpConfigurations.folderName,
                SourceUserName = flpConfigurations.userName,
                SourcePassword = flpConfigurations.password,
                SourceDomain = flpConfigurations.domain,
                UploadedFileName = flpConfigurations.uploadedFileName,
                DetalakeStorageAccountPath = flpConfigurations.datalakeStorageAccountPath,
                DataBricksAPIToken = flpConfigurations.databricksAPIToken,
                DataBricksAPIVersion = flpConfigurations.databricksAPIVersion,
                DatabricksInstance = flpConfigurations.databricksInstance,
                ProcessModified = flpConfigurations.processModified,
                JobId = flpConfigurations.jobId,
                TabName = flpConfigurations.tabName,
                DatabaseConnectionSecret = flpConfigurations.databaseConnectionSecret,
                ParquetCompression = flpConfigurations.parquet_compression,
                HistoryTableName = flpConfigurations.historyTableName,
                UnityCatalog = flpConfigurations.unityCatalog,
                SasKey = flpConfigurations.sasKey,
                SasKeyToken = flpConfigurations.sasKeyToken,
                UIValidation = flpConfigurations.UIValidation,
                BEValidation = flpConfigurations.BEValidation,
                campaignId = flpConfigurations.campaignId

            };
            return new APIResponse<FlpConfigurationResponseDtoV4_1>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = flpConfigurationList
            };


        }


        public async Task<bool> UpdateFlpProcessStatus(string fileUploadedId, APIResultStatus apiResultStatus)
        {
            //To be changed process attempt environment variable
            if (apiResultStatus.Code == (int)HttpStatusCode.OK)
              await _fileLoadingProcessConfigurationRepository.commitFlpConfigProcessStatus(fileUploadedId, (int)FlpProcessStatusEnum.Processed);
            else
                await _fileLoadingProcessConfigurationRepository.commitFlpConfigProcessStatus(fileUploadedId, (int)FlpProcessStatusEnum.Error);
            return true;
        }


        public async Task<bool> UpdateProcessStatus(string fileUploadedId, int flpProcessStatusId)
        {
            await _fileLoadingProcessConfigurationRepository.commitFlpConfigProcessStatus(fileUploadedId, flpProcessStatusId);
            return true;
        }

        public async Task<DestinationStorageAccountDtoV4_1?> DestinationstorageAccountInfo(string flpConfigurationId)
        {
            DestinationStorageAccountDtoV4_1 destinationStorageAccountDto = null;
            var dbResult = await _fileLoadingProcessConfigurationRepository.DestinationStorageAccountInfo(flpConfigurationId);
            if (dbResult != null)
            {
                //Records found 
                destinationStorageAccountDto = new DestinationStorageAccountDtoV4_1
                {
                    FlpConfigurationId = dbResult.flpConfigurationId,
                    FlpStorageAccount = dbResult.storageAccountName,
                    StorageAccountKey = dbResult.storageAccountKey,
                    StorageContainerName = dbResult.storageContainerName,
                    SasKey = dbResult.sasKey
                };            
                return destinationStorageAccountDto;
            }
            if (dbResult == null)
            {
                _logger.LogError($"Not found destination storage account for flpConfigurationId {flpConfigurationId}");
            }
            return destinationStorageAccountDto;
        }

        public async Task<SharedLocationDestinationServerDtoV4_1?> SharedLocationDestinationServerDetails(string flpConfigurationId)
        {
            SharedLocationDestinationServerDtoV4_1 destinationServerDto = null;
            var dbResult = await _fileLoadingProcessConfigurationRepository.GetSharedLocationDestinationServer(flpConfigurationId);
            if (dbResult == null)
            {
                _logger.LogError($"Not found SharedLocationDestinationServer records found for flpConfigurationId {flpConfigurationId}");
            }
            if (string.Compare(dbResult?.Result, "Success", true) == 0)
            {
                if (dbResult != null)
                {
                    //Records found 
                    destinationServerDto = new SharedLocationDestinationServerDtoV4_1
                    {
                        ServerName = dbResult.serverName,
                        FolderName = dbResult.folderName,
                        UserName = dbResult.userName,
                        Password = dbResult.password,
                        Domain = dbResult.domain
                    };
                    return destinationServerDto;
                }
            }


            return destinationServerDto;
        }
        public async Task<bool> AddBackUpFileDetails(string uploadFileId, string flpConfigurationId, string? backupFileName, string? tabName, int totalRows, int insertedRows, int duplicateRows, string blobName)
        {

            var dbResult = await _fileLoadingProcessConfigurationRepository.AddBackUpFileDetails(uploadFileId, flpConfigurationId, backupFileName, tabName, totalRows, insertedRows, duplicateRows, blobName);
            if (string.Compare(dbResult.Result, "Success", true) == 0)
            {
                return true;
            }
            _logger.LogError($"Updating failed, fileName: {backupFileName} and configId: {uploadFileId}, Error Message:  {dbResult.Message}");
            return false;
        }

        public async Task<DatabricksStorageAccountDto4_1?> DatabricksStorageAccountInfo(string flpConfigurationId, string tabName)
        {
            DatabricksStorageAccountDto4_1 destinationStorageAccountDto = null;
            var dbResult = await _fileLoadingProcessConfigurationRepository.DatabricksStorageAccountInfo(flpConfigurationId, tabName);
            if (dbResult != null)
            {
                //Records found 
                destinationStorageAccountDto = new DatabricksStorageAccountDto4_1
                {
                    FlpConfigurationId = dbResult.flpConfigurationId,
                    FlpStorageAccount = dbResult.storageAccountName,
                    StorageAccountKey = dbResult.storageAccountKey,
                    StorageContainerName = dbResult.storageContainerName,
                    SasKey = dbResult.sasKey,
                    SasKeyToken = dbResult.sasKeyToken

                };
                return destinationStorageAccountDto;
            }
            if (dbResult == null)
            {
                _logger.LogInformation("No records found.");
            }
            return destinationStorageAccountDto;
        }


        public async Task<APIResponse<List<RuleSetDtoV4_1>>> GetDIRuleSetByRuleSetNameId(string ruleSetNameId)
        {
            var DIFlpRuleSet = await _fileLoadingProcessConfigurationRepository.GetDIRuleSetByRuleSetNameId(ruleSetNameId);
            if (DIFlpRuleSet == null)
            {
                return new APIResponse<List<RuleSetDtoV4_1>>
                {
                    ResultStatus = APIResultStatus.NoContent,
                    ResponseMessage = ["No Rule Set found."]
                };
            }

            return new APIResponse<List<RuleSetDtoV4_1>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = DIFlpRuleSet.Select(r => new RuleSetDtoV4_1
                {
                    id = r.id,
                    ruleTypeId = r.ruleTypeId,
                    subRuleId = r.subRuleId,
                    ruleColumnNameRaw = r.ruleColumnNameRaw,
                    ruleDescription = r.ruleDescription,
                    prompt = r.prompt,
                    format = r.format,
                    patternId = r.patternId,
                    isCombinationRule = r.isCombinationRule,
                    fromValue = r.fromValue,
                    toValue = r.toValue,
                    conditionId = r.conditionId
                }).ToList()
            };

        }







    }
}
