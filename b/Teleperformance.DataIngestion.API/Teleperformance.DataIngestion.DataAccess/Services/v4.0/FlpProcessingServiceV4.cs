using AngleSharp.Io;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NPOI.SS.Formula.Functions;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.Common.Crypto;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v3._0;
using Teleperformance.DataIngestion.DatabricksAPI.Model;
using Teleperformance.DataIngestion.DatabricksAPI.Services;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._0;
using Teleperformance.DataIngestion.Models.DTOs.v4._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingProcess.TableSchemaValidation;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v4._0.DataBricksAPIJobStatus;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Enums.v4._0;
using Teleperformance.DataIngestion.Models.Helpers;
using Teleperformance.DataIngestion.Models.Models.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Services.v4._0
{
    public class FlpProcessingServiceV4 : IFlpProcessingServiceV4
    {
        private readonly IFileLoadingProcessRepository _ifileLoadingProcessRepository;
        private readonly IValidateSchemaService _iTableSchemaValidationService;
        private readonly IBronzeDbRepository _iInsertedDataToBronzeDbRepository;
        private readonly ILogger<FlpProcessingServiceV4> _logger;
        private readonly IBlobStorageService _iBlobStorageService;
        private readonly ISMBLibraryServices _iISMBLibraryServices;
        private readonly ICache _cache;
        private readonly IValidateSchemaService _iValidateSchemaService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FlpProcessingServiceV4(IFileLoadingProcessRepository ifileLoadingProcessRepository, ILogger<FlpProcessingServiceV4> logger, IValidateSchemaService iTableSchemaValidationService, IBronzeDbRepository iInsertedDataToBronzeDbRepository, IBlobStorageService iBlobStorageService, ISMBLibraryServices iISMBLibraryServices, ICache cache, IValidateSchemaService iValidateSchemaService, IHttpContextAccessor httpContextAccessor)
        {
            _ifileLoadingProcessRepository = ifileLoadingProcessRepository;
            _logger = logger;
            _iTableSchemaValidationService = iTableSchemaValidationService;
            _iInsertedDataToBronzeDbRepository = iInsertedDataToBronzeDbRepository;
            _iBlobStorageService = iBlobStorageService;
            _iISMBLibraryServices = iISMBLibraryServices;
            _cache = cache;
            _iValidateSchemaService = iValidateSchemaService;
            _httpContextAccessor = httpContextAccessor;
        }
        public async Task<APIResponse<FlpDatabricksProcessResponseDto>> ParquetFileProcessToDataLake(long processId, string fileType, string fileLocation, string? tabName, string connectionString, FlpActivityLogStatusEnum currentStatus, FlpProcessTempFile flpProcessTempFile, ParquetFileResponseDto resultResponse, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto, DatabricksStorageAccountDto databricksStorageAccountDto)
        {
            FlpDatabricksProcessResponseDto flpConvertToParquetResponseDto = new FlpDatabricksProcessResponseDto();
            flpConvertToParquetResponseDto.TotalRows = resultResponse.TotalRows;
            flpConvertToParquetResponseDto.InsertedRows = resultResponse.InsertedRows;
            flpConvertToParquetResponseDto.DuplicateRows = resultResponse.DuplicateRows;
            flpConvertToParquetResponseDto.BlobName = flpProcessTempFile.Name;
            var validationRuleServiceHelper = new ValidationRuleServiceHelper(_logger);
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

            if (flpConfigurationRequestDto.UIValidation)
            {
                await AddFileProcessLosStatus(
                 fileType: fileType,
                 loginId: "",
                 message: $"Success: UI Validation process starting",
                 messageType: "info",
                 processId: processId,
                 processName: flpConfigurationRequestDto.ProcessName,
                 tableName: configurationTableMappingDto.TableName,
                 totalRows: resultResponse.TotalRows,
                 flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                 fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                 FileStatusActivityEnum.Processing,
                 FlpActivityLogStatusEnum.UIValidation
                );
                string bearerToken = GetBearerToken();
                //Calling UI Validation API
                var res1 = fileType == "excel" ? await validationRuleServiceHelper.ValidateExcelRules(bearerToken, true, flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.UploadedFileId, flpProcessTempFile?.Name ?? "", resultResponse.ParquetFilePath, destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey, destinationStorageAccountDto.StorageContainerName, null) :
                    await validationRuleServiceHelper.ValidateCsvTextRules(bearerToken, true, flpProcessTempFile, flpConfigurationRequestDto, resultResponse, destinationStorageAccountDto);

                if (res1?.ResultStatus.Code == APIResultStatus.Completed.Code)
                {
                    //Completed status
                    var result = res1?.Result;
                    var responseMessage = result?.ResponseMessage;
                    //Checked failed rows
                    if (result?.FailedRows > 0)
                    {
                        await AddFileProcessLosStatus(
                            fileType: fileType,
                            loginId: "",
                            message: $"Error: UI Validation Failed for this file. Reason- {responseMessage} ",
                            messageType: "error",
                            processId: processId,
                            processName: flpConfigurationRequestDto.ProcessName,
                            tableName: configurationTableMappingDto.TableName,
                            totalRows: resultResponse.TotalRows,
                            flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                            fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                            FileStatusActivityEnum.Error,
                            FlpActivityLogStatusEnum.UIValidation
                        );

                        _logger.LogError($"Error: Unable to complete UI Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId},{responseMessage}");
                        //return
                        return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { $"Error: Unable to complete BE Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}" },
                            Result = flpConvertToParquetResponseDto
                        });
                    }
                    else
                    {
                        await AddFileProcessLosStatus(
                            fileType: fileType,
                            loginId: "",
                            message: $"Success: UI Validation process completed",
                            messageType: "info",
                            processId: processId,
                            processName: flpConfigurationRequestDto.ProcessName,
                            tableName: configurationTableMappingDto.TableName,
                            totalRows: resultResponse.TotalRows,
                            flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                            fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                            FileStatusActivityEnum.ProcessCompleted,
                            FlpActivityLogStatusEnum.UIValidation
                       );


                       
                    }
                }
                else
                {
                    await AddFileProcessLosStatus(
                      fileType: fileType,
                      loginId: "",
                      message: $"Error: UI Validation process Failed {res1?.ResponseMessage.FirstOrDefault()}",
                      messageType: "info",
                      processId: processId,
                      processName: flpConfigurationRequestDto.ProcessName,
                      tableName: configurationTableMappingDto.TableName,
                      totalRows: resultResponse.TotalRows,
                      flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                      fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                      FileStatusActivityEnum.Error,
                      FlpActivityLogStatusEnum.UIValidation
                     );

                    _logger.LogError($"Error: Unable to complete UI Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}");
                    //return
                    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { $"Error: Unable to complete BE Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}" },
                        Result = flpConvertToParquetResponseDto
                    });
                }
            }
            else
            {
                await AddFileProcessLosStatus(
                fileType: fileType,
                loginId: "",
                message: $"Success: UI Validation processing stage is skipped",
                messageType: "info",
                processId: processId,
                processName: flpConfigurationRequestDto.ProcessName,
                tableName: configurationTableMappingDto.TableName,
                totalRows: resultResponse.TotalRows,
                flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                FileStatusActivityEnum.Skip,
                FlpActivityLogStatusEnum.UIValidation
               );
            }
            //BEValidation
            if (flpConfigurationRequestDto.BEValidation)
            {
                await AddFileProcessLosStatus(
                fileType: fileType,
                loginId: "",
                message: $"Success: BE Validation process starting",
                messageType: "info",
                processId: processId,
                processName: flpConfigurationRequestDto.ProcessName,
                tableName: configurationTableMappingDto.TableName,
                totalRows: resultResponse.TotalRows,
                flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                FileStatusActivityEnum.Processing,
                FlpActivityLogStatusEnum.BEValidation
               );
                string bearerToken = GetBearerToken();
                var res2 = fileType == "excel" ? await validationRuleServiceHelper.ValidateExcelRules(bearerToken, false, flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.UploadedFileId, flpProcessTempFile?.Name ?? "", resultResponse.ParquetFilePath, destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey, destinationStorageAccountDto.StorageContainerName, null) :
                    await validationRuleServiceHelper.ValidateCsvTextRules(bearerToken, false, flpProcessTempFile, flpConfigurationRequestDto, resultResponse, destinationStorageAccountDto);
                if (res2.ResultStatus.Code == APIResultStatus.Completed.Code)
                {
                    var result = res2.Result;
                    var responseMessage = result.ResponseMessage;
                    //Checked failed rows
                    if (result.FailedRows > 0)
                    {
                        await AddFileProcessLosStatus(
                            fileType: fileType,
                            loginId: "",
                            message: $"Error: UI Validation Failed for this file. Reason- {responseMessage} ",
                            messageType: "error",
                            processId: processId,
                            processName: flpConfigurationRequestDto.ProcessName,
                            tableName: configurationTableMappingDto.TableName,
                            totalRows: resultResponse.TotalRows,
                            flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                            fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                            FileStatusActivityEnum.Error,
                            FlpActivityLogStatusEnum.BEValidation
                        );

                        _logger.LogError($"Error: Unable to complete BE Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId},{responseMessage}");
                        //return
                        return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { $"Error: Unable to complete BE Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}" },
                            Result = flpConvertToParquetResponseDto
                        });
                    }
                    else
                    {
                        await AddFileProcessLosStatus(
                             fileType: fileType,
                             loginId: "",
                             message: $"Success: BE Validation process completed",
                             messageType: "info",
                             processId: processId,
                             processName: flpConfigurationRequestDto.ProcessName,
                             tableName: configurationTableMappingDto.TableName,
                             totalRows: resultResponse.TotalRows,
                             flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                             fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                             FileStatusActivityEnum.ProcessCompleted,
                             FlpActivityLogStatusEnum.BEValidation
                        );


                    }
                }
                else
                {
                    await AddFileProcessLosStatus(
                     fileType: fileType,
                     loginId: "",
                     message: $"Error: BE Validation process Failed {res2?.ResponseMessage.FirstOrDefault()}",
                     messageType: "info",
                     processId: processId,
                     processName: flpConfigurationRequestDto.ProcessName,
                     tableName: configurationTableMappingDto.TableName,
                     totalRows: resultResponse.TotalRows,
                     flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                     fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                     FileStatusActivityEnum.Error,
                     FlpActivityLogStatusEnum.BEValidation
                    );

                    _logger.LogError($"Error: Unable to complete BE Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}");
                    //return
                    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { $"Error: Unable to complete BE Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}" },
                        Result = flpConvertToParquetResponseDto
                    });
                }
            }
            else
            {
                await AddFileProcessLosStatus(
                fileType: fileType,
                loginId: "",
                message: $"Success: BE Validation processing stage is skipped",
                messageType: "info",
                processId: processId,
                processName: flpConfigurationRequestDto.ProcessName,
                tableName: configurationTableMappingDto.TableName,
                totalRows: resultResponse.TotalRows,
                flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                FileStatusActivityEnum.Skip,
                FlpActivityLogStatusEnum.BEValidation
               );
            }
            

            //Moving folder into archive
            var flpLogHistory2 = new FileProcessLogHistoryDto()
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
            //Moved  Parquet File into storage account
            
            (bool retMovedFile2, string response2) = await MoveParquetFileToDeltaLakeStorageAccount(flpConfigurationRequestDto.DetalakeStorageAccountPath,
                resultResponse.ParquetFilePath, resultResponse.ParquetBlobClient, flpProcessTempFile.ParquetBlobConnectionString, flpLogHistory2, _iISMBLibraryServices,databricksStorageAccountDto);
            if (retMovedFile2)
            {
                flpConvertToParquetResponseDto.DataLakeStorageAccountPath = response2;
            }
            else
            {
                _logger.LogError($"Error: Parquet file moving into Location for {flpConfigurationRequestDto.FlpConfigurationId}");
                //return
                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { response2 },
                    Result = flpConvertToParquetResponseDto
                });

            }


            //currentStatus = FlpActivityLogStatusEnum.FileSchemaValidated;
            //do_not_acrhive false
            if (!configurationTableMappingDto.DoNotArchiveFile)
            {
                //currentStatus = FlpActivityLogStatusEnum.ParquetFileArchived;
                //Moving folder into archive
                var flpLogHistory1 = new FileProcessLogHistoryDto()
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
                (bool retMovedFile1, string response1) = await MoveParquetToArchiveAndDeletedMainFileV2(flpConfigurationRequestDto.DestinationLocationTypeId ?? 0,
                    resultResponse.ParquetFilePath, resultResponse.ParquetBlobClient, flpProcessTempFile.ParquetBlobConnectionString,
                    flpProcessTempFile.BlobContainerName, flpLogHistory1, slDestinationServerDto, _iISMBLibraryServices, destinationStorageAccountDto.SasKeyToken);
                if (retMovedFile1)
                {
                    flpConvertToParquetResponseDto.BackUpFileName = response1;
                }
                else
                {
                    _logger.LogError($"Error: Parquet file moving into Archived folder for {flpConfigurationRequestDto.FlpConfigurationId}");
                    //return
                    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { response1 },
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
            (bool fileDelteed, string parquetFileResponse) = await DeleteParquetFileFromParquetLocation(processId, fileType, resultResponse, configurationTableMappingDto, flpConfigurationRequestDto, slDestinationServerDto);
            if (!fileDelteed)
            {
                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { parquetFileResponse },
                    Result = flpConvertToParquetResponseDto
                });
            }


            ////Deleting temp file path - Commented as per new tab sheet requirement will delete after all process completed
            //(bool tempFileDelteed, string tempFileResponse) = await DeleteTempFileFromTemptLocation(processId, fileType, flpProcessTempFile, resultResponse, configurationTableMappingDto, flpConfigurationRequestDto, slDestinationServerDto);
            //if (!tempFileDelteed)
            //{
            //    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
            //    {
            //        ResultStatus = APIResultStatus.Error,
            //        ResponseMessage = new List<string> { tempFileResponse },
            //        Result = flpConvertToParquetResponseDto
            //    });
            //}
            //DataBricks Job process started
            //Databricks configuration
            await AddFileProcessLosStatus(
                        fileType: fileType,
                        loginId: "",
                        message: $"DataBricks Run Job process starting",
                        messageType: "info",
                        processId: processId,
                        processName: flpConfigurationRequestDto.ProcessName,
                        tableName: configurationTableMappingDto.TableName,
                        totalRows: resultResponse.TotalRows,
                        flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        FileStatusActivityEnum.Processing,
                        FlpActivityLogStatusEnum.DatabricksJobProcess
            );

            var   configurationFileColumns = await _iValidateSchemaService.GetFileColumnListV2(flpConfigurationRequestDto.FlpConfigurationId);
            var columnWithDataType = string.Join(",", configurationFileColumns.Select(column => $"{column.DbColumn}={column.dataType}"));
            // Initialize the DatabricksJobRunner
            var jobRunner = new DatabricksJobRunner(flpConfigurationRequestDto.DatabricksInstance, flpConfigurationRequestDto.DataBricksAPIVersion, flpConfigurationRequestDto.DataBricksAPIToken);           
            flpConvertToParquetResponseDto.DataLakeStorageAccountPath = Uri.UnescapeDataString(flpConvertToParquetResponseDto.DataLakeStorageAccountPath).Replace("\\", "/") ?? "";

            //Uploaded json file path into blob storage
            string jsonFilePath = FlpConfigurationHelperV4_1.CreatedJsonFilePath(response2, flpConfigurationRequestDto.ProcessName);
            List<string> splitArray = FlpConfigurationHelper.SplitColumnsWithoutCleanColumn(databricksStorageAccountDto.SasKey, "?");
            string sasKey = splitArray.Count > 1 ? splitArray[1] : splitArray[0];
            string jsonFileFullPath = $"{jsonFilePath}?{sasKey}";
            (bool ret,string message) = await _iBlobStorageService.UploadColumnJsonAsync(configurationFileColumns, $"{jsonFileFullPath}");
            if (!ret)
            {
                await AddFileProcessLosStatus(
                                        fileType: fileType,
                                        loginId: "",
                                        message: $"Error:{message}",
                                        messageType: "error",
                                        processId: processId,
                                        processName: flpConfigurationRequestDto.ProcessName,
                                        tableName: configurationTableMappingDto.TableName,
                                        totalRows: resultResponse.TotalRows,
                                        flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                        fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                                        FileStatusActivityEnum.Error,
                                        FlpActivityLogStatusEnum.DatabricksJobProcess);

                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { message },
                    Result = flpConvertToParquetResponseDto
                });
            }

            bool isJsonMappingAddedInDb = await databricksJsonFileColumn(jsonFilePath, columnWithDataType, flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.UploadedFileId, configurationTableMappingDto.TabName);
            if (!isJsonMappingAddedInDb)
            {
                await AddFileProcessLosStatus(
                                        fileType: fileType,
                                        loginId: "",
                                        message: $"Error:{message}",
                                        messageType: "error",
                                        processId: processId,
                                        processName: flpConfigurationRequestDto.ProcessName,
                                        tableName: configurationTableMappingDto.TableName,
                                        totalRows: resultResponse.TotalRows,
                                        flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                        fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                                        FileStatusActivityEnum.Error,
                                        FlpActivityLogStatusEnum.DatabricksJobProcess);

                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { message },
                    Result = flpConvertToParquetResponseDto
                });
            }
            string columnWithDataTypeJsonFilePath = jsonFilePath;
            var jobParameters = new
            {
                deltaTableName = configurationTableMappingDto.TableName ?? "",
                historyTableName = configurationTableMappingDto.historyTableName ?? "",
                dropMainTable = configurationTableMappingDto.DropMainTable,
                AllowOverwrite = configurationTableMappingDto.mergeData,
                SaveOverwriteLog = configurationTableMappingDto.createHistoryTable,
                databricksCatalogue = configurationTableMappingDto.UnityCatalog,
                dataLakeStoragePath = flpConvertToParquetResponseDto.DataLakeStorageAccountPath ?? "",
                dataLakeStorageAccountName = databricksStorageAccountDto.FlpStorageAccount ?? "",
                dataLakeStorageContainerName = databricksStorageAccountDto.StorageContainerName ?? "",
                processModified = flpConfigurationRequestDto.ProcessModified,
                keyColumnList = configurationTableMappingDto.KeyColumnList ?? "",
                columnWithDataType = jsonFilePath ?? "",
                fileUploadId = flpConfigurationRequestDto.UploadedFileId,
                fileName = flpConfigurationRequestDto.UploadedFileName,
                validateSchema = configurationTableMappingDto.ValidateFileSchema,
                campaignId = configurationTableMappingDto.campaignId ?? ""
            };

            // Run the job
            if (string.IsNullOrWhiteSpace(flpConfigurationRequestDto.JobId))
            {
                await AddFileProcessLosStatus(
                   fileType: fileType,
                   loginId: "",
                   message: $"JobId is empty",
                   messageType: "error",
                   processId: processId,
                   processName: flpConfigurationRequestDto.ProcessName,
                   tableName: configurationTableMappingDto.TableName,
                   totalRows: resultResponse.TotalRows,
                   flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                   fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                   FileStatusActivityEnum.Error,
                   FlpActivityLogStatusEnum.DatabricksJobProcess,
                   databricksAPIResponse:null
               );

                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { $"JobId is empty" },
                    Result = flpConvertToParquetResponseDto
                });
            }


            //  var dataBricksStages = await _cache.GetDataBricksStagesAsync();
            //  var databricksTerminationDetails = await _cache.GetDatabricksTerminationDetailsAsync();
            var response = await jobRunner.RunJobAsync(Convert.ToInt64(flpConfigurationRequestDto.JobId), jobParameters);
            var updatingJsonContent = Convert.ToBoolean(KeyVault.GetKeyVaultValue("TPDataIngestionAddingJobRequestBody").Result);
            if (updatingJsonContent)
            {
                string convertedParamInJson = JsonConvert.SerializeObject(jobParameters, Formatting.Indented);
                _logger.LogError("Info: Job Runner Request body for: " + convertedParamInJson);
            }
            
            //var response = await jobRunner.RunJobAsync(Convert.ToInt64(flpConfigurationRequestDto.JobId), jobParameters);
            //var updatingJsonContent = Convert.ToBoolean(KeyVault.GetKeyVaultValue("TPDataIngestionAddingJobRequestBody").Result);
            
            if (response !=null && response.JobRunSuccess)
            {
                flpConvertToParquetResponseDto.RunId = response.RunId;               
                

                await AddFileProcessLosStatus(
                    fileType: fileType,
                    loginId: "",
                    message: $"DataBricks Run Job triggered successfully",
                    messageType: "info",
                    processId: processId,
                    processName: flpConfigurationRequestDto.ProcessName,
                    tableName: configurationTableMappingDto.TableName,
                    totalRows: resultResponse.TotalRows,
                    flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                    fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                    FileStatusActivityEnum.ProcessCompleted,
                    FlpActivityLogStatusEnum.DatabricksJobProcess,
                    databricksAPIResponse:response.ResponseContent
                );
                //Update RunId in db
                //Will start the writing catalog procss - run the job tracking status
                await AddFileProcessLosStatus(
                    fileType: fileType,
                    loginId: "",
                    message: $"Writing to Databricks catalog",
                    messageType: "info",
                    processId: processId,
                    processName: flpConfigurationRequestDto.ProcessName,
                    tableName: configurationTableMappingDto.TableName,
                    totalRows: resultResponse.TotalRows,
                    flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                    fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                    FileStatusActivityEnum.Processing,
                    FlpActivityLogStatusEnum.WritingDataBricksCatalog
                );
               _logger.LogInformation($"Info:Writing Catalog- JobId: {flpConfigurationRequestDto.JobId}, RunId: {response.RunId}, calling API");
                var jobRunStatusAPIResponse = await jobRunner.GetJobRunStatusAsync(flpConvertToParquetResponseDto.RunId??0);
                if(jobRunStatusAPIResponse !=null && (int)jobRunStatusAPIResponse.StatusCode == 200)
                {
                    if(jobRunStatusAPIResponse.JobSatus != null)
                    {
                        
                       // var jobStatusState = jobRunStatusAPIResponse.JobSatus.Status.State;
                        var jobStatusState = jobRunStatusAPIResponse.JobSatus.State;
                        _logger.LogInformation($"Info:Writing Catalog- call status {jobStatusState}, calling API");
                       // var stageDetails = dataBricksStages?.FirstOrDefault(x => x.Stage == jobStatusState);
                        if(jobStatusState != null)
                        {
                            var lifeCycleStateId = EnumHelper.GetLifeCycleStateEnumValueFromDescription(jobStatusState.LifeCycleState);
                            var resultStateId = EnumHelper.GetResultStateEnumEnumValueFromDescription(jobStatusState.ResultState);
                            flpConvertToParquetResponseDto.LifeCycleStateId = lifeCycleStateId;
                            flpConvertToParquetResponseDto.ResultStateId = resultStateId;
                            var jobStatusMessage = $"life_cycle_state {jobStatusState.LifeCycleState} and result_state {jobStatusState.ResultState}";
                            if (lifeCycleStateId == (int)LifeCycleStateEnum.TERMINATED && resultStateId == (int)ResultStateEnum.SUCCESS)
                            {
                                //Completed
                                await AddFileProcessLosStatus(
                                        fileType: fileType,
                                        loginId: "",
                                        message: $"Process completed successfully. {jobStatusMessage}",
                                        messageType: "info",
                                        processId: processId,
                                        processName: flpConfigurationRequestDto.ProcessName,
                                        tableName: configurationTableMappingDto.TableName,
                                        totalRows: resultResponse.TotalRows,
                                        flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                        fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                                        FileStatusActivityEnum.ProcessCompleted,
                                        FlpActivityLogStatusEnum.WritingDataBricksCatalog,
                                        databricksAPIResponse: jobRunStatusAPIResponse.ResponseContent
                                    );
                              
                            }
                            else
                            {
                                //Not completed: update only status from main function
                                _logger.LogInformation($"Info:Writing Catalog- call status {jobStatusState}, Not terminated api & not success status");
                                await AddFileProcessLosStatus(
                                               fileType: fileType,
                                               loginId: "",
                                               message: $"Writing Databricks catalog: {jobStatusMessage}",
                                               messageType: "info",
                                               processId: processId,
                                               processName: flpConfigurationRequestDto.ProcessName,
                                               tableName: configurationTableMappingDto.TableName,
                                               totalRows: resultResponse.TotalRows,
                                               flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                               fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                                               FileStatusActivityEnum.Processing,
                                               FlpActivityLogStatusEnum.WritingDataBricksCatalog,
                                               databricksAPIResponse: jobRunStatusAPIResponse.ResponseContent
                                           );

                                //return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                                //{
                                //    ResultStatus = APIResultStatus.Error,
                                //    ResponseMessage = new List<string> { $"Writing to Databricks catalog: {jobStatusMessage}" },
                                //    Result = flpConvertToParquetResponseDto
                                //});
                            }
                        }
                        else
                        {
                            //Error Not fetched stage details in history table: update only error status
                            //Completed
                            await AddFileProcessLosStatus(
                                    fileType: fileType,
                                   loginId: "",
                                    message: $"Error: not found jobStatusState details",
                                    messageType: "error",
                                    processId: processId,
                                    processName: flpConfigurationRequestDto.ProcessName,
                                    tableName: configurationTableMappingDto.TableName,
                                    totalRows: resultResponse.TotalRows,
                                    flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                    fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                                    FileStatusActivityEnum.Error,
                                    FlpActivityLogStatusEnum.WritingDataBricksCatalog,
                                    databricksAPIResponse: jobRunStatusAPIResponse.ResponseContent
                                );

                            return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                            {
                                ResultStatus = APIResultStatus.Error,
                                ResponseMessage = new List<string> { $"Error: not found jobStatusState details" },
                                Result = flpConvertToParquetResponseDto
                            });
                        }


                    }
                    else
                    {
                        //Error Not fetched job status in history table: update only error status
                        _logger.LogInformation($"Error:Writing Catalog- JobId: {flpConfigurationRequestDto.JobId},RunId: {response.RunId},Not found JobStatus calling API");
                        await AddFileProcessLosStatus(
                                   fileType: fileType,
                                  loginId: "",
                                   message: $"Error: Not found job status,Message:{jobRunStatusAPIResponse.Message}",
                                   messageType: "error",
                                   processId: processId,
                                   processName: flpConfigurationRequestDto.ProcessName,
                                   tableName: configurationTableMappingDto.TableName,
                                   totalRows: resultResponse.TotalRows,
                                   flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                   fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                                   FileStatusActivityEnum.Error,
                                   FlpActivityLogStatusEnum.WritingDataBricksCatalog,
                                   databricksAPIResponse: jobRunStatusAPIResponse.ResponseContent
                               );

                        return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { $"Error: Not found job status from Job/getrun API" },
                            Result = flpConvertToParquetResponseDto 
                        });
                    }
                }
                else
                {
                    //Skip this condition - will check by function app next time
                    //Error Not fetched job status in history table: update only error status
                    _logger.LogInformation($"Error:Writing Catalog- JobId: {flpConfigurationRequestDto.JobId}, RunId: {response.RunId}, calling API");
                    await AddFileProcessLosStatus(
                               fileType: fileType,
                              loginId: "",
                               message: $"Error: {jobRunStatusAPIResponse?.Message?? "Internal server error in JobRunStatusAPI"}",
                               messageType: "error",
                               processId: processId,
                               processName: flpConfigurationRequestDto.ProcessName,
                               tableName: configurationTableMappingDto.TableName,
                               totalRows: resultResponse.TotalRows,
                               flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                               fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                               FileStatusActivityEnum.Error,
                               FlpActivityLogStatusEnum.WritingDataBricksCatalog,
                               databricksAPIResponse: jobRunStatusAPIResponse.ResponseContent??""
                           );

                    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { $"Error: Not found details from Job/getrun API  for this {flpConvertToParquetResponseDto.RunId}" },
                        Result = flpConvertToParquetResponseDto
                    });
                }
            }
            else
            {
                
                await AddFileProcessLosStatus(
                fileType: fileType,
                loginId: "",
                message: $"Error:{response.Message}",
                messageType: "error",
                processId: processId,
                processName: flpConfigurationRequestDto.ProcessName,
                tableName: configurationTableMappingDto.TableName,
                totalRows: resultResponse.TotalRows,
                flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                FileStatusActivityEnum.Error,
                FlpActivityLogStatusEnum.DatabricksJobProcess,
                databricksAPIResponse: response.ResponseContent
              );

                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { $"Error: Error: {response.StatusCode}, Details: {response.Message}" },
                    Result = flpConvertToParquetResponseDto
                });
            }

           

            //All process have been completed
            return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Process Completed" },
                Result = flpConvertToParquetResponseDto
            });
        }




        public async Task<(FlpProcessTempFile?, bool)> MoveSourceExcelFileToTemporaryDestinationAndDelete(long processId, string fileType, string fileLocation, string fileUploadedId, string backUpFileName, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto)
        {
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();
            FlpProcessTempFile flpCsvProcessTempFile = new FlpProcessTempFile();
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
                bool isCsvFileCopiedFile = false;
                BlobClient sourceBlobClient = null;
                string sourceUrl = "";
                string relativePath = "";

                if (flpConfigurationRequestDto.BlobClients != null && !string.IsNullOrWhiteSpace(flpConfigurationRequestDto.BlobClients.Uri) && !string.IsNullOrWhiteSpace(flpConfigurationRequestDto.BlobClients.Name))
                {
                    flpConfigurationRequestDto.BlobClients.Uri = Uri.UnescapeDataString(flpConfigurationRequestDto.BlobClients.Uri);
                    flpConfigurationRequestDto.BlobClients.Name = Uri.UnescapeDataString(flpConfigurationRequestDto.BlobClients.Name);
                }
                if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure &&
                    flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                {
                    string sourceBlobUrl = flpConfigurationRequestDto.BlobClients.Uri;
                    string sourceBlobName = flpConfigurationRequestDto.BlobClients.Name;
                    string sourceBlobConnectionString = "";// FlpConfigurationHelper.GetBlobConnectionString(flpConfigurationRequestDto.SourceStorageAccount, flpConfigurationRequestDto.SourceStorageAccountKey);
                    string sourceBlobContainer =  flpConfigurationRequestDto.SourceContainerName;

                    //sourceBlobClient = _iBlobStorageService.GetBlobClientDetails(flpConfigurationRequestDto.BlobClients.Name, sourceBlobConnectionString, sourceBlobContainer);
                   // sourceBlobClient = _iBlobStorageService.GetBlobClientDetails(sourceBlobName, sourceBlobConnectionString, sourceBlobContainer);


                    sourceBlobClient = null;//_iBlobStorageService.GetBlobClientDetails(sourceBlobName, sourceBlobConnectionString, sourceBlobContainer);

                    if (flpConfigurationRequestDto.SasKeyToken)
                    {
                        sourceBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionStringBySasKey(flpConfigurationRequestDto.SourceStorageAccount, flpConfigurationRequestDto.SasKey);
                       // sourceBlobContainer = flpConfigurationRequestDto.SourceContainerName;
                        sourceBlobClient = _iBlobStorageService.GetBlobClientDetailsBySasToken(sourceBlobName, sourceBlobConnectionString);
                    }
                    else
                    {
                        sourceBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(flpConfigurationRequestDto.SourceStorageAccount, flpConfigurationRequestDto.SourceStorageAccountKey);
                       // sourceBlobContainer = flpConfigurationRequestDto.SourceContainerName;
                        sourceBlobClient = _iBlobStorageService.GetBlobClientDetails(sourceBlobName, sourceBlobConnectionString, sourceBlobContainer);
                    }

                    //destination
                    string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey);
                    string destinationContainerName = destinationStorageAccountDto.StorageContainerName;
                    string destinationBlobUrl = $"{flpConfigurationRequestDto.DestinationPath}temp/"; //Moved file into temporary storage
                    string destinationBlobName = $"{destinationBlobUrl}{backUpFileName}";// {sourceBlobClient.Name.Split('/').Last()}";
                    BlobProperties properties = await sourceBlobClient.GetPropertiesAsync();
                    double fileSizeInMB = properties.ContentLength / (1024.0 * 1024.0);
                    if (fileSizeInMB > 300)
                    {
                        throw new Exception("file is greater than 300 Mb, Please contact to administrative");
                    }
                    BlobClient destinationBlobClient = _iBlobStorageService.GetBlobClientDetails(destinationBlobName, destinationBlobConnectionString, destinationContainerName);
                    (isCopiedFile, flpProcessTempFile) = await _iBlobStorageService.CopyFileSourceBlobToDestinationBlobAsync(sourceBlobClient, destinationBlobClient);



                    //if (fileSizeInMB > 50)
                    //{
                    //    _logger.LogInformation($"File size is greater than 50 MB {fileSizeInMB}, creating CSV file for {flpConfigurationRequestDto.FlpConfigurationId}");
                    //    (isCsvFileCopiedFile, flpCsvProcessTempFile) = await _iBlobStorageService.CreateCsvFileFromSourceFile(sourceBlobClient, destinationBlobClient, destinationContainerName, destinationBlobConnectionString);
                    //    if (isCsvFileCopiedFile)
                    //    {
                    //        flpProcessTempFile.CsvFile = flpCsvProcessTempFile.CsvFile;
                    //    }
                    //}
                    //File Size wiil be used for excel only
                    flpProcessTempFile.FileSize = fileSizeInMB;

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
                    (isCopiedFile, flpProcessTempFile) = FlpConfigurationHelper.CopyFileBlobToOnPremAsync(tempFilePath, flpConfigurationRequestDto.FlpConfigurationId, sourceBlobClient, slDestinationServerDto, _iISMBLibraryServices);
                    var onPremLocation = true;// Convert.ToBoolean(KeyVault.GetKeyVaultValue("OnPremLocation").Result);
                    if (onPremLocation)
                    {
                        (isCopiedFile, flpProcessTempFile) = FlpConfigurationHelper.CopyFileBlobToOnPremAsync(tempFilePath, flpConfigurationRequestDto.FlpConfigurationId, sourceBlobClient, slDestinationServerDto, _iISMBLibraryServices);
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
                    if (flpProcessTempFile.FileSize > 300)
                    {
                        throw new Exception("file is greater than 300 Mb, Please contact to administrative");
                    }
                    (isCopiedFile, flpProcessTempFile) = await FlpConfigurationHelper.CopyFileOnPremToDestinationBlobAsync(sourceUrl, flpConfigurationRequestDto.FlpConfigurationId, destinationBlobClient, sourceServer, _iISMBLibraryServices);
                    if (isCopiedFile && flpProcessTempFile.FileSize > 50)
                    {
                        _logger.LogInformation($"File size is greater than 50 MB {flpProcessTempFile.FileSize}, creating CSV file for {flpConfigurationRequestDto.FlpConfigurationId}");
                        string csvBlobName = Path.ChangeExtension(destinationBlobClient.Name, ".csv");
                        BlobClient csvBlobClient = _iBlobStorageService.GetBlobClientDetails(csvBlobName, destinationBlobConnectionString, destinationContainerName);

                        (isCsvFileCopiedFile, flpCsvProcessTempFile) = await FlpConfigurationHelper.CreateCsvFromExcelUsingSmbLibraryAsync(flpConfigurationRequestDto.FlpConfigurationId, destinationBlobClient, sourceServer, csvBlobClient, _iISMBLibraryServices);
                        if (isCsvFileCopiedFile)
                        {
                            flpProcessTempFile.CsvFile = flpCsvProcessTempFile.CsvFile;
                        }
                    }
                    flpProcessTempFile.AccountName = destinationStorageAccountDto.FlpStorageAccount;
                    flpProcessTempFile.BlobContainerName = destinationStorageAccountDto.StorageContainerName;
                    flpProcessTempFile.ParquetBlobConnectionString = destinationBlobConnectionString;
                    // flpProcessTempFile.SourceBlobConnectionString = sourceBlobConnectionString;
                    flpProcessTempFile.DestinationFolder = @$"{flpConfigurationRequestDto.DestinationPath}/parquet/";
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

                if (flpConfigurationRequestDto.BlobClients != null && !string.IsNullOrWhiteSpace(flpConfigurationRequestDto.BlobClients.Uri) && !string.IsNullOrWhiteSpace(flpConfigurationRequestDto.BlobClients.Name))
                {
                    flpConfigurationRequestDto.BlobClients.Uri = Uri.UnescapeDataString(flpConfigurationRequestDto.BlobClients.Uri);
                    flpConfigurationRequestDto.BlobClients.Name = Uri.UnescapeDataString(flpConfigurationRequestDto.BlobClients.Name);
                }
                if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure &&
                    flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                {
                    string sourceBlobUrl = flpConfigurationRequestDto.BlobClients.Uri;
                    string sourceBlobName = flpConfigurationRequestDto.BlobClients.Name;
                    string sourceBlobConnectionString = "";// FlpConfigurationHelper.GetBlobConnectionString(flpConfigurationRequestDto.SourceStorageAccount, flpConfigurationRequestDto.SourceStorageAccountKey);
                    string sourceBlobContainer =  flpConfigurationRequestDto.SourceContainerName;

                    //sourceBlobClient = _iBlobStorageService.GetBlobClientDetails(flpConfigurationRequestDto.BlobClients.Name, sourceBlobConnectionString, sourceBlobContainer);
                    sourceBlobClient = null;//_iBlobStorageService.GetBlobClientDetails(sourceBlobName, sourceBlobConnectionString, sourceBlobContainer);

                    if (flpConfigurationRequestDto.SasKeyToken)
                    {
                        sourceBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionStringBySasKey(flpConfigurationRequestDto.SourceStorageAccount, flpConfigurationRequestDto.SasKey);
                       // sourceBlobContainer = flpConfigurationRequestDto.SourceContainerName;
                        sourceBlobClient = _iBlobStorageService.GetBlobClientDetailsBySasToken(sourceBlobName, sourceBlobConnectionString);
                    }
                    else
                    {
                        sourceBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(flpConfigurationRequestDto.SourceStorageAccount, flpConfigurationRequestDto.SourceStorageAccountKey);
                      //  sourceBlobContainer = flpConfigurationRequestDto.SourceContainerName;
                        sourceBlobClient = _iBlobStorageService.GetBlobClientDetails(sourceBlobName, sourceBlobConnectionString, sourceBlobContainer);
                    }

                    //destination
                    string destinationBlobConnectionString = string.Empty;
                    destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey);
                    string destinationContainerName = destinationStorageAccountDto.StorageContainerName;
                    string destinationBlobUrl = $"{flpConfigurationRequestDto.DestinationPath}temp/"; //Moved file into temporary storage
                    string destinationBlobName = $"{destinationBlobUrl}{backUpFileName}";// {sourceBlobClient.Name.Split('/').Last()}";

                    BlobClient destinationBlobClient = _iBlobStorageService.GetBlobClientDetails(destinationBlobName, destinationBlobConnectionString, destinationContainerName);
                    //(isCopiedFile, flpProcessTempFile) = await _iBlobStorageService.CopyFileSourceBlobToDestinationBlobAsync(sourceBlobClient, destinationBlobClient);
                    (isCopiedFile, flpProcessTempFile) = await new BlobStorageService().CopyBlobUsingStreamAsync(sourceBlobClient, destinationBlobClient);
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
                    (isCopiedFile, flpProcessTempFile) = FlpConfigurationHelper.CopyFileBlobToOnPremAsync(tempFilePath, flpConfigurationRequestDto.FlpConfigurationId, sourceBlobClient, slDestinationServerDto, _iISMBLibraryServices);
                    var onPremLocation = true;// Convert.ToBoolean(KeyVault.GetKeyVaultValue("OnPremLocation").Result);
                    if (onPremLocation)
                    {
                        (isCopiedFile, flpProcessTempFile) = FlpConfigurationHelper.CopyFileBlobToOnPremAsync(tempFilePath, flpConfigurationRequestDto.FlpConfigurationId, sourceBlobClient, slDestinationServerDto, _iISMBLibraryServices);
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
                    BlobClient destinationBlobClient = null;
                    //destination
                    string destinationBlobConnectionString = string.Empty;// FlpConfigurationHelper.GetBlobConnectionString(destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey);
                    string destinationContainerName = destinationStorageAccountDto.StorageContainerName;
                    string destinationBlobUrl = $"{flpConfigurationRequestDto.DestinationPath}temp/{currentTimeString}/";
                    string destinationBlobName = $"{destinationBlobUrl}{backUpFileName}";

                    destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey);
                    destinationBlobClient = _iBlobStorageService.GetBlobClientDetails(destinationBlobName, destinationBlobConnectionString, destinationContainerName);

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



    

        public async Task<(bool, string)> MoveParquetToArchiveAndDeletedMainFileV2(int destinationLocationTypeId, string parquetFilePath, BlobClient bcParquetSourceLocation, string blobConnectionString, string destinationContainerName, FileProcessLogHistoryDto fileLogHistory, SharedLocationDestinationServerDto slDestinationServerDto, ISMBLibraryServices _ismbLibraryServices, bool sasToken)
        {
            string currentTimeString = DateTime.Now.ToString("yyyy/MMMM/dd");
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
                   
                    string destinationBlobName = $"{destinationFolderPath}/{bcParquetSourceLocation.Name.Split('/').Last()}";
                    BlobClient destinationBlob = null;
                    // Get the destination container client
                    // BlobServiceClient destinationBlobServiceClient = new BlobServiceClient(blobConnectionString);
                    //  BlobContainerClient destinationContainerClient = destinationBlobServiceClient.GetBlobContainerClient(destinationContainerName);
                    // Get a reference to the destination blob
                    // BlobClient destinationBlob = destinationContainerClient.GetBlobClient(destinationBlobName);
                    if (sasToken)
                    {
                        destinationBlob = _iBlobStorageService.GetBlobClientDetailsBySasToken(destinationBlobName, blobConnectionString);
                    }
                    else
                    {
                        destinationBlob = _iBlobStorageService.GetBlobClientDetails(destinationBlobName, blobConnectionString, destinationContainerName);
                    }
                        
                    if (bcParquetSourceLocation != null)
                    {
                        (bool ret, var flpProcessTempFile) = await new BlobStorageService().CopyBlobUsingStreamAsync(bcParquetSourceLocation, destinationBlob);

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


        public async Task<(bool, string)> MoveParquetFileToDeltaLakeStorageAccount(string detalakeStorageAccountPath, string parquetFilePath, BlobClient bcParquetSourceLocation,
            string blobConnectionString, FileProcessLogHistoryDto fileLogHistory, ISMBLibraryServices _ismbLibraryServices, DatabricksStorageAccountDto databricksStorageAccountDto)
        {
            //string currentTimeString = DateTime.Now.ToString("yyyyMMdd");
            string currentTimeString = DateTime.Now.ToString("yyyy/MMMM/dd");
            string directoryPath = ""; // Gets the directory part of the path
            string destinationFolderPath = "";

            destinationFolderPath = Path.GetDirectoryName(bcParquetSourceLocation.Name).Replace("\\", "/");
            // destinationFolderPath = $"{directoryPath}/{currentTimeString}";
            try
            {
                //detalakeStorageAccountPath = "parquetfile/source/";

                await AddFileProcessLosStatus(
                         fileType: fileLogHistory.fileType,
                         loginId: fileLogHistory.loginid,
                         message: $"Parquet file moving from this location folder:{destinationFolderPath}",
                         messageType: "info",
                         processId: fileLogHistory.processId,
                         processName: fileLogHistory.processName,
                         tableName: fileLogHistory.tableName,
                         totalRows: fileLogHistory.totalRows,
                         flpConfigurationId: fileLogHistory.flpConfigurationId,
                         fileUploadedId: fileLogHistory.fileUploadedId,
                         FileStatusActivityEnum.Processing,
                         FlpActivityLogStatusEnum.PlacedParquetFile
                     );
                string filePath = "";
                BlobClient destinationBlob = null;

                string destinationBlobName = $"{detalakeStorageAccountPath}{bcParquetSourceLocation.Name.Split('/').Last()}";

                if (databricksStorageAccountDto.SasKeyToken)
                {
                    //string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(databricksStorageAccountDto.FlpStorageAccount, databricksStorageAccountDto.StorageAccountKey);
                   string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionStringBySasKey(databricksStorageAccountDto.FlpStorageAccount, databricksStorageAccountDto.SasKey);
                   destinationBlob = _iBlobStorageService.GetBlobClientDetailsBySasToken(destinationBlobName, destinationBlobConnectionString);
                }
                else
                {
                    // string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(databricksStorageAccountDto.FlpStorageAccount, databricksStorageAccountDto.StorageAccountKey);
                    string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(databricksStorageAccountDto.FlpStorageAccount, databricksStorageAccountDto.StorageAccountKey);
                    // Parse the source blob URL
                    // Initialize the BlobServiceClient for the destination
                    // BlobServiceClient destinationBlobServiceClient = new BlobServiceClient(destinationBlobConnectionString);
                    // Get the destination container client
                    // BlobContainerClient destinationContainerClient = destinationBlobServiceClient.GetBlobContainerClient(destinationContainerName);
                    //  BlobContainerClient destinationContainerClient = destinationBlobServiceClient.GetBlobContainerClient(databricksStorageAccountDto.StorageContainerName);
                    // Get a reference to the destination blob
                   // destinationBlob = destinationContainerClient.GetBlobClient(destinationBlobName);

                    destinationBlob = _iBlobStorageService.GetBlobClientDetails(destinationBlobName, destinationBlobConnectionString, databricksStorageAccountDto.StorageContainerName);
                }                 

                if (bcParquetSourceLocation != null)
                {

                    (bool ret, var flpProcessTempFile) = await new BlobStorageService().CopyBlobUsingStreamAsync(bcParquetSourceLocation, destinationBlob);

                    if (ret)
                    {
                        if (ret && databricksStorageAccountDto.SasKeyToken)
                        {
                            var cleanUri = new Uri(flpProcessTempFile.Uri);
                            flpProcessTempFile.Uri = cleanUri.GetLeftPart(UriPartial.Path);
                        }

                        await AddFileProcessLosStatus(
                            fileType: fileLogHistory.fileType,
                            loginId: fileLogHistory.loginid,
                            message: $"Parquet file moving into location:{destinationBlobName}",
                            messageType: "info",
                            processId: fileLogHistory.processId,
                            processName: fileLogHistory.processName,
                            tableName: fileLogHistory.tableName,
                            totalRows: fileLogHistory.totalRows,
                            flpConfigurationId: fileLogHistory.flpConfigurationId,
                            fileUploadedId: fileLogHistory.fileUploadedId,
                            FileStatusActivityEnum.ProcessCompleted,
                            FlpActivityLogStatusEnum.PlacedParquetFile);
                        //Deleting source file
                        return (true, flpProcessTempFile.Uri);
                    }
                    else
                    {
                        await AddFileProcessLosStatus(
                            fileType: fileLogHistory.fileType,
                            loginId: fileLogHistory.loginid,
                            message: $"Parquet file not moved into location:{destinationBlobName}",
                            messageType: "info",
                            processId: fileLogHistory.processId,
                            processName: fileLogHistory.processName,
                            tableName: fileLogHistory.tableName,
                            totalRows: fileLogHistory.totalRows,
                            flpConfigurationId: fileLogHistory.flpConfigurationId,
                            fileUploadedId: fileLogHistory.fileUploadedId,
                            FileStatusActivityEnum.ProcessCompleted,
                            FlpActivityLogStatusEnum.PlacedParquetFile);
                        //Deleting source file
                        return (false, "something went wrong");
                    }

                }

                return (false, "something went wrong");

            }
            catch (Exception ex)
            {

                _logger.LogError($"check logerror line no 1117: {ex.Message}", ex);
                await AddFileProcessLosStatus(
                       fileType: fileLogHistory.fileType,
                       loginId: fileLogHistory.loginid,
                       message: $"Error: Parquet file moving from location: {destinationFolderPath}",
                       messageType: "error",
                       processId: fileLogHistory.processId,
                       processName: fileLogHistory.processName,
                       tableName: fileLogHistory.tableName,
                       totalRows: fileLogHistory.totalRows,
                       flpConfigurationId: fileLogHistory.flpConfigurationId,
                       fileUploadedId: fileLogHistory.fileUploadedId,
                       FileStatusActivityEnum.Error,
                       FlpActivityLogStatusEnum.PlacedParquetFile);
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
                fileUploadedId = fileUploadedId,


            };
            var ret = await _ifileLoadingProcessRepository.InsertFileProcessStatus(fileProcessLogHistoryDto);
            if (!ret)
            {
                _logger.LogError($"Error: Not inserted records for {flpConfigurationId}");
            }
        }


        public async Task AddFileProcessLosStatus(string fileType, string loginId, string message, string messageType,
      long processId, string processName, string tableName, int totalRows, string flpConfigurationId, string fileUploadedId,
      FileStatusActivityEnum fileStatusActivityEnum, FlpActivityLogStatusEnum flpActivityLogStatusEnum,string databricksAPIResponse)
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
                fileUploadedId = fileUploadedId,
                databricksAPIResponse = databricksAPIResponse


            };
            var ret = await _ifileLoadingProcessRepository.InsertFileProcessStatus(fileProcessLogHistoryDto);
            if (!ret)
            {
                _logger.LogError($"Error: Not inserted records for {flpConfigurationId}");
            }
        }

        private async Task<(bool, string)> DeleteParquetFileFromParquetLocation(long processId, string fileType, ParquetFileResponseDto resultResponse, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationRequestDto, SharedLocationDestinationServerDto slDestinationServerDto)
        {
            bool deleted = false;
            string messsage = "";

            
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




        private async Task<(bool, string)> DeleteTempFileFromTemptLocation(long processId, string fileType, FlpProcessTempFile flpProcessTempFile, ParquetFileResponseDto resultResponse, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationRequestDto, SharedLocationDestinationServerDto slDestinationServerDto)
        {
            bool deleted = false;
            string messsage = "";

            
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
                //deleted = await _iBlobStorageService.DeleteTempFileBlobAsync(flpProcessTempFile.Name, flpProcessTempFile.ParquetBlobConnectionString, flpProcessTempFile.BlobContainerName);
                //if (!deleted)
                //{
                //    messsage = "something went wrong.";
                //}
                if (fileType == "excel")
                {                   
                    deleted = await _iBlobStorageService.DeleteTempFileBlobAsync(flpProcessTempFile.Name, flpProcessTempFile.ParquetBlobConnectionString, flpProcessTempFile.BlobContainerName);
                    if (flpProcessTempFile.FileSize > 50)
                        await _iBlobStorageService.DeleteTempFileBlobAsync(flpProcessTempFile.CsvFile.CsvName, flpProcessTempFile.ParquetBlobConnectionString, flpProcessTempFile.BlobContainerName);
                   
                }
                else
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
       

        public string GetBearerToken()
        {
            var authorizationHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
            var authorizationHeader1 = _httpContextAccessor.HttpContext.Request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith("Bearer "))
            {
                return authorizationHeader.Substring("Bearer ".Length).Trim();
            }

            return null;
        }

        public async Task AddSecurityGroup(SecurityGroup securityGroups)
        {
            throw new NotImplementedException();
        }

        private async Task<bool> databricksJsonFileColumn(string fileURL, string columns, string flpConfigurationId, string uploadFileId, string tabName)
        {

            var dbResult = await _ifileLoadingProcessRepository.AddDatabricksJsonFileColumn(fileURL, columns, flpConfigurationId, uploadFileId, tabName);
            if (string.Compare(dbResult.Result, "Success", true) == 0)
            {
                return true;
            }
            return false;
        }

       

    }
}
