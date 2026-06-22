using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NPOI.HPSF;
using System.Net;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v4._1;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Enums.v3._0;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using Teleperformance.DataIngestion.Sharepoint.Interfaces.V1_0;
using Teleperformance.DataIngestion.Sharepoint.Models.Request;

namespace Teleperformance.DataIngestion.DataAccess.Services.v1._0
{
    public class FileLoadingProcessConfigurationService : IFileLoadingProcessConfiguration
    {
        private readonly ILogger<FileLoadingProcessConfigurationService> _logger;
        private readonly IFileLoadingProcessConfigurationRepository _fileLoadingProcessConfigurationRepository;
        private ServiceHelper serviceHelper = new ServiceHelper();
        private readonly ISMBLibraryServices _ismbLibraryServices;
        private readonly IProcessConfigurationServiceV4_1 _processConfigurationServiceV4_1;
        private readonly ISharePointPluginService _sharePointPluginService;

        public FileLoadingProcessConfigurationService(ILogger<FileLoadingProcessConfigurationService> logger, IFileLoadingProcessConfigurationRepository fileLoadingProcessConfigurationRepository, ISMBLibraryServices ismbLibraryServices, IProcessConfigurationServiceV4_1 processConfigurationServiceV4_1, ISharePointPluginService sharePointPluginService)
        {
            _logger = logger;
            _fileLoadingProcessConfigurationRepository = fileLoadingProcessConfigurationRepository;
            _ismbLibraryServices = ismbLibraryServices;
            _processConfigurationServiceV4_1 = processConfigurationServiceV4_1;
            _sharePointPluginService = sharePointPluginService;
        }

        public async Task<APIResponse<IEnumerable<FileLoadingConfigurationResponseDto>>> GetProcessList(int processTypeId)
        {
            var flpConfigurations = await _fileLoadingProcessConfigurationRepository.FlpConfigurationDetails(processTypeId);

            if (flpConfigurations == null)
            {
                _logger.LogInformation("No records found in flpConfigurations.");
                return await Task.FromResult(new APIResponse<IEnumerable<FileLoadingConfigurationResponseDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "No records found." },
                    Result = null
                });
            }

