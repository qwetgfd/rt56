using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingProcess.TableSchemaValidation;
using Teleperformance.DataIngestion.Common;
using Azure;
using Teleperformance.DataIngestion.Sharepoint.Interfaces.V1_0;
using Teleperformance.DataIngestion.Sharepoint.Models.Request;
using Teleperformance.DataIngestion.Sharepoint.Models.Response;
namespace Teleperformance.DataIngestion.DataAccess.Services.v1._0
{
    public class FileProcessingService: IFileProcessingService
    {
        private readonly IFileLoadingProcessRepository _ifileLoadingProcessRepository;
        private readonly ISchemaValidationService _iTableSchemaValidationService;
        private readonly IBronzeDbRepository _iInsertedDataToBronzeDbRepository;
        private readonly ILogger<FileProcessingService> _logger;
        private readonly IBlobStorageService _iBlobStorageService;
        private readonly ISMBLibraryServices _iISMBLibraryServices;
        private readonly ISharePointPluginService _sharePointPluginService;

        public FileProcessingService(IFileLoadingProcessRepository ifileLoadingProcessRepository, ILogger<FileProcessingService> logger, ISchemaValidationService iTableSchemaValidationService, IBronzeDbRepository iInsertedDataToBronzeDbRepository, IBlobStorageService iBlobStorageService, ISMBLibraryServices iISMBLibraryServices, ISharePointPluginService sharePointPluginService)
        {
            _ifileLoadingProcessRepository = ifileLoadingProcessRepository;
            _logger = logger;
            _iTableSchemaValidationService = iTableSchemaValidationService;
            _iInsertedDataToBronzeDbRepository = iInsertedDataToBronzeDbRepository;
            _iBlobStorageService = iBlobStorageService;
            _iISMBLibraryServices = iISMBLibraryServices;
            _sharePointPluginService = sharePointPluginService;
        }

        public async Task<APIResponse<FlpProcessResponseDto>> ParquetFileProcessToBronzeTable(long processId, string fileType, string fileLocation,string? tabName, string connectionString, FlpActivityLogStatusEnum currentStatus, FlpProcessTempFile flpProcessTempFile, ParquetFileResponseDto resultResponse,ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto)
        {
            FlpProcessResponseDto flpConvertToParquetResponseDto = new FlpProcessResponseDto();
            flpConvertToParquetResponseDto.TotalRows = resultResponse.TotalRows;
            flpConvertToParquetResponseDto.DuplicateRows = resultResponse.DuplicateRows;
            flpConvertToParquetResponseDto.BlobName = flpProcessTempFile.Name;
            

            
                await AddFileProcessLosStatus(
                     fileType: fileType,
                     loginId: "",
                     message: $"File conversion process completed",
                messageType: "info",
                     processId: processId,
                     processName: flpConfigurationRequestDto.ProcessName,
                     tableName: configurationTableMappingDto.TableName,
                     totalRows: resultResponse.TotalRows,
                     flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                     fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                     FileStatusActivityEnum.ProcessCompleted,
                     FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation
                );

                if (configurationTableMappingDto.DropMainTable)
                {
                    //currentStatus = FlpActivityLogStatusEnum.DroppedMainTable;
                    

                    await AddFileProcessLosStatus(
                           fileType: fileType,
                           loginId: "",
                           message: $"Dropping Main table in processing:{configurationTableMappingDto.TableName}",
                           messageType: "info",
                           processId: processId,
                           processName: flpConfigurationRequestDto.ProcessName,
                           tableName: configurationTableMappingDto.TableName,
                           totalRows: resultResponse.TotalRows,
                           flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                           fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                           FileStatusActivityEnum.Processing,
                           FlpActivityLogStatusEnum.DroppedMainTable
                      );
                    (bool retMainTable, bool retHistoryTable) = await _ifileLoadingProcessRepository.dropMainTableAndHistoryTable(configurationTableMappingDto.DropMainTable, false, configurationTableMappingDto.TableName, "", connectionString);
                    if (retMainTable)
                    {
                        await AddFileProcessLosStatus(
                               fileType: fileType,
                               loginId: "",
                               message: $"Dropped Main table: {configurationTableMappingDto.TableName}",
                               messageType: "info",
                               processId: processId,
                               processName: flpConfigurationRequestDto.ProcessName,
                               tableName: configurationTableMappingDto.TableName,
                               totalRows: resultResponse.TotalRows,
                               flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                               fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                               FileStatusActivityEnum.ProcessCompleted,
                               FlpActivityLogStatusEnum.DroppedMainTable
                          );
                    }
                    else
                    {
                        await AddFileProcessLosStatus(
                            fileType: fileType,
                            loginId: "",
                            message: $"Error in dropping Main table: {configurationTableMappingDto.TableName}",
                            messageType: "error",
                            processId: processId,
                            processName: flpConfigurationRequestDto.ProcessName,
                            tableName: configurationTableMappingDto.TableName,
                            totalRows: resultResponse.TotalRows,
                            flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                            fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                            FileStatusActivityEnum.Error,
                            FlpActivityLogStatusEnum.DroppedMainTable
                        );

                        _logger.LogError($"Error in dropping Main table: {configurationTableMappingDto.TableName}");
                        //return
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { "Something went wrong" },
                            Result = flpConvertToParquetResponseDto
                        });
                    }

                }
                else
                {
                    await AddFileProcessLosStatus(
                       fileType: fileType,
                       loginId: "",
                       message: $"Not dropped main table: {configurationTableMappingDto.TableName}",
                       messageType: "info",
                       processId: processId,
                       processName: flpConfigurationRequestDto.ProcessName,
                       tableName: configurationTableMappingDto.TableName,
                       totalRows: resultResponse.TotalRows,
                       flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                       fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                       FileStatusActivityEnum.Skip,
                       FlpActivityLogStatusEnum.DroppedMainTable
                   );
                }
            //currentStatus = FlpActivityLogStatusEnum.FileSchemaValidated;