            if (!flpConfigurations.Any())
            {

                _logger.LogInformation("No records found in flpConfigurations.");
                return await Task.FromResult(new APIResponse<IEnumerable<FileLoadingConfigurationResponseDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "No records found." },
                    Result = null
                });
            }
            if (flpConfigurations.Any() && string.Compare(flpConfigurations.FirstOrDefault().Result, "Failure", true) == 0)
            {
                _logger.LogError("Return failure from database.");
                return await Task.FromResult(new APIResponse<IEnumerable<FileLoadingConfigurationResponseDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Internal server error" },
                    Result = null
                });
            }
            var flpConfigurationList = new List<FileLoadingConfigurationResponseDto>();

            foreach (var flp in flpConfigurations)
            {
                var flpConfigurationResponseDto = new FileLoadingConfigurationResponseDto
                {
                    FlpConfigurationId = flp.flpConfigurationId,
                    BillingClientName = flp.billing_client_name,
                    SourcePath = flp.sourcePath,
                    DestinationPath = flp.destinationPath,
                    LocationTypeId = flp.locationTypeId,
                    DestinationLocationTypeId = flp.destinationLocationTypeId,
                    LoginId = flp.loginid,
                    ProcessTypeId = flp.processTypeId,
                    ProcessName = flp.process_name,
                    SearchStringInFileName = flp.search_string_in_file_name,
                    SenderCommunicationEmail = flp.sender_communication_email,
                    SupportCommunicationEmail = flp.support_communication_email,
                    SourceContainerName = flp.sourceContainerName,
                    SourceServerName = flp.sourceServerName,

                    SourceStorageAccountKey = flp.sourceStorageAccountKey,
                    SourceStorageAccount = flp.sourceStorageAccount,
                    FileProcessingServerTypeId = flp.fileProcessingServerTypeId,

                    BlobClients = (flp.locationTypeId == (int)SourceLocationTypeEnum.Azure)
                        ? await GetSourceLocationFilesFromBlobStorage(flp.flpConfigurationId, flp.sourceStorageAccount, flp.sourceStorageAccountKey, flp.sourceContainerName, flp.sourcePath, flp.search_string_in_file_name, flp.processTypeId,flp.sasKey,flp.sasKeyToken, flp.securityGroupId)
                        : null,
                    OnPremFileLocations = (flp.locationTypeId == (int)SourceLocationTypeEnum.OnPrem)
                        ? await GetSharedFileLocationList(flp.flpConfigurationId, flp.sourcePath, flp.search_string_in_file_name, flp.serverName, flp.folderName, flp.userName, flp.password, flp.domain, flp.processTypeId, flp.securityGroupId)
                        : null,
                    SharePointFiles = (flp.locationTypeId == (int)SourceLocationTypeEnum.SharePoint)
                        ? await GetSharePointFileLocationList(flp.flpConfigurationId, flp.sharePointApplicationId, flp.sharePointApplicationSiteId, flp.sharePointLibraryName, flp.sharePointFolderPath, flp.search_string_in_file_name, flp.processTypeId)
                        : null
                };
                //Update the process scheduler status
                await _fileLoadingProcessConfigurationRepository.commitProcessScheduler(flp.flpConfigurationId, (int)FlpProcessStatusEnum.Processing);
                flpConfigurationList.Add(flpConfigurationResponseDto);
            }

            //var flpConfigurationArray = await Task.WhenAll(flpConfigurationList);
            return new APIResponse<IEnumerable<FileLoadingConfigurationResponseDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = flpConfigurationList
            };


        }

        public async Task<List<BlobClient>> FindBlobsInFoldersAsync(string storageAccountName, string storageAccountKey,string containerName, string sourcePath, string searchStringInFileName, string sasKey, bool sasToken)
        {
            List<BlobClient> matchingBlobs = new List<BlobClient>();
            BlobContainerClient containerClient = null;
            string blobConnectionString = "";

            // Determine connection method
            if (sasToken)
            {
                // Use SAS token to create container client
                blobConnectionString = FlpConfigurationHelper.GetBlobConnectionStringBySasKey(storageAccountName, sasKey);
                Uri containerUri = new Uri(blobConnectionString);

                // Pass the Uri to the BlobContainerClient constructor
                containerClient = new BlobContainerClient(containerUri);


            }
            else
            {
                // Use connection string
                blobConnectionString = GetBlobConnectionString(storageAccountName, storageAccountKey);
                BlobServiceClient _blobServiceClient = new BlobServiceClient(blobConnectionString);
                // Get the blob container client
                containerClient = _blobServiceClient.GetBlobContainerClient(containerName);




            }

            string blobStorageSourcePath = !string.IsNullOrWhiteSpace(sourcePath)
                ? sourcePath.Substring(0, sourcePath.LastIndexOf('/'))
                : null;


            // Use sourcePath as a prefix for efficient filtering at the server level.
            // This is the most effective way to search within a "folder".
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(prefix: sourcePath))
            {
                _logger.LogInformation($"Evaluating blob: {blobItem.Name} in container: {containerName}");

                // After filtering by prefix, we only need to check the file name search string.
                bool isNameMatch = string.IsNullOrWhiteSpace(searchStringInFileName) ||
                                   blobItem.Name.Contains(searchStringInFileName, StringComparison.OrdinalIgnoreCase);

                if (isNameMatch)
                {
                    BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);
                    if (await blobClient.ExistsAsync())
                    {
                        _logger.LogInformation($"Blob matched and exists: {blobItem.Name}");
                        matchingBlobs.Add(blobClient);
                    }
                }
            }


            //await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            //{
            //    //_logger.LogInformation($"Blob found: {blobItem.Name} in container: {containerName}");

            //    string folderPath = blobItem.Name.Contains("/")
            //        ? blobItem.Name.Substring(0, blobItem.Name.LastIndexOf('/'))
            //        : null;
            //    //_logger.LogInformation($"Blob found#folder path: {folderPath} in container: {containerName}");
            //    bool isPathMatch = !string.IsNullOrWhiteSpace(blobStorageSourcePath) &&
            //                       !string.IsNullOrWhiteSpace(folderPath) &&
            //                       blobStorageSourcePath.Contains(folderPath, StringComparison.OrdinalIgnoreCase);

            //    bool isNameMatch = !string.IsNullOrWhiteSpace(searchStringInFileName) &&
            //                       blobItem.Name.Contains(searchStringInFileName, StringComparison.OrdinalIgnoreCase);

            //    if ((isPathMatch && (isNameMatch || string.IsNullOrWhiteSpace(searchStringInFileName))) ||
            //        (!isPathMatch && isNameMatch) ||
            //        (string.IsNullOrWhiteSpace(blobStorageSourcePath) && string.IsNullOrWhiteSpace(searchStringInFileName)))
            //    {
            //        _logger.LogInformation($"Blob found#folder path#Blob matched: {blobItem.Name} in container: {containerName}");
            //        //matchingBlobs.Add(containerClient.GetBlobClient(blobItem.Name));
            //        BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);
            //        if (await blobClient.ExistsAsync())
            //        {
            //            _logger.LogInformation($"Exist-Blob found#folder path#Blob matched: {blobItem.Name} in container: {containerName}");
            //            matchingBlobs.Add(blobClient);
            //        }
            //    }
            //}

            return matchingBlobs;
        }

        public static string GetBlobConnectionString(string storageAccountName, string storageAccountKey)
        {
            string blobConnectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageAccountKey};EndpointSuffix=core.windows.net"; ;
            return blobConnectionString;

        }



        public async Task<APIResponse<IEnumerable<FileLoadingConfigurationResponseDto>>>GetProcessListToLandingLayer(int processTypeId)
        {
            var flpConfigurations = await _fileLoadingProcessConfigurationRepository.FlpConfigurationDetailsToLandingLayer(processTypeId,(int)FileProcessingServerType.LandingLayer);

            // If no data
            if (flpConfigurations == null || !flpConfigurations.Any())
            {
                _logger.LogInformation("No records found in flpConfigurations.");
                return new APIResponse<IEnumerable<FileLoadingConfigurationResponseDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "No records found." },
                    Result = null
                };
            }

            // If database returned "Failure"
            var firstRecord = flpConfigurations.First();
            if (string.Equals(firstRecord.Result, "Failure", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Return failure from database.");
                return new APIResponse<IEnumerable<FileLoadingConfigurationResponseDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Internal server error" },
                    Result = null
                };
            }

            var flpConfigurationList = new List<FileLoadingConfigurationResponseDto>();

            foreach (var flp in flpConfigurations)
            {
                var dto = new FileLoadingConfigurationResponseDto
                {
                    FlpConfigurationId = flp.flpConfigurationId,
                    BillingClientName = flp.billing_client_name,
                    SourcePath = flp.sourcePath,
                    DestinationPath = flp.destinationPath,
                    LocationTypeId = flp.locationTypeId,
                    DestinationLocationTypeId = flp.destinationLocationTypeId,
                    LoginId = flp.loginid,
                    ProcessTypeId = flp.processTypeId,
                    ProcessName = flp.process_name,
                    SearchStringInFileName = flp.search_string_in_file_name,
                    SenderCommunicationEmail = flp.sender_communication_email,
                    SupportCommunicationEmail = flp.support_communication_email,
                    SourceContainerName = flp.sourceContainerName,
                    SourceServerName = flp.sourceServerName,
                    SourceStorageAccountKey = flp.sourceStorageAccountKey,
                    SourceStorageAccount = flp.sourceStorageAccount,
                    FileProcessingServerTypeId = flp.fileProcessingServerTypeId
                };

                // Fetch file locations               

                switch (flp.locationTypeId)
                {
                    case (int)SourceLocationTypeEnum.Azure:
                        (dto.FileExistInBlob,dto.LandingLayerUploadedFileId) = await FileExistInBlobLocation(
                        flp.flpConfigurationId, flp.sourceStorageAccount, flp.sourceStorageAccountKey,
                        flp.sourceContainerName, flp.sourcePath, flp.search_string_in_file_name,
                        flp.processTypeId, flp.sasKey, flp.sasKeyToken);
                        break;   // <-- Required to prevent syntax error
                    case (int)SourceLocationTypeEnum.OnPrem:
                        (dto.FileExistInRemoteLocation,dto.LandingLayerUploadedFileId) = await FileExistInRemoteLocation(flp.flpConfigurationId,
                            flp.sourcePath, flp.search_string_in_file_name, flp.serverName, flp.folderName,
                            flp.userName, flp.password, flp.domain, flp.processTypeId);
                        break;   // <-- Required to prevent syntax error
                    case (int)SourceLocationTypeEnum.SFTP:
                        (dto.FileExistInSFTPLocation,dto.LandingLayerUploadedFileId) = await FileExistInSFTPFolderLocation(flp.flpConfigurationId,
                            flp.sourcePath, flp.search_string_in_file_name, flp.serverName, flp.folderName,
                            flp.userName, flp.password, flp.domain, flp.processTypeId);
                        break;   // <-- Required to prevent syntax error
                    case (int)SourceLocationTypeEnum.SharePoint:
                        (dto.FileExistInBlob, dto.LandingLayerUploadedFileId) = await FileExistInSharePointLocation(flp.flpConfigurationId,
                            flp.sharePointApplicationId, flp.sharePointApplicationSiteId,
                            flp.sharePointLibraryName, flp.sharePointFolderPath,
                            flp.search_string_in_file_name, flp.processTypeId);
                        break;
                }

                

                // Update process scheduler
                await _fileLoadingProcessConfigurationRepository
                    .commitProcessScheduler(flp.flpConfigurationId,
                                            (int)FlpProcessStatusEnum.Processing);

                flpConfigurationList.Add(dto);
            }

            return new APIResponse<IEnumerable<FileLoadingConfigurationResponseDto>>
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

        public async Task<bool> UpdateProcessSchedulerStatus(string flpConfigurationId, APIResultStatus apiResultStatus)
        {
            //To be changed process attempt environment variable
            if (apiResultStatus.Code == (int)HttpStatusCode.OK)
                await _fileLoadingProcessConfigurationRepository.commitProcessScheduler(flpConfigurationId, (int)FlpProcessStatusEnum.Processed);
            else
                await _fileLoadingProcessConfigurationRepository.commitProcessScheduler(flpConfigurationId, (int)FlpProcessStatusEnum.Error);
            return true;
        }

        public async Task<bool> UpdateFlpProcessStatus(string fileUploadedId, FlpProcessStatusEnum apiResultStatus)
        {
            await _fileLoadingProcessConfigurationRepository.commitFlpConfigProcessStatus(fileUploadedId, (int)apiResultStatus);
            return true;
        }


        public async Task<bool> UpdateProcessSchedulerLastDate(string flpConfigurationId)
        {
            //To be changed process attempt environment variable
             await _fileLoadingProcessConfigurationRepository.commitProcessScheduler(flpConfigurationId, (int)FlpProcessStatusEnum.Skip);
            _logger.LogInformation($"File Not Processed for {flpConfigurationId}:Updated lastRun");
            return true;
        }

        public async Task<bool> IsValidFile(string flpConfigurationId, string blobName, string extention)
        {
            // Extract the extension
            string fileExtention = Path.GetExtension(blobName);
            // Check if the extension is .csv or .txt
            if (fileExtention.Equals(extention, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            _logger.LogError($"Invalid File extention for {flpConfigurationId}");
            return false;
        }

        public async Task<APIResponse<FlpConfigurationResponseDto>> GetFlpProcessByConfigurationId(string flpConfigurationId,string uploadedFileId)
        {
            var flpConfigurations = await _fileLoadingProcessConfigurationRepository.FlpParameterConfigurationByConfigurationId(flpConfigurationId,uploadedFileId);

            if (flpConfigurations == null)
            {
                _logger.LogInformation($"No records found flpConfigurations for {flpConfigurationId}");
                return await Task.FromResult(new APIResponse<FlpConfigurationResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "No records found flpConfigurations" },
                    Result = null
                });
            }


            if (string.Compare(flpConfigurations.Result, "Failure", true) == 0)
            {
                return await Task.FromResult(new APIResponse<FlpConfigurationResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Internal server error" },
                    Result = null
                });
            }
            //Records found 
        var flpConfigurationList = new FlpConfigurationResponseDto
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
                SasKey = flpConfigurations.sasKey,
                SasKeyToken = flpConfigurations.sasKeyToken,
                UIValidation = flpConfigurations.UIValidation,
                BEValidation = flpConfigurations.BEValidation,
                silverTableName = flpConfigurations.silverTableName,
                goldTableName = flpConfigurations.goldTableName,
                SharePointApplicationId = flpConfigurations.sharePointApplicationId,
                SharePointApplicationSiteId = flpConfigurations.sharePointApplicationSiteId,
                SharePointLibraryName = flpConfigurations.sharePointLibraryName,
                SharePointFolderPath = flpConfigurations.sharePointFolderPath




        };
            return new APIResponse<FlpConfigurationResponseDto>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = flpConfigurationList
            };


        }

        public async Task<DestinationStorageAccountDto?> DestinationstorageAccountInfo(string flpConfigurationId)
        {
            DestinationStorageAccountDto destinationStorageAccountDto = null;
            var dbResult = await _fileLoadingProcessConfigurationRepository.DestinationStorageAccountInfo(flpConfigurationId);
            if (dbResult != null)
            {
                //Records found 
                destinationStorageAccountDto = new DestinationStorageAccountDto
                {
                    FlpConfigurationId = dbResult.flpConfigurationId,
                    FlpStorageAccount = dbResult.storageAccountName,
                    StorageAccountKey = dbResult.storageAccountKey,
                    StorageContainerName = dbResult.storageContainerName,
                    SasKey = dbResult.sasKey,
                    SasKeyToken =false// dbResult.sasKeyToken


                };
                return destinationStorageAccountDto;
            }
            if (dbResult == null)
            {
                _logger.LogInformation("No records found.");
            }
            return destinationStorageAccountDto;
        }

        public async Task<DatabricksStorageAccountDto?> DatabricksStorageAccountInfo(string flpConfigurationId)
        {
            DatabricksStorageAccountDto destinationStorageAccountDto = null;
            var dbResult = await _fileLoadingProcessConfigurationRepository.DatabricksStorageAccountInfo(flpConfigurationId);
            if (dbResult != null)
            {
                //Records found 
                destinationStorageAccountDto = new DatabricksStorageAccountDto
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
                _logger.LogInformation($"No records found for {flpConfigurationId}.");
            }
            return destinationStorageAccountDto;
        }

        public async Task<bool> UpdateBackUpFileName(string backupFileName, string fileUploadedId)
        {

            var dbResult = await _fileLoadingProcessConfigurationRepository.UpdateBackUpFileName(backupFileName, fileUploadedId);
            if (string.Compare(dbResult.Result, "Success", true) == 0)
            {
                return true;
            }
            _logger.LogError($"Updating failed, fileName: {backupFileName} and configId: {fileUploadedId}, Error Message:  {dbResult.Message}");
            return false;
        }
        

        //public async Task<bool> AddBackUpFileDetails(string uploadFileId, string flpConfigurationId, string? backupFileName, string? tabName, int totalRows, int insertedRows, int duplicateRows)
        //{

        //    var dbResult = await _fileLoadingProcessConfigurationRepository.AddBackUpFileDetails(uploadFileId, flpConfigurationId, backupFileName, tabName, totalRows, insertedRows, duplicateRows);
        //    if (string.Compare(dbResult.Result, "Success", true) == 0)
        //    {
        //        return true;
        //    }
        //    _logger.LogError($"Updating failed, fileName: {backupFileName} and configId: {uploadFileId}, Error Message:  {dbResult.Message}");
        //    return false;
        //}


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


        public async Task<bool> AddBackUpFileDetails(string uploadFileId, string flpConfigurationId, string? backupFileName, string? tabName, int totalRows, int insertedRows, int duplicateRows, string blobName,string datalakeStorageAccountPath, float? fileSize, string? csvBlobName)
        {

            var dbResult = await _fileLoadingProcessConfigurationRepository.AddBackUpFileDetails(uploadFileId, flpConfigurationId, backupFileName, tabName, totalRows, insertedRows, duplicateRows, blobName, datalakeStorageAccountPath, fileSize, csvBlobName);
            if (string.Compare(dbResult.Result, "Success", true) == 0)
            {
                return true;
            }
            _logger.LogError($"Updating failed, fileName: {backupFileName} and configId: {uploadFileId}, Error Message:  {dbResult.Message}");
            return false;
        }


        public async Task<SharedLocationDestinationServerDto?> SharedLocationDestinationServerDetails(string flpConfigurationId)
        {
            SharedLocationDestinationServerDto destinationServerDto = null;
            var dbResult = await _fileLoadingProcessConfigurationRepository.GetSharedLocationDestinationServer(flpConfigurationId);
            if (dbResult == null)
            {
                _logger.LogInformation("No records found");
            }
            if (string.Compare(dbResult?.Result, "Success", true) == 0)
            {
                if (dbResult != null)
                {
                    //Records found 
                    destinationServerDto = new SharedLocationDestinationServerDto
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


        public async Task<IEnumerable<ConfigurationTableMappingDto?>> ConfigurationTableMapping(string flpConfigurationId)
        {
            var dbResult = await _fileLoadingProcessConfigurationRepository.GetConfigurationTableMappings(flpConfigurationId);
            if (dbResult == null)
            {
                _logger.LogInformation("No records found");
            }

            var result = dbResult?
                        .Select(x => new ConfigurationTableMappingDto
                        {
                            FlpConfigurationId = x.flpConfigurationId,
                            TableName = x.tableName,
                            TabName = x.tabName,
                            ColumnNameList = x.column_name_list,
                            ConvertDataTypeColumnNameList = x.convert_datatypes_column_list,
                            Delimiter = x.delimiter,
                            DoNotArchiveFile = x.do_not_archive_file,
                            DropHistoryTable = x.drop_history_table,
                            DropMainTable = x.drop_main_table,
                            FileNameString = x.fileNameString,
                            FlpTabName = x.flpTabName,
                            IgnoreDuplicateRows = x.ignore_duplicate_rows,
                            IsHeaderProvided = x.is_header_provided,
                            KeepFirstRow = x.keep_first_row,
                            KeyColumnList = x.key_column_list,
                            OrderByColumnListForDedup = x.order_by_column_list_for_dedup,
                            ParquetCompression = x.parquet_compression,
                            QuoteCharacter = x.quote_character,
                            SkipFooterRows = x.skip_footer_rows,
                            SkipRows = x.skip_rows,
                            ValidateFileSchema = x.validate_fileschema,
                            DatabaseConnectionSecret = x.databaseConnectionSecret,
                            SpanishToEnglish = x.spanishToEnglish,
                            OrdinalToRoman = x.romanNumeralsOnly,
                            SkipEmptyLines = x.skipEmptyLines,
                            mergeData = x.mergeData,
                            createHistoryTable= x.createHistoryTable,
                            historyTableName = x.historyTableName,
                            UnityCatalog = x.unityCatalog,
                            campaignId = x.campaignId
                            

                        }).ToList();

            return result;
        }


        //Private Functions
        private async Task<List<BlobClientDetails>> GetSourceLocationFilesFromBlobStorage(string flpConfigurationId, string storageAccountName, string storageAccountKey, string containerName, string sourcePath, string searchStringInFileName, int processTypeId,string sasKey,bool sasKeyToken, string securityGroupId)
        {
            List<BlobClientDetails> list = new List<BlobClientDetails>();
            try
            {
                
                var fileList = await serviceHelper.FindBlobsInFoldersAsync(storageAccountName, storageAccountKey, containerName, sourcePath, searchStringInFileName,sasKey,sasKeyToken);

                //Need to check filelist and skip the file
                if (fileList != null)
                {
                    foreach (var file in fileList)
                    {
                        BlobClientDetails blobClientDetails = new BlobClientDetails();
                        string fileName = Path.GetFileName(file.Name);
                        var isValidFile = await IsValidExtention(flpConfigurationId, fileName);
                        if (!isValidFile)
                        {
                            continue;
                        }
                        var success = await checkFileUploadedStatus(flpConfigurationId, fileName);
                        if (success)
                        {


                            //Data to be inserted in the table 
                            (bool res, string uploadedId) = await UploadedFileDetails(flpConfigurationId, fileName, processTypeId);
                            if (res)
                            {
                                blobClientDetails.Uri = file.Uri.ToString();
                                blobClientDetails.AccountName = file.AccountName;
                                blobClientDetails.BlobContainerName = file.BlobContainerName;
                                blobClientDetails.Name = file.Name;
                                blobClientDetails.CanGenerateSasUri = file.CanGenerateSasUri;
                                blobClientDetails.UploadedId = uploadedId;
                                list.Add(blobClientDetails);
                                //Any file that is in processing will have a seprate entry in table
                                var properties = await file.GetPropertiesAsync();
                                var fileSizeInBytes = properties?.Value?.ContentLength ?? 0;

                                LogUploadedFileRequest logUploadedFileRequest = new LogUploadedFileRequest
                                {
                                    fileName = file.Name,
                                    fileSize = fileSizeInBytes,
                                    uploadedDateTime = DateTime.UtcNow.ToString(),
                                    uploadedBy = "Function App",
                                    flpConfigurationId = flpConfigurationId,
                                    uploadFileId = uploadedId,
                                    securityGroupId = securityGroupId
                                };
                                var res2 = await _processConfigurationServiceV4_1.LogUploadedFile(logUploadedFileRequest);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

                _logger.LogError($"Error : {ex.Message.ToString()} in GetSourceLocationFilesFromBlobStorage() for {flpConfigurationId}");
                list = new List<BlobClientDetails>();
            }
            return list;
        }

        private async Task<List<OnPremFileLocation>> GetSharedFileLocationList(string flpConfigurationId, string sourcePath, string searchStringInFileName, string serverName, string folderName, string userName, string password, string domain, int processTypeId,string securityGroupId)
        {
            List<OnPremFileLocation> onPremFiles = new List<OnPremFileLocation>();
            try
            {
               
                var onPremLocation = true;// Convert.ToBoolean(KeyVault.GetKeyVaultValue("OnPremLocation").Result);
                if (onPremLocation)
                {

                    if (!FlpConfigurationHelper.ValidString(serverName, folderName, userName, sourcePath, password, domain))
                    {
                        _logger.LogError("Incomplete source server configuration.");
                        return onPremFiles;
                    }
                    //var dataIngestionOnPremDomainName = Convert.ToString(Environment.GetEnvironmentVariable("DataIngestionOnPremDomainName"));
                    CheckConnectivitySMBLibraryModel model = new CheckConnectivitySMBLibraryModel();
                    model.serverIP = serverName;
                    model.sharedFolderName = folderName;
                    model.sharedFolderPath = $@"{sourcePath}";
                    model.username = userName;
                    model.password = password;
                    model.domain = domain;
                    model.fileName = searchStringInFileName;



                    var result = _ismbLibraryServices.SMBRequest(model, flpConfigurationId, SMBRequestEnum.FileListFromLocation);

                    if (result != null && result.SharedFileLocations != null)
                    {
                        foreach (var file in result?.SharedFileLocations)
                        {
                            var isValidFile = await IsValidExtention(flpConfigurationId, file.FilePath);
                            if (!isValidFile)
                            {
                                continue;
                            }
                            OnPremFileLocation onPremFileLocation = new OnPremFileLocation();
                            //onPremFiles                    
                            var success = await checkFileUploadedStatus(flpConfigurationId, file.FileName);
                            if (success)
                            {
                                //Data to be inserted in the table 
                                (bool res, string uploadedId) = await UploadedFileDetails(flpConfigurationId, file.FileName, processTypeId);
                                if (res)
                                {
                                    onPremFileLocation.UploadedId = uploadedId;
                                    onPremFileLocation.FileUrl = file.FilePath;
                                    onPremFileLocation.FileSize = file.FileSize;
                                    onPremFiles.Add(onPremFileLocation);
                                    LogUploadedFileRequest logUploadedFileRequest = new LogUploadedFileRequest
                                    {
                                        fileName = file.FileName,
                                        fileSize = file.FileSize,
                                        uploadedDateTime = DateTime.UtcNow.ToString(),
                                        uploadedBy = "Function App",
                                        flpConfigurationId = flpConfigurationId,
                                        uploadFileId = uploadedId,
                                        securityGroupId = securityGroupId
                                    };
                                    var res2 = await _processConfigurationServiceV4_1.LogUploadedFile(logUploadedFileRequest);
                                }
                            }

                        }

                    }

                }
                else
                {
                    //Local machine path
                    if (Directory.Exists(sourcePath))
                    {
                        string searchPattern = $"*{searchStringInFileName}*"; // Pattern to match files containing the search string
                        var files = Directory.GetFiles(sourcePath, searchPattern);

                        // Filter files for case-sensitive match
                        var caseSensitiveFiles = files
                            .Where(f => Path.GetFileName(f).Contains(searchStringInFileName))
                            .ToList();

                        foreach (var file in caseSensitiveFiles)
                        {
                            OnPremFileLocation onPremFileLocation = new OnPremFileLocation();
                            //onPremFiles
                            string fileName = Path.GetFileName(file);
                            var success = await checkFileUploadedStatus(flpConfigurationId, fileName);
                            if (success)
                            {
                                //Data to be inserted in the table 
                                (bool res, string uploadedId) = await UploadedFileDetails(flpConfigurationId, fileName, processTypeId);
                                if (res)
                                {
                                    onPremFileLocation.UploadedId = uploadedId;
                                    onPremFileLocation.FileUrl = file;
                                    onPremFiles.Add(onPremFileLocation);
                                }
                            }

                        }
                    }

                    return onPremFiles;
                }
            }
            catch (Exception ex)
            {

                _logger.LogError($"Error : {ex.Message.ToString()} in GetSharedFileLocationList() for {flpConfigurationId}");
                onPremFiles = new List<OnPremFileLocation>();

            }


            return onPremFiles;
        }
        //private async Task<bool> FileExistInFolderLocation(string flpConfigurationId,string sourcePath, string searchStringInFileName, string serverName, string folderName, string userName, string password, string domain)
        //{
        //    bool isAnyFileExist = false;
        //    try
        //    {
        //        string host = "";
        //        int port = 0;
        //        string username = "";
        //         password = "";
        //        string remoteDirectory = sourcePath;

        //        using var sftp = new SftpHelper(host, port, username, password);
        //        {
        //            isAnyFileExist = sftp.AnyFileExists(remoteDirectory);

        //        }


        //    }
        //    catch (Exception ex)
        //    {

        //        _logger.LogError($"Error : {ex.Message.ToString()} in FileExistInFolderLocation() for {flpConfigurationId}");
        //        isAnyFileExist = false;

        //    }
        //    return isAnyFileExist;
        //}

        private async Task<(bool, string)> FileExistInSFTPFolderLocation(
                            string flpConfigurationId,
                            string sourcePath,
                            string searchStringInFileName,
                            string serverName,
                            string folderName,
                            string userName,
                            string password,
                            string domain, // not used for SFTP unless you prefix it to username
                            int processTypeId,
                            CancellationToken cancellationToken = default)
        {
            bool isAnyFileExist = false;
            string uploadedFileId = "";

            try
            {
                var success = await checkLandingLayerFileUploadedStatus(flpConfigurationId);
                if (success)
                {
                    // Build connection inputs
                    // If you have a non-default port, add it as parameter; otherwise default to 22.
                    string host = serverName?.Trim();
                    int port = 22;

                    // SFTP typically ignores Windows domain; if needed, include it in the username as "domain\username".
                    string username = string.IsNullOrWhiteSpace(domain)
                        ? userName
                        : $"{domain}\\{userName}";

                    // Build remote directory. If folderName is a subfolder, combine with sourcePath.
                    // Ensure POSIX separators for SFTP.
                    string remoteDirectory = string.IsNullOrWhiteSpace(folderName)
                        ? sourcePath
                        : $"{sourcePath.TrimEnd('/')}/{folderName.TrimStart('/')}";

                    // Normalize leading slash if your server requires absolute paths
                    // remoteDirectory = remoteDirectory.StartsWith("/") ? remoteDirectory : "/" + remoteDirectory;

                    using var sftp = new SftpHelper(host, port, username, password);

                    // If you want to match specific filenames, pass a simple glob like "*.csv" or "prefix_*"
                    // If searchStringInFileName is empty, we just check if any file exists in the directory.
                    string pattern = string.IsNullOrWhiteSpace(searchStringInFileName)
                        ? null
                        : searchStringInFileName;

                    isAnyFileExist = await sftp.AnyFileExistsAsync(
                        remoteDirectory: remoteDirectory,
                        searchPattern: pattern,
                        excludeHiddenDotFiles: true,
                        cancellationToken: cancellationToken
                    ).ConfigureAwait(false);
                    if (isAnyFileExist)
                    {
                        (bool res, uploadedFileId) = await UploadedFileDetails(flpConfigurationId, "Landing Layer File", processTypeId);
                        if (res)
                        {
                            isAnyFileExist = true;
                            // return (true, uploadedId);
                        }
                    }
                }

                return (false, "");


                // return (false, "");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Cancelled: FileExistInFolderLocation() for {FlpConfigurationId} (server={Server}, path={Path})",
                    flpConfigurationId, serverName, sourcePath);
                isAnyFileExist = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error: {Message} in FileExistInFolderLocation() for {FlpConfigurationId} (server={Server}, path={Path})",
                    ex.Message, flpConfigurationId, serverName, sourcePath);
                isAnyFileExist = false;
            }

            return (isAnyFileExist, uploadedFileId); 
        }


        private async Task<(bool,string)> FileExistInBlobLocation(string flpConfigurationId, string storageAccountName, string storageAccountKey, string containerName, string sourcePath, string searchStringInFileName, int processTypeId, string sasKey, bool sasKeyToken)
        {           
            try
            {
                var success = await checkLandingLayerFileUploadedStatus(flpConfigurationId);
                if (success)
                {
                   // var fileList = await serviceHelper.FindBlobsInFoldersAsync(storageAccountName, storageAccountKey, containerName, sourcePath, searchStringInFileName, sasKey, sasKeyToken);
                  //  var fileList = await FindBlobsInFoldersAsync(storageAccountName, storageAccountKey, containerName, sourcePath, searchStringInFileName, sasKey, sasKeyToken);

                    //Need 
                     var fileExistInBlob = await serviceHelper.AnyBlobExistsAsync(storageAccountName, storageAccountKey, containerName, sourcePath, searchStringInFileName, sasKey, sasKeyToken);
                    if (fileExistInBlob)
                    {
                        _logger.LogInformation($"Blob Exist for flpConfigurationId:{flpConfigurationId} .");
                        (bool res, string uploadedId) = await UploadedFileDetails(flpConfigurationId, "Landing Layer File", processTypeId);
                        if (res)
                        {
                            return (true, uploadedId);
                        }
                    }
                    _logger.LogInformation($"Blob Not Exist for flpConfigurationId:{flpConfigurationId} .");
                }               
                return (false, "");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error : {ex.Message.ToString()} in FileExistInBlobLocation() for {flpConfigurationId}");
                return (false, ""); 
            }
            
        }

        private async Task<(bool, string)> FileExistInRemoteLocation(string flpConfigurationId, string sourcePath, string searchStringInFileName, string serverName, string folderName, string userName, string password, string domain,int processTypeId)
        {
           
            try
            {
                

                var success = await checkLandingLayerFileUploadedStatus(flpConfigurationId);
                if (success)
                {
                    if (!FlpConfigurationHelper.ValidString(serverName, folderName, userName, sourcePath, password, domain))
                    {
                        _logger.LogError("Incomplete source server configuration.");
                        return (false, "");
                    }
                    //var dataIngestionOnPremDomainName = Convert.ToString(Environment.GetEnvironmentVariable("DataIngestionOnPremDomainName"));
                    CheckConnectivitySMBLibraryModel model = new CheckConnectivitySMBLibraryModel();
                    model.serverIP = serverName;
                    model.sharedFolderName = folderName;
                    model.sharedFolderPath = $@"{sourcePath}";
                    model.username = userName;
                    model.password = password;
                    model.domain = domain;
                    model.fileName = searchStringInFileName;                    
                    var result = await _ismbLibraryServices.FileExistsInRemoteLocation(model, flpConfigurationId);
                    if (result)
                    {
                        (bool res, string uploadedId) = await UploadedFileDetails(flpConfigurationId, "Landing Layer File", processTypeId);
                        if (res)
                        {
                            return (true, uploadedId);
                        }
                    }
                    
                }
                
                return (false,"");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error : {ex.Message.ToString()} in GetSharedFileLocationList() for {flpConfigurationId}");
                return (false, "");

            }           
        }

        private async Task<bool> IsValidExtention(string flpConfigrationId, string fileName)
        {
            var isValidCsvFile = await IsValidFile(flpConfigrationId, fileName, ".csv");
            var isValidTxtFile = await IsValidFile(flpConfigrationId, fileName, ".txt");
            var isValidExcelFile1 = await IsValidFile(flpConfigrationId, fileName, ".xlsx");
            var isValidExcelFile2 = await IsValidFile(flpConfigrationId, fileName, ".xls");
            var isValidExcelFile3 = await IsValidFile(flpConfigrationId, fileName, ".xlsb");
            if (!(isValidCsvFile || isValidTxtFile || isValidExcelFile1 || isValidExcelFile2|| isValidExcelFile3))
            {
                _logger.LogError($"Invalid file found {fileName} for {flpConfigrationId}");
                return false;
            }
            return true;

        }
        private async Task<bool> checkFileUploadedStatus(string flpConfigurationId, string fileName)
        {
            var dbResult = await _fileLoadingProcessConfigurationRepository.checkFileStatus(flpConfigurationId, fileName);
            if (string.Compare(dbResult.Result, "Success", true) == 0)
            {
                return true;
            }
            _logger.LogError($"Invalid file or Filename check failed.");
            return false;
        }


        private async Task<bool> checkLandingLayerFileUploadedStatus(string flpConfigurationId)
        {
            var dbResult = await _fileLoadingProcessConfigurationRepository.checkLandingFileStatus(flpConfigurationId);
            if (string.Compare(dbResult.Result, "Success", true) == 0)
            {
                return true;
            }
            _logger.LogError($"Invalid file or Filename check failed.");
            return false;
        }

        public async Task<(bool success, string uploadedFileId)> UploadedFileDetails(string flpConfigurationId, string fileName, int processTypeId)
        {
            var dbResult = await _fileLoadingProcessConfigurationRepository.commitFileUploadByFunctionApp(fileName, flpConfigurationId, "", "Function app", processTypeId);
            if (string.Compare(dbResult.Result, "Success", true) == 0)
            {
                return (true, dbResult.uploadFileId);
            }
            _logger.LogError($"Failed in finding uploaded file details.");
            return (false, null);
        }

        #region SharePoint Workspace - AY

        private async Task<List<SharePointFileLocation>> GetSharePointFileLocationList(
            string flpConfigurationId,
            Guid? sharePointApplicationId,
            Guid? sharePointApplicationSiteId,
            string? sharePointLibraryName,
            string? sharePointFolderPath,
            string searchStringInFileName,
            int processTypeId)
        {
            var sharePointFiles = new List<SharePointFileLocation>();
            try
            {
                if (!sharePointApplicationId.HasValue || sharePointApplicationId.Value == Guid.Empty)
                {
                    _logger.LogWarning("SharePoint ApplicationId is not configured for {FlpConfigurationId}", flpConfigurationId);
                    return sharePointFiles;
                }

                var credentials = SharePointCredentials(sharePointApplicationId.Value, sharePointLibraryName);
                string browsePath = !string.IsNullOrWhiteSpace(sharePointFolderPath) ? sharePointFolderPath.Trim() : "/";

                var items = await _sharePointPluginService.BrowseFolderAsync(credentials, browsePath);

                if (items == null || items.Count == 0)
                {
                    _logger.LogInformation("No files found in SharePoint for {FlpConfigurationId}", flpConfigurationId);
                    return sharePointFiles;
                }

                foreach (var item in items)
                {
                    if (item.IsFolder)
                        continue;

                    var isValidFile = await IsValidExtention(flpConfigurationId, item.Name);
                    if (!isValidFile)
                        continue;

                    if (!string.IsNullOrWhiteSpace(searchStringInFileName)
                        && !item.Name.Contains(searchStringInFileName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var alreadyUploaded = await checkFileUploadedStatus(flpConfigurationId, item.Name);
                    if (alreadyUploaded)
                        continue;

                    (bool success, string uploadedId) = await UploadedFileDetails(flpConfigurationId, item.Name, processTypeId);
                    if (success)
                    {
                        sharePointFiles.Add(new SharePointFileLocation
                        {
                            UploadedId = uploadedId,
                            FileUrl = item.Path ?? item.WebUrl ?? item.Name,
                            FileSize = item.Size
                        });
                    }
                }

                return sharePointFiles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSharePointFileLocationList() for {FlpConfigurationId}", flpConfigurationId);
                return sharePointFiles;
            }
        }

        private async Task<(bool fileExists, string uploadedFileId)> FileExistInSharePointLocation(
            string flpConfigurationId,
            Guid? sharePointApplicationId,
            Guid? sharePointApplicationSiteId,
            string? sharePointLibraryName,
            string? sharePointFolderPath,
            string searchStringInFileName,
            int processTypeId)
        {
            try
            {
                if (!sharePointApplicationId.HasValue || sharePointApplicationId.Value == Guid.Empty)
                {
                    _logger.LogWarning("SharePoint ApplicationId is not configured for landing-layer check on {FlpConfigurationId}", flpConfigurationId);
                    return (false, "");
                }

                var credentials = SharePointCredentials(sharePointApplicationId.Value, sharePointLibraryName);
                string browsePath = !string.IsNullOrWhiteSpace(sharePointFolderPath) ? sharePointFolderPath.Trim() : "/";

                var items = await _sharePointPluginService.BrowseFolderAsync(credentials, browsePath);

                if (items == null || items.Count == 0)
                    return (false, "");

                foreach (var item in items)
                {
                    if (item.IsFolder)
                        continue;

                    var isValidFile = await IsValidExtention(flpConfigurationId, item.Name);
                    if (!isValidFile)
                        continue;

                    if (!string.IsNullOrWhiteSpace(searchStringInFileName)
                        && !item.Name.Contains(searchStringInFileName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    (bool success, string uploadedId) = await UploadedFileDetails(flpConfigurationId, "Landing Layer File", processTypeId);
                    if (success)
                        return (true, uploadedId);

                    return (true, "");
                }

                return (false, "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FileExistInSharePointLocation() for {FlpConfigurationId}", flpConfigurationId);
                return (false, "");
            }
        }

        private static WorkspaceCredentials SharePointCredentials(Guid applicationId, string? libraryName)
        {
            return new WorkspaceCredentials
            {
                ApplicationId = applicationId,
                SiteName = null,
                LibraryName = !string.IsNullOrWhiteSpace(libraryName) ? libraryName.Trim() : null
            };
        }

        #endregion
    }
}