             await AddFileProcessLosStatus(
                   fileType: fileType,
                   loginId: "",
                   message: $"File schema is validatation {configurationTableMappingDto.TableName}",
                   messageType: "info",
                   processId: processId,
                   processName: flpConfigurationRequestDto.ProcessName,
                   tableName: configurationTableMappingDto.TableName,
                   totalRows: resultResponse.TotalRows,
                   flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                   fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                   FileStatusActivityEnum.Processing,
                   FlpActivityLogStatusEnum.FileSchemaValidated
               );
             MappingTableSchemaResult mappingTableSchemaResponse = null;
             if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.OnPrem)
             {
                 mappingTableSchemaResponse = await _iTableSchemaValidationService.CreateBronzeTableFromSharedLocation(resultResponse.ParquetFilePath, connectionString, flpConfigurationRequestDto.ProcessName, configurationTableMappingDto.TableName, flpConfigurationRequestDto.FlpConfigurationId, tabName, slDestinationServerDto,resultResponse);
             }
             else
             {
                 mappingTableSchemaResponse = await _iTableSchemaValidationService.CreateBronzeTableFromBlob(resultResponse.ParquetBlobClient, connectionString, flpConfigurationRequestDto.ProcessName, configurationTableMappingDto.TableName, flpConfigurationRequestDto.FlpConfigurationId, tabName,resultResponse);
             }
             if (mappingTableSchemaResponse != null && mappingTableSchemaResponse.MatchSchema)
             {
                 await AddFileProcessLosStatus(
                     fileType: fileType,
                     loginId: "",
                     message: $"File schema is validated {configurationTableMappingDto.TableName}",
                     messageType: "info",
                     processId: processId,
                     processName: flpConfigurationRequestDto.ProcessName,
                     tableName: configurationTableMappingDto.TableName,
                     totalRows: resultResponse.TotalRows,
                     flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                     fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                     FileStatusActivityEnum.ProcessCompleted,
                     FlpActivityLogStatusEnum.FileSchemaValidated
                 );
             }
             else
             {
                 await AddFileProcessLosStatus(
                     fileType: fileType,
                     loginId: "",
                     message: $"Error:{mappingTableSchemaResponse?.ErrorMessage??"Error occurred in table schema validation"}",
                     messageType: "error",
                     processId: processId,
                     processName: flpConfigurationRequestDto.ProcessName,
                     tableName: configurationTableMappingDto.TableName,
                     totalRows: resultResponse.TotalRows,
                     flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                     fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                     FileStatusActivityEnum.Error,
                      FlpActivityLogStatusEnum.FileSchemaValidated
                 );

                 _logger.LogError($"Error mappingTableSchemaResponse.MatchSchema is failed: {configurationTableMappingDto.TableName}");
                 //return
                 return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                 {
                     ResultStatus = APIResultStatus.InvalidParameters,
                     ResponseMessage = new List<string> { "Something went wrong" },
                     Result = null
                 });

             }
            // var retDropMainTable = await _dataRepository.dropMainTableAndHistoryTable(flpParametersConfiguration.ParamDropMainTable, false, flpParametersConfiguration.ParamTableName, "");

            await AddFileProcessLosStatus(
                      fileType: fileType,
                      loginId: "",
                      message: $"Inserting data in table {configurationTableMappingDto.TableName}",
                      messageType: "info",
                      processId: processId,
                      processName: flpConfigurationRequestDto.ProcessName,
                      tableName: configurationTableMappingDto.TableName,
                      totalRows: resultResponse.TotalRows,
                      flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                      fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                      FileStatusActivityEnum.Processing,
                      FlpActivityLogStatusEnum.DataInsertedToBronzeTable
                  );

                bool insertedData = false;
                int totalInsertedRows = 0;
                if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.OnPrem)
                { 
                  // (insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataFromParquetSharedLocationAsync(resultResponse.ParquetFilePath,
                  //                            connectionString, configurationTableMappingDto.TableName.Trim(), flpConfigurationRequestDto.FlpConfigurationId, slDestinationServerDto);
                  //flpConvertToParquetResponseDto.InsertedRows = totalInsertedRows;
                }
                else
                {
                //(insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataFromParquetStreamV2(resultResponse.ParquetBlobClient, flpConfigurationRequestDto.FlpConfigurationId,
                //                               connectionString, configurationTableMappingDto.TableName.Trim());
                  flpConvertToParquetResponseDto.InsertedRows = totalInsertedRows;
                }
                if (insertedData)
                {
                    await AddFileProcessLosStatus(
                      fileType: fileType,
                      loginId: "",
                      message: $"Success: Inserting data in table {configurationTableMappingDto.TableName}",
                      messageType: "info",
                      processId: processId,
                      processName: flpConfigurationRequestDto.ProcessName,
                      tableName: configurationTableMappingDto.TableName,
                      totalRows: resultResponse.TotalRows,
                      flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                      fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                      FileStatusActivityEnum.ProcessCompleted,
                      FlpActivityLogStatusEnum.DataInsertedToBronzeTable
                  );

                }
                else
                {
                    await AddFileProcessLosStatus(
                     fileType: fileType,
                     loginId: "",
                     message: $"Error: Inserting data in table {configurationTableMappingDto.TableName}",
                     messageType: "error",
                     processId: processId,
                     processName: flpConfigurationRequestDto.ProcessName,
                     tableName: configurationTableMappingDto.TableName,
                     totalRows: resultResponse.TotalRows,
                     flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                     fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                     FileStatusActivityEnum.Error,
                     FlpActivityLogStatusEnum.DataInsertedToBronzeTable
                 );

                   // _logger.LogError($"Error Inserting data in table : {flpConfigurationRequestDto.TableName}");

                    //return
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Something went wrong" },
                        Result = flpConvertToParquetResponseDto
                    });


                }
                //do_not_acrhive false
                if (!configurationTableMappingDto.DoNotArchiveFile)
                {
                    //currentStatus = FlpActivityLogStatusEnum.ParquetFileArchived;
                    //Moving folder into archive
                    var flpLogHistory = new FileProcessLogHistoryDto()
                    {
                        dateTimeReceived = DateTime.UtcNow,
                        fileType = fileType,
                        loginid = "",
                        message = "",
                        messageType = "",
                        processId = processId,
                        processName = flpConfigurationRequestDto.ProcessName,
                        tableName = configurationTableMappingDto.TableName,
                        totalRows = resultResponse.TotalRows,
                        fileUploadedId = flpConfigurationRequestDto.UploadedFileId,
                        flpConfigurationId = flpConfigurationRequestDto.FlpConfigurationId
                    };
                    //(bool retMovedFile, string msg) = await FlpConfigurationMethods.MoveParquetToArchiveAndDeletedMainFile1(flpLogHistory, resultResponse.ParquetBlobClient, flpProcessTempFile.ParquetBlobConnectionString, flpProcessTempFile.BlobContainerName, _activityLoggerRepository);
                    (bool retMovedFile, string response) = await MoveParquetToArchiveAndDeletedMainFileV2(flpConfigurationRequestDto.DestinationLocationTypeId ?? 0,
                        resultResponse.ParquetFilePath, resultResponse.ParquetBlobClient, flpProcessTempFile.ParquetBlobConnectionString,
                        flpProcessTempFile.BlobContainerName, flpLogHistory, slDestinationServerDto, _iISMBLibraryServices);
                    if (retMovedFile)
                    {
                        flpConvertToParquetResponseDto.BackUpFileName = response;
                    }
                    else
                    {
                        _logger.LogError($"Error: Parquet file moving into Archived folder for {flpConfigurationRequestDto.FlpConfigurationId}");
                        //return
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { response },
                            Result = flpConvertToParquetResponseDto
                        });

                    }

                }
                else
                {
                    await AddFileProcessLosStatus(
                        fileType: fileType,
                        loginId: "",
                        message: $"Not moved file in archived location",
                        messageType: "info",
                        processId: processId,
                        processName: flpConfigurationRequestDto.ProcessName,
                        tableName: configurationTableMappingDto.TableName,
                        totalRows: resultResponse.TotalRows,
                        flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        FileStatusActivityEnum.Skip,
                        FlpActivityLogStatusEnum.ParquetFileArchived
                    );
                }

                //Deleting parquet file path 
                (bool fileDelteed, string parquetFileResponse) = await DeleteParquetFileFromParquetLocation(processId, fileType, resultResponse,configurationTableMappingDto, flpConfigurationRequestDto, slDestinationServerDto);
                if (!fileDelteed)
                {
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { parquetFileResponse },
                        Result = flpConvertToParquetResponseDto
                    });
                }

                //Deleting temp file path
                (bool tempFileDelteed, string tempFileResponse) = await DeleteTempFileFromTemptLocation(processId, fileType, flpProcessTempFile, resultResponse, configurationTableMappingDto,flpConfigurationRequestDto, slDestinationServerDto);
                if (!tempFileDelteed)
                {
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { tempFileResponse },
                        Result = flpConvertToParquetResponseDto
                    });
                }

           
            
            return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Process Completed" },
                Result = flpConvertToParquetResponseDto
            });
        }

        public async Task<(FlpProcessTempFile?, bool)> MoveSourceFileToTemporaryDestinationAndDelete(long processId, string fileType, string fileLocation, string fileUploadedId, string backUpFileName, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto)
        {
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();
            //source
            //Adding Log
            FlpActivityLogStatusEnum currentStatus = FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation;
            CheckConnectivitySMBLibraryModel sourceServer = null;
            try
            {
                currentStatus = FlpActivityLogStatusEnum.FileMovedToTempStorage;
                
                await AddFileProcessLosStatus(
                        fileType: fileType,
                        loginId: "",
                        message: $"Moving File into temp folder from location: {fileLocation}",
                        messageType: "info",
                        processId: processId,
                        processName: flpConfigurationRequestDto.ProcessName,
                        tableName: configurationTableMappingDto.TableName,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        fileUploadedId: fileUploadedId,
                        FileStatusActivityEnum.Processing,
                        FlpActivityLogStatusEnum.FileMovedToTempStorage
                    );
                bool isCopiedFile = false;
                BlobClient sourceBlobClient = null;
                string sourceUrl = "";
                string relativePath = "";
                if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure &&
                    flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                {
                    string sourceBlobUrl = flpConfigurationRequestDto.BlobClients.Uri;
                    string sourceBlobName = flpConfigurationRequestDto.BlobClients.Name;
                    string sourceBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(flpConfigurationRequestDto.SourceStorageAccount, flpConfigurationRequestDto.SourceStorageAccountKey);
                    string sourceBlobContainer = flpConfigurationRequestDto.SourceContainerName;

                    sourceBlobClient = _iBlobStorageService.GetBlobClientDetails(flpConfigurationRequestDto.BlobClients.Name, sourceBlobConnectionString, sourceBlobContainer);

                    //destination
                    string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey);
                    string destinationContainerName = destinationStorageAccountDto.StorageContainerName;
                    string destinationBlobUrl = $"{flpConfigurationRequestDto.DestinationPath}temp/"; //Moved file into temporary storage
                    string destinationBlobName = $"{destinationBlobUrl}{backUpFileName}";// {sourceBlobClient.Name.Split('/').Last()}";

                    BlobClient destinationBlobClient = _iBlobStorageService.GetBlobClientDetails(destinationBlobName, destinationBlobConnectionString, destinationContainerName);
                    (isCopiedFile, flpProcessTempFile) = await _iBlobStorageService.CopyFileSourceBlobToDestinationBlobAsync(sourceBlobClient, destinationBlobClient);
                    flpProcessTempFile.AccountName = destinationStorageAccountDto.FlpStorageAccount;
                    flpProcessTempFile.BlobContainerName = destinationStorageAccountDto.StorageContainerName;
                    flpProcessTempFile.ParquetBlobConnectionString = destinationBlobConnectionString;
                    // flpProcessTempFile.SourceBlobConnectionString = sourceBlobConnectionString;
                    flpProcessTempFile.DestinationFolder = $"{flpConfigurationRequestDto.DestinationPath}parquet/";
                }
                else if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure &&
                   flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.OnPrem)
                {
                    //File move from blob to onprem
                    string sourceBlobUrl = flpConfigurationRequestDto.BlobClients.Uri;
                    string sourceBlobName = flpConfigurationRequestDto.BlobClients.Name;
                    string sourceBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(flpConfigurationRequestDto.SourceStorageAccount, flpConfigurationRequestDto.SourceStorageAccountKey);
                    string sourceBlobContainer = flpConfigurationRequestDto.SourceContainerName;

                    sourceBlobClient = _iBlobStorageService.GetBlobClientDetails(flpConfigurationRequestDto.BlobClients.Name, sourceBlobConnectionString, sourceBlobContainer);

                    //destination
                    string currentTimeString = DateTime.Now.ToString("yyyyMMdd");
                    string onPremFilePath = flpConfigurationRequestDto.DestinationPath;
                    string destinationUrl = @$"{flpConfigurationRequestDto.DestinationPath}temp\{currentTimeString}";
                    string tempFilePath = Path.Combine(destinationUrl, backUpFileName);
                     (isCopiedFile, flpProcessTempFile) = FlpConfigurationHelper.CopyFileBlobToOnPremAsync(tempFilePath, flpConfigurationRequestDto.FlpConfigurationId, sourceBlobClient, slDestinationServerDto,_iISMBLibraryServices);
                    var onPremLocation = true;// Convert.ToBoolean(KeyVault.GetKeyVaultValue("OnPremLocation").Result);
                    if (onPremLocation)
                    {
                        (isCopiedFile, flpProcessTempFile) = FlpConfigurationHelper.CopyFileBlobToOnPremAsync(tempFilePath, flpConfigurationRequestDto.FlpConfigurationId, sourceBlobClient, slDestinationServerDto,_iISMBLibraryServices);
                    }
                    else
                    {
                        (isCopiedFile, flpProcessTempFile) = await _iBlobStorageService.CopyFileBlobToOnPremAsync(tempFilePath, sourceBlobClient);
                    }

                    flpProcessTempFile.DestinationFolder = @$"{flpConfigurationRequestDto.DestinationPath}\parquet\";
                    //flpProcessTempFile.sourceTempFilePath = $"{destinationUrl}";

                }
                else if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.OnPrem &&
                  flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.OnPrem)
                {
                    sourceUrl = $"{flpConfigurationRequestDto.SourcePath}";
                    string destinationUrl = @$"{flpConfigurationRequestDto.DestinationPath}temp\"; //Moved file into temporary storage
                   // var dataIngestionOnPremDomainName = Convert.ToString(Environment.GetEnvironmentVariable("DataIngestionOnPremDomainName"));
                    sourceServer = new CheckConnectivitySMBLibraryModel();
                    sourceServer.serverIP = flpConfigurationRequestDto.SourceServerName;
                    sourceServer.username = flpConfigurationRequestDto.SourceUserName;
                    sourceServer.password = flpConfigurationRequestDto.SourcePassword;
                    sourceServer.sharedFolderName = flpConfigurationRequestDto.SourceAppFolder;
                    sourceServer.domain = flpConfigurationRequestDto.SourceDomain;

                    string tempFilePath = Path.Combine(sourceServer.serverIP, sourceServer.sharedFolderName);
                    // To get the relative path from the app folder
                    relativePath = sourceUrl.Substring(tempFilePath.Length).TrimStart(Path.DirectorySeparatorChar);

                    (isCopiedFile, string response) = await FlpConfigurationHelper.CopySourceOnPremFileToDestinationOnPrem(flpConfigurationRequestDto.FlpConfigurationId, relativePath, destinationUrl, backUpFileName, sourceServer, slDestinationServerDto, _iISMBLibraryServices);
                    if (isCopiedFile)
                    {
                        flpProcessTempFile.DestinationFolder = @$"{flpConfigurationRequestDto.DestinationPath}";
                        flpProcessTempFile.sourceTempFilePath = $"{response}";
                    }

                }
                else if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.OnPrem &&
                    flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                {
                    //OnPrem to azure
                    string currentTimeString = DateTime.Now.ToString("yyyyMMdd");
                    sourceUrl = $"{flpConfigurationRequestDto.SourcePath}";
                    //destination
                    string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey);
                    string destinationContainerName = destinationStorageAccountDto.StorageContainerName;
                    string destinationBlobUrl = $"{flpConfigurationRequestDto.DestinationPath}temp/{currentTimeString}/";
                    string destinationBlobName = $"{destinationBlobUrl}{backUpFileName}";

                    BlobClient destinationBlobClient = _iBlobStorageService.GetBlobClientDetails(destinationBlobName, destinationBlobConnectionString, destinationContainerName);

                    var onPremLocation = true;// Convert.ToBoolean(KeyVault.GetKeyVaultValue("OnPremLocation").Result);
                    if (onPremLocation)
                    {
                        string tempFilePath = Path.Combine(flpConfigurationRequestDto.SourceServerName, flpConfigurationRequestDto.SourceAppFolder);
                        // To get the relative path from the app folder
                        relativePath = sourceUrl.Substring(tempFilePath.Length).TrimStart(Path.DirectorySeparatorChar);
                       // var dataIngestionOnPremDomainName = Convert.ToString(Environment.GetEnvironmentVariable("DataIngestionOnPremDomainName"));
                        sourceServer = new CheckConnectivitySMBLibraryModel();
                        sourceServer.serverIP = flpConfigurationRequestDto.SourceServerName;
                        sourceServer.username = flpConfigurationRequestDto.SourceUserName;
                        sourceServer.password = flpConfigurationRequestDto.SourcePassword;
                        sourceServer.sharedFolderName = flpConfigurationRequestDto.SourceAppFolder;
                        sourceServer.sourceFilePath = relativePath;
                        sourceServer.domain = flpConfigurationRequestDto.SourceDomain;

                        (isCopiedFile, flpProcessTempFile) = await FlpConfigurationHelper.CopyFileOnPremToDestinationBlobAsync(sourceUrl, flpConfigurationRequestDto.FlpConfigurationId, destinationBlobClient, sourceServer, _iISMBLibraryServices);
                    }
                    else
                    {
                        (isCopiedFile, flpProcessTempFile) = await new BlobStorageService().CopyFileOnPremToDestinationBlobAsync(sourceUrl, destinationBlobClient);//sourceBlobClient, destinationBlobClient);
                    }

                    flpProcessTempFile.AccountName = destinationStorageAccountDto.FlpStorageAccount;
                    flpProcessTempFile.BlobContainerName = destinationStorageAccountDto.StorageContainerName;
                    flpProcessTempFile.ParquetBlobConnectionString = destinationBlobConnectionString;
                    // flpProcessTempFile.SourceBlobConnectionString = sourceBlobConnectionString;
                    flpProcessTempFile.DestinationFolder = @$"{flpConfigurationRequestDto.DestinationPath}/parquet/";
                }
                else if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.SharePoint &&
                    flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                {
                    #region SharePoint Workspace - AY
                    var fileContent = await FetchSharePointFileAsync(flpConfigurationRequestDto);
                    if (fileContent == null)
                        return (null, false);

                    string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(
                        destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey);
                    string destinationContainerName = destinationStorageAccountDto.StorageContainerName;
                    string destinationBlobUrl = $"{flpConfigurationRequestDto.DestinationPath}temp/";
                    string destinationBlobName = $"{destinationBlobUrl}{backUpFileName}";

                    BlobClient destinationBlobClient = _iBlobStorageService.GetBlobClientDetails(
                        destinationBlobName, destinationBlobConnectionString, destinationContainerName);

                    await destinationBlobClient.UploadAsync(fileContent.Content, overwrite: true);

                    isCopiedFile = true;
                    flpProcessTempFile.AccountName = destinationStorageAccountDto.FlpStorageAccount;
                    flpProcessTempFile.BlobContainerName = destinationStorageAccountDto.StorageContainerName;
                    flpProcessTempFile.ParquetBlobConnectionString = destinationBlobConnectionString;
                    flpProcessTempFile.DestinationFolder = $"{flpConfigurationRequestDto.DestinationPath}parquet/";
                    #endregion
                }
                else if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.SharePoint &&
                    flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.OnPrem)
                {
                    #region SharePoint Workspace - AY
                    var fileContent = await FetchSharePointFileAsync(flpConfigurationRequestDto);
                    if (fileContent == null)
                        return (null, false);

                    string currentTimeString = DateTime.Now.ToString("yyyyMMdd");
                    string destinationUrl = @$"{flpConfigurationRequestDto.DestinationPath}temp\{currentTimeString}";
                    string tempFilePath = Path.Combine(destinationUrl, backUpFileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath));
                    using (var fileStream = File.Create(tempFilePath))
                    {
                        await fileContent.Content.CopyToAsync(fileStream);
                    }

                    isCopiedFile = true;
                    flpProcessTempFile.DestinationFolder = @$"{flpConfigurationRequestDto.DestinationPath}\parquet\";
                    #endregion
                }

                //(flpProcessTempFile, BlobClient sourceBlobClient) = await new BlobStorageService().MovedFileToTempAsync(sourceBlobUrl, sourceBlobName, sourceBlobConnectionString, sourceBlobContainer, destinationBlobConnectionString, destinationContainerName, destinationBlobUrl);
                if (isCopiedFile && flpProcessTempFile != null)
                {

                    await AddFileProcessLosStatus(
                            fileType: fileType,
                            loginId: "",
                            message: $"Moved File into destination temp folder from location {fileLocation}",
                            messageType: "info",
                            processId: processId,
                            processName: flpConfigurationRequestDto.ProcessName,
                            tableName: configurationTableMappingDto.TableName,
                            totalRows: 0,
                            flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                            fileUploadedId: fileUploadedId,
                            FileStatusActivityEnum.ProcessCompleted,
                            FlpActivityLogStatusEnum.FileMovedToTempStorage
                        );

                    if (flpProcessTempFile != null)
                    {
                        currentStatus = FlpActivityLogStatusEnum.DeletedFileFromMainLocation;
       

                        await AddFileProcessLosStatus(
                                 fileType: fileType,
                                 loginId: "",
                                 message: $"started deleting from location:{fileLocation}",
                                 messageType: "info",
                                 processId: processId,
                                 processName: flpConfigurationRequestDto.ProcessName,
                                 tableName: configurationTableMappingDto.TableName,
                                 totalRows: 0,
                                 flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                 fileUploadedId: fileUploadedId,
                                 FileStatusActivityEnum.Processing,
                                 FlpActivityLogStatusEnum.DeletedFileFromMainLocation
                             );


                        var isDeleted = false;
                        string responseMsg = "";

                        if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure)
                        {
                            isDeleted = await _iBlobStorageService.DeleteBlobAsync(sourceBlobClient);
                        }
                        else if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.OnPrem)
                        {
                            var onPremLocation = true;// Convert.ToBoolean(KeyVault.GetKeyVaultValue("OnPremLocation").Result);
                            if (onPremLocation)
                            {
                                sourceServer.sourceFilePath = relativePath;
                                var result = _iISMBLibraryServices.SMBRequest(sourceServer, flpConfigurationRequestDto.FlpConfigurationId, SMBRequestEnum.DeleteFile);
                                if (result.FileIsDeleted)
                                {
                                    isDeleted = true;
                                    responseMsg = "DeletedFile";
                                }
                                else
                                {
                                    isDeleted = false;
                                    responseMsg = "something went wrong";
                                }
                            }
                            else
                            {
                                (isDeleted, responseMsg) = await FlpConfigurationHelper.DeleteFile(sourceUrl);
                            }

                        }
                        else if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.SharePoint)
                        {
                            #region SharePoint Workspace - AY
                            _logger.LogInformation("SharePoint source files are managed by SharePoint retention. " +
                                "Skipping physical delete for {FlpConfigurationId}, file {FileLocation}",
                                flpConfigurationRequestDto.FlpConfigurationId, fileLocation);
                            isDeleted = true;
                            responseMsg = "DeletedFile";
                            #endregion
                        }

                        if (isDeleted)
                        {
                            await AddFileProcessLosStatus(
                                   fileType: fileType,
                                   loginId: "",
                                   message: $"deleted from location:{fileLocation}",
                                   messageType: "info",
                                   processId: processId,
                                   processName: flpConfigurationRequestDto.ProcessName,
                                   tableName: configurationTableMappingDto.TableName,
                                   totalRows: 0,
                                   flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                   fileUploadedId: fileUploadedId,
                                   FileStatusActivityEnum.ProcessCompleted,
                                   FlpActivityLogStatusEnum.DeletedFileFromMainLocation
                               );

                        }
                        else
                        {
                            await AddFileProcessLosStatus(
                                    fileType: fileType,
                                    loginId: "",
                                    message: $"Error: Not deleted from location:{fileLocation}",
                                    messageType: "error",
                                    processId: processId,
                                    processName: flpConfigurationRequestDto.ProcessName,
                                    tableName: configurationTableMappingDto.TableName,
                                    totalRows: 0,
                                    flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                    fileUploadedId: fileUploadedId,
                                    FileStatusActivityEnum.Error,
                                    FlpActivityLogStatusEnum.DeletedFileFromMainLocation
                                );
                            _logger.LogError($"Error: Not deleted from location:{fileLocation}", $"{responseMsg}");
                            return (flpProcessTempFile, false);

                        }


                    }
                }
                else
                {
                    await AddFileProcessLosStatus(
                    fileType: fileType,
                    loginId: "",
                    message: $"Error: File moving  failed to templorary storage",
                    messageType: "error",
                    processId: processId,
                    processName: flpConfigurationRequestDto.ProcessName,
                    tableName: configurationTableMappingDto.TableName,
                    totalRows: 0,
                    flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                    fileUploadedId: fileUploadedId,
                    FileStatusActivityEnum.Error,
                    FlpActivityLogStatusEnum.FileMovedToTempStorage);
                    _logger.LogError($"Error: File moving  failed to templorary storage for {flpConfigurationRequestDto.FlpConfigurationId}", "flpProcessTempFile is null");
                    return (null, false);
                }
                return (flpProcessTempFile, true);
            }
            catch (Exception ex)
            {
                await AddFileProcessLosStatus(
                                    fileType: fileType,
                                    loginId: "",
                                    message: $"Error: File moving  failed to templorary storage",
                                    messageType: "error",
                                    processId: processId,
                                    processName: flpConfigurationRequestDto.ProcessName,
                                    tableName: configurationTableMappingDto.TableName,
                                    totalRows: 0,
                                    flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                    fileUploadedId: fileUploadedId,
                                    FileStatusActivityEnum.Error,
                                    currentStatus
                                );

                _logger.LogError($"Error: File moving  failed to templorary storage for {flpConfigurationRequestDto.FlpConfigurationId}", ex.Message.ToString());
                return (null, false);
            }
        }



        public  async Task<(bool, string)> MoveParquetToArchiveAndDeletedMainFileV2(int destinationLocationTypeId, string parquetFilePath, BlobClient bcParquetSourceLocation, string blobConnectionString, string destinationContainerName, FileProcessLogHistoryDto fileLogHistory, SharedLocationDestinationServerDto slDestinationServerDto, ISMBLibraryServices _ismbLibraryServices)
        {
            string currentTimeString = DateTime.Now.ToString("yyyyMMdd");
            string directoryPath = ""; // Gets the directory part of the path
            string destinationFolderPath = "";

            if (destinationLocationTypeId == (int)DestinationLocationTypeEnum.OnPrem)
            {
                directoryPath = Path.GetDirectoryName(parquetFilePath);
                destinationFolderPath = $"{directoryPath}/Archive/{currentTimeString}";
            }
            else
            {
                directoryPath = Path.GetDirectoryName(bcParquetSourceLocation.Name).Replace("\\", "/");
                destinationFolderPath = $"{directoryPath}/Archive/{currentTimeString}";
            }
            try
            {

                //await AddFileProcessLosStatus(
                //          fileType: fileLogHistory.fileType,
                //          loginId: fileLogHistory.loginid,
                //          message: $"Parquet file moving into Archived folder: {destinationFolderPath}",
                //          messageType: "info",
                //          processId: fileLogHistory.processId,
                //          processName: fileLogHistory.processName,
                //          tableName: fileLogHistory.tableName,
                //          totalRows: fileLogHistory.totalRows,
                //          flpConfigurationId: fileLogHistory.flpConfigurationId,
                //          fileUploadedId: fileLogHistory.fileUploadedId,
                //          FileStatusActivityEnum.ProcessStarted,
                //          FlpActivityLogStatusEnum.ParquetFileArchived
                //      );
                await AddFileProcessLosStatus(
                         fileType: fileLogHistory.fileType,
                         loginId: fileLogHistory.loginid,
                         message: $"Parquet file moving into Archived folder: {destinationFolderPath}",
                         messageType: "info",
                         processId: fileLogHistory.processId,
                         processName: fileLogHistory.processName,
                         tableName: fileLogHistory.tableName,
                         totalRows: fileLogHistory.totalRows,
                         flpConfigurationId: fileLogHistory.flpConfigurationId,
                         fileUploadedId: fileLogHistory.fileUploadedId,
                         FileStatusActivityEnum.Processing,
                         FlpActivityLogStatusEnum.ParquetFileArchived
                     );
                string archivedParquetFilePath = "";
                if (destinationLocationTypeId == (int)DestinationLocationTypeEnum.OnPrem)
                {
                    archivedParquetFilePath = Path.Combine(destinationFolderPath, Path.GetFileName(parquetFilePath));
                    var onPremLocation = true; // Convert.ToBoolean(KeyVault.GetKeyVaultValue("OnPremLocation").Result);
                    if (onPremLocation)
                    {
                        string archivedFilePath = archivedParquetFilePath.Replace("/", "\\");
                        CheckConnectivitySMBLibraryModel parquetFileModel = new CheckConnectivitySMBLibraryModel
                        {
                            serverIP = slDestinationServerDto.ServerName,
                            username = slDestinationServerDto.UserName,
                            password = slDestinationServerDto.Password,
                            sharedFolderName = slDestinationServerDto.FolderName,
                            domain = slDestinationServerDto.Domain,
                            sourceFilePath = parquetFilePath,
                            destinationFilePath = archivedFilePath
                        };

                        var result = _ismbLibraryServices.SMBRequest(parquetFileModel, fileLogHistory.flpConfigurationId, SMBRequestEnum.CopiedFileToArchivedFolder);

                        if (result.CopiedFileToArchivedFolder)
                        {

                            await AddFileProcessLosStatus(
                                fileType: fileLogHistory.fileType,
                                loginId: fileLogHistory.loginid,
                                message: $"Parquet file moving into Archived folder: {destinationFolderPath}",
                                messageType: "info",
                                processId: fileLogHistory.processId,
                                processName: fileLogHistory.processName,
                                tableName: fileLogHistory.tableName,
                                totalRows: fileLogHistory.totalRows,
                                flpConfigurationId: fileLogHistory.flpConfigurationId,
                                fileUploadedId: fileLogHistory.fileUploadedId,
                                FileStatusActivityEnum.ProcessCompleted,
                                FlpActivityLogStatusEnum.ParquetFileArchived);
                            //Deleting source file
                            string retArchivedFilePath = Path.Combine(parquetFileModel.serverIP, parquetFileModel.sharedFolderName, archivedFilePath);
                            return (true, retArchivedFilePath);
                        }
                        else
                        {
                            await AddFileProcessLosStatus(
                                fileType: fileLogHistory.fileType,
                                loginId: fileLogHistory.loginid,
                                message: $"Parquet file moving into Archived folder: {destinationFolderPath}",
                                messageType: "info",
                                processId: fileLogHistory.processId,
                                processName: fileLogHistory.processName,
                                tableName: fileLogHistory.tableName,
                                totalRows: fileLogHistory.totalRows,
                                flpConfigurationId: fileLogHistory.flpConfigurationId,
                                fileUploadedId: fileLogHistory.fileUploadedId,
                                FileStatusActivityEnum.ProcessCompleted,
                                FlpActivityLogStatusEnum.ParquetFileArchived);
                            //Deleting source file
                            return (false, "something went wrong");
                        }
                    }
                    else
                    {
                        // Create archive folder if it doesn't exist
                        if (!Directory.Exists(destinationFolderPath))
                        {
                            Directory.CreateDirectory(destinationFolderPath);
                        }
                        //Move parquet file to archive folder

                        File.Copy(parquetFilePath, archivedParquetFilePath, true);
                    }

                    await AddFileProcessLosStatus(
                             fileType: fileLogHistory.fileType,
                             loginId: fileLogHistory.loginid,
                             message: $"Parquet file moving into Archived folder: {destinationFolderPath}",
                             messageType: "info",
                             processId: fileLogHistory.processId,
                             processName: fileLogHistory.processName,
                             tableName: fileLogHistory.tableName,
                             totalRows: fileLogHistory.totalRows,
                             flpConfigurationId: fileLogHistory.flpConfigurationId,
                             fileUploadedId: fileLogHistory.fileUploadedId,
                             FileStatusActivityEnum.ProcessCompleted,
                             FlpActivityLogStatusEnum.ParquetFileArchived
                         );
                }
                else
                {
                    // Parse the source blob URL
                    // Initialize the BlobServiceClient for the destination
                    BlobServiceClient destinationBlobServiceClient = new BlobServiceClient(blobConnectionString);
                    string destinationBlobName = $"{destinationFolderPath}/{bcParquetSourceLocation.Name.Split('/').Last()}";
                    // Get the destination container client
                    BlobContainerClient destinationContainerClient = destinationBlobServiceClient.GetBlobContainerClient(destinationContainerName);
                    // Get a reference to the destination blob
                    BlobClient destinationBlob = destinationContainerClient.GetBlobClient(destinationBlobName);
                    if (bcParquetSourceLocation != null)
                    {
                        (bool ret, var flpProcessTempFile) = await new BlobStorageService().CopyFileSourceBlobToDestinationBlobAsync(bcParquetSourceLocation, destinationBlob);

                        if (ret)
                        {
                            await AddFileProcessLosStatus(
                                fileType: fileLogHistory.fileType,
                                loginId: fileLogHistory.loginid,
                                message: $"Parquet file moving into Archived folder: {destinationFolderPath}",
                                messageType: "info",
                                processId: fileLogHistory.processId,
                                processName: fileLogHistory.processName,
                                tableName: fileLogHistory.tableName,
                                totalRows: fileLogHistory.totalRows,
                                flpConfigurationId: fileLogHistory.flpConfigurationId,
                                fileUploadedId: fileLogHistory.fileUploadedId,
                                FileStatusActivityEnum.ProcessCompleted,
                                FlpActivityLogStatusEnum.ParquetFileArchived);
                            //Deleting source file
                            return (true, flpProcessTempFile.Uri);
                        }
                        else
                        {
                            await AddFileProcessLosStatus(
                                fileType: fileLogHistory.fileType,
                                loginId: fileLogHistory.loginid,
                                message: $"Parquet file moving into Archived folder: {destinationFolderPath}",
                                messageType: "info",
                                processId: fileLogHistory.processId,
                                processName: fileLogHistory.processName,
                                tableName: fileLogHistory.tableName,
                                totalRows: fileLogHistory.totalRows,
                                flpConfigurationId: fileLogHistory.flpConfigurationId,
                                fileUploadedId: fileLogHistory.fileUploadedId,
                                FileStatusActivityEnum.ProcessCompleted,
                                FlpActivityLogStatusEnum.ParquetFileArchived);
                            //Deleting source file
                            return (false, "something went wrong");
                        }

                    }


                }

                //Deleting source file 

                return (true, archivedParquetFilePath);


            }
            catch (Exception ex)
            {
                await AddFileProcessLosStatus(
                       fileType: fileLogHistory.fileType,
                       loginId: fileLogHistory.loginid,
                       message: $"Error: Parquet file moving into Archived folder: {destinationFolderPath}",
                       messageType: "error",
                       processId: fileLogHistory.processId,
                       processName: fileLogHistory.processName,
                       tableName: fileLogHistory.tableName,
                       totalRows: fileLogHistory.totalRows,
                       flpConfigurationId: fileLogHistory.flpConfigurationId,
                       fileUploadedId: fileLogHistory.fileUploadedId,
                       FileStatusActivityEnum.Error,
                       FlpActivityLogStatusEnum.ParquetFileArchived);
                return (false, ex.Message.ToString());
            }

        }

        public async Task AddFileProcessLosStatus(string fileType, string loginId, string message, string messageType,
      long processId, string processName, string tableName, int totalRows, string flpConfigurationId, string fileUploadedId,
      FileStatusActivityEnum fileStatusActivityEnum, FlpActivityLogStatusEnum flpActivityLogStatusEnum)
        {
            var fileProcessLogHistoryDto = new FileProcessLogHistoryDto()
            {
                dateTimeReceived = DateTime.UtcNow,
                fileType = fileType,
                loginid = loginId,
                message = message,
                messageType = messageType,
                processId = processId,
                processName = processName,
                tableName = tableName,
                totalRows = totalRows,
                flpConfigurationId = flpConfigurationId,
                activityProcessStatusId = (int)fileStatusActivityEnum,
                flpFileLogStatusId = (int)flpActivityLogStatusEnum,
                fileUploadedId = fileUploadedId

            };
            var ret = await _ifileLoadingProcessRepository.InsertFileProcessStatus(fileProcessLogHistoryDto);
            if (!ret)
            {
                _logger.LogError($"Error: Not inserted records for {flpConfigurationId}");
            }
        }

        private async Task<(bool,string)> DeleteParquetFileFromParquetLocation(long processId, string fileType,  ParquetFileResponseDto resultResponse,ConfigurationTableMappingDto configurationTableMappingDto ,FlpConfigurationResponseDto flpConfigurationRequestDto,SharedLocationDestinationServerDto slDestinationServerDto)
        {
            bool deleted = false;
            string messsage = "";

           // await AddFileProcessLosStatus(
           //   fileType: fileType,
           //   loginId: "",
           //   message: $"deleting file from location:{resultResponse.ParquetFilePath}",
           //   messageType: "info",
           //   processId: processId,
           //   processName: flpConfigurationRequestDto.ProcessName,
           //   tableName: flpConfigurationRequestDto.TableName,
           //   totalRows: resultResponse.TotalRows,
           //   flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
           //   fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
           //   FileStatusActivityEnum.ProcessStarted,
           //   FlpActivityLogStatusEnum.DeletedParquetLocation
           //);
            //Deleting parquet file from location , its moved already in archived folder
            await AddFileProcessLosStatus(
             fileType: fileType,
             loginId: "",
             message: $"deleting file from location:{resultResponse.ParquetFilePath}",
             messageType: "info",
             processId: processId,
             processName: flpConfigurationRequestDto.ProcessName,
             tableName: configurationTableMappingDto.TableName,
             totalRows: resultResponse.TotalRows,
             flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
             fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
             FileStatusActivityEnum.Processing,
             FlpActivityLogStatusEnum.DeletedParquetLocation
          );
            //Call deleting function
           
           
            if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
            {
                deleted = await _iBlobStorageService.DeleteBlobAsync(resultResponse.ParquetBlobClient);
                if (!deleted)
                {
                    messsage = "something went wrong";
                }
            }
            else
            {
                var onPremLocation = true;// Convert.ToBoolean(KeyVault.GetKeyVaultValue("OnPremLocation"));
                if (onPremLocation)
                {
                    CheckConnectivitySMBLibraryModel parquetFileModel = new CheckConnectivitySMBLibraryModel
                    {
                        serverIP = slDestinationServerDto.ServerName,
                        username = slDestinationServerDto.UserName,
                        password = slDestinationServerDto.Password,
                        sharedFolderName = slDestinationServerDto.FolderName,
                        domain = slDestinationServerDto.Domain,
                        sourceFilePath = resultResponse.ParquetFilePath,
                    };
                    var result = _iISMBLibraryServices.SMBRequest(parquetFileModel, flpConfigurationRequestDto.FlpConfigurationId, SMBRequestEnum.DeleteFile);
                    if (result.FileIsDeleted)
                    {
                        deleted = true;
                        messsage = "DeletedFile";
                    }
                    else
                    {
                        deleted = false;
                        messsage = "something went wrong";
                    }
                }
                else
                {
                    (deleted, messsage) = await FlpConfigurationHelper.DeleteFile(resultResponse.ParquetFilePath);
                }

            }

            if (deleted)
            {
                await AddFileProcessLosStatus(
                 fileType: fileType,
                 loginId: "",
                 message: $"deleting file from location:{resultResponse.ParquetFilePath}",
                 messageType: "info",
                 processId: processId,
                 processName: flpConfigurationRequestDto.ProcessName,
                 tableName: configurationTableMappingDto.TableName,
                 totalRows: resultResponse.TotalRows,
                 flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                 fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                 FileStatusActivityEnum.ProcessCompleted,
                 FlpActivityLogStatusEnum.DeletedParquetLocation
                );
            }
            else
            {
                await AddFileProcessLosStatus(
                 fileType: fileType,
                 loginId: "",
                 message: $"deleting file from location:{resultResponse.ParquetFilePath}",
                 messageType: "error",
                 processId: processId,
                 processName: flpConfigurationRequestDto.ProcessName,
                 tableName: configurationTableMappingDto.TableName,
                 totalRows: resultResponse.TotalRows,
                 flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                 fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                 FileStatusActivityEnum.Error,
                 FlpActivityLogStatusEnum.DeletedParquetLocation
                );
                _logger.LogError($"Error: deleting file from location:{flpConfigurationRequestDto.FlpConfigurationId}");
                //return
                
            }
            return (deleted, messsage);
        }




        private async Task<(bool, string)> DeleteTempFileFromTemptLocation(long processId, string fileType, FlpProcessTempFile flpProcessTempFile, ParquetFileResponseDto resultResponse,ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationRequestDto, SharedLocationDestinationServerDto slDestinationServerDto)
        {
            bool deleted = false;
            string messsage = "";

            //Deleting temp folder 
            //await AddFileProcessLosStatus(
            //      fileType: fileType,
            //      loginId: "",
            //      message: $"file is deleting is starting from temp folder{flpProcessTempFile.sourceTempFilePath}",
            //      messageType: "info",
            //      processId: processId,
            //      processName: flpConfigurationRequestDto.ProcessName,
            //      tableName: flpConfigurationRequestDto.TableName,
            //      totalRows: resultResponse.TotalRows,
            //      flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
            //      fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
            //      FileStatusActivityEnum.ProcessStarted,
            //      FlpActivityLogStatusEnum.FileDeletedFromTemp
            // );
            await AddFileProcessLosStatus(
                  fileType: fileType,
                  loginId: "",
                  message: $"file is deleting is starting from temp folder{flpProcessTempFile.sourceTempFilePath}",
                  messageType: "info",
                  processId: processId,
                  processName: flpConfigurationRequestDto.ProcessName,
                  tableName: configurationTableMappingDto.TableName,
                  totalRows: resultResponse.TotalRows,
                  flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                  fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                  FileStatusActivityEnum.Processing,
                  FlpActivityLogStatusEnum.FileDeletedFromTemp
             );
            //Call deleting function
           
           
            if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
            {
                deleted = await _iBlobStorageService.DeleteTempFileBlobAsync(flpProcessTempFile.Name, flpProcessTempFile.ParquetBlobConnectionString, flpProcessTempFile.BlobContainerName);
                if (!deleted)
                {
                    messsage = "something went wrong.";
                }
            }
            else
            {
                var onPremLocation = true;// Convert.ToBoolean(KeyVault.GetKeyVaultValue("OnPremLocation").Result);
                if (onPremLocation)
                {
                    CheckConnectivitySMBLibraryModel parquetFileModel = new CheckConnectivitySMBLibraryModel
                    {
                        serverIP = slDestinationServerDto.ServerName,
                        username = slDestinationServerDto.UserName,
                        password = slDestinationServerDto.Password,
                        sharedFolderName = slDestinationServerDto.FolderName,
                        domain = slDestinationServerDto.Domain,
                        sourceFilePath = flpProcessTempFile.sourceTempFilePath,
                    };
                    var result = _iISMBLibraryServices.SMBRequest(parquetFileModel, flpConfigurationRequestDto.FlpConfigurationId, SMBRequestEnum.DeleteFile);
                    if (result.FileIsDeleted)
                    {
                        deleted = true;
                        messsage = "DeletedFile";
                    }
                    else
                    {
                        //Adding log in seprate function
                        deleted = false;
                        messsage = "something went wrong";
                    }
                }
                else
                {
                    (deleted, messsage) = await FlpConfigurationHelper.DeleteFile(flpProcessTempFile.sourceTempFilePath);
                }

            }

            if (deleted)
            {
                await AddFileProcessLosStatus(
                 fileType: fileType,
                 loginId: "",
                 message: $"deleted file from location:{flpProcessTempFile.sourceTempFilePath}",
                 messageType: "info",
                 processId: processId,
                 processName: flpConfigurationRequestDto.ProcessName,
                 tableName: configurationTableMappingDto.TableName,
                 totalRows: resultResponse.TotalRows,
                 flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                 fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                 FileStatusActivityEnum.ProcessCompleted,
                 FlpActivityLogStatusEnum.FileDeletedFromTemp
                );
            }
            else
            {
                await AddFileProcessLosStatus(
                 fileType: fileType,
                 loginId: "",
                 message: $"deleting file from location:{flpProcessTempFile.sourceTempFilePath}",
                 messageType: "error",
                 processId: processId,
                 processName: flpConfigurationRequestDto.ProcessName,
                 tableName: configurationTableMappingDto.TableName,
                 totalRows: resultResponse.TotalRows,
                 flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                 fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                 FileStatusActivityEnum.Error,
                 FlpActivityLogStatusEnum.FileDeletedFromTemp
                );

                _logger.LogError($"Error: deleting file from location:{resultResponse.ParquetBlobClient.Name}");
                
            }

            return (deleted, messsage);
        }
    #region SharePoint Workspace - AY
        private async Task<FileStreamContent?> FetchSharePointFileAsync(FlpConfigurationResponseDto config)
        {
            if (config.SharePointApplicationId == null || config.SharePointApplicationId.Value == Guid.Empty)
            {
                _logger.LogError("SharePoint ApplicationId is missing for {FlpConfigurationId}", config.FlpConfigurationId);
                return null;
            }

            var credentials = new WorkspaceCredentials
            {
                ApplicationId = config.SharePointApplicationId.Value,
                LibraryName = config.SharePointLibraryName
            };

            var fileContent = await _sharePointPluginService.FetchFileAsync(credentials, config.SourcePath, null);

            if (fileContent.Content == null || fileContent.Content == Stream.Null)
            {
                _logger.LogError("Failed to fetch file from SharePoint for {FlpConfigurationId}", config.FlpConfigurationId);
                return null;
            }

            return fileContent;
        }
        #endregion
    }
}
