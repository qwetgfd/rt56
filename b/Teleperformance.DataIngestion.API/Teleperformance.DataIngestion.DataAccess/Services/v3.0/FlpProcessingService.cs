using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.Common.Crypto;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
//using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.ValidateFileRules;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingProcess.TableSchemaValidation;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Models.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Services.v3._0
{
    public class FlpProcessingService: IFlpProcessingService
    {
        private readonly IFileLoadingProcessRepository _ifileLoadingProcessRepository;
        private readonly IValidateSchemaService _iTableSchemaValidationService;
        private readonly IBronzeDbRepository _iInsertedDataToBronzeDbRepository;
        private readonly ILogger<FlpProcessingService> _logger;
        private readonly IBlobStorageService _iBlobStorageService;
        private readonly ISMBLibraryServices _iISMBLibraryServices;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FlpProcessingService(IFileLoadingProcessRepository ifileLoadingProcessRepository, ILogger<FlpProcessingService> logger, IValidateSchemaService iTableSchemaValidationService, IBronzeDbRepository iInsertedDataToBronzeDbRepository, IBlobStorageService iBlobStorageService, ISMBLibraryServices iISMBLibraryServices, IHttpContextAccessor httpContextAccessor)
        {
            _ifileLoadingProcessRepository = ifileLoadingProcessRepository;
            _logger = logger;
            _iTableSchemaValidationService = iTableSchemaValidationService;
            _iInsertedDataToBronzeDbRepository = iInsertedDataToBronzeDbRepository;
            _iBlobStorageService = iBlobStorageService;
            _iISMBLibraryServices = iISMBLibraryServices;
            _httpContextAccessor = httpContextAccessor;
        }
        public async Task<APIResponse<FlpProcessResponseDto>> ParquetFileProcessToBronzeTable(long processId, string fileType, string fileLocation, string? tabName, string connectionString, FlpActivityLogStatusEnum currentStatus, FlpProcessTempFile flpProcessTempFile, ParquetFileResponseDto resultResponse, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto)
        {
            FlpProcessResponseDto flpConvertToParquetResponseDto = new FlpProcessResponseDto();
            flpConvertToParquetResponseDto.TotalRows = resultResponse.TotalRows;
            flpConvertToParquetResponseDto.DuplicateRows = resultResponse.DuplicateRows;
            flpConvertToParquetResponseDto.BlobName = flpProcessTempFile.Name;
            MappingTableSchemaResult mappingTableSchemaResponse = null;
            string tableNameForBEValidation = "";
            List<string> keyColumnList = null;
            bool insertedData = false;
            int totalInsertedRows = 0;
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
            //Checked UI Validation flag is false          
            //Checked UI Validation 
            _logger.LogInformation($"Line No-82 UI Validation started for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}");
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
                var response = fileType=="excel" ? await validationRuleServiceHelper.ValidateExcelRules(bearerToken, true, flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.UploadedFileId, flpProcessTempFile?.Name??"",resultResponse.ParquetFilePath, destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey, destinationStorageAccountDto.StorageContainerName, null):
                    await validationRuleServiceHelper.ValidateCsvTextRules(bearerToken,true,flpProcessTempFile, flpConfigurationRequestDto, resultResponse, destinationStorageAccountDto);
                
                if (response?.ResultStatus.Code == APIResultStatus.Completed.Code)
                {
                    //Completed status
                    var result = response?.Result;
                    var responseMessage = result?.ResponseMessage;
                    //Checked failed rows
                    if(result?.FailedRows > 0)
                    {
                         await AddFileProcessLosStatus(
                             fileType: fileType,
                             loginId: "",
                             message: $"Error: Validation Failed for this file. Reason- {responseMessage} ",
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

                        _logger.LogError($"Line no 127 Error: Unable to complete UI Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId},Something went wrong {response?.ResultStatus.Code} {response?.ResponseMessage.FirstOrDefault() ?? ""} ");
                        //return
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { "Something went wrong" },
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
                      message: $"Error: UI Validation process Failed {response?.ResponseMessage.FirstOrDefault()}",
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

                    _logger.LogError($"Error: Unable to complete UI Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId},Something went wrong {response?.ResultStatus.Code} {response?.ResponseMessage.FirstOrDefault() ?? ""} ");
                    //return
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { $"Something went wrong {response?.ResultStatus.Code} {response?.ResponseMessage.FirstOrDefault()??""} " },
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
                tableNameForBEValidation = configurationTableMappingDto.TableName + "_Temp";
                await AddFileProcessLosStatus(
                fileType: fileType,
                loginId: "",
                message: $"Success: BE Validation process starting",
                messageType: "info",
                processId: processId,
                processName: flpConfigurationRequestDto.ProcessName,
                tableName: tableNameForBEValidation,
                totalRows: resultResponse.TotalRows,
                flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                FileStatusActivityEnum.Processing,
                FlpActivityLogStatusEnum.BEValidation
               );
                mappingTableSchemaResponse = null;
                mappingTableSchemaResponse = await _iTableSchemaValidationService.CreateTempTableFromBlob(resultResponse.ParquetBlobClient, connectionString, flpConfigurationRequestDto.ProcessName, tableNameForBEValidation, flpConfigurationRequestDto.FlpConfigurationId, tabName, resultResponse);

                if (mappingTableSchemaResponse != null && !mappingTableSchemaResponse.MatchSchema)
                {
                    await AddFileProcessLosStatus(
                        fileType: fileType,
                        loginId: "",
                        message: $"Backend checks failed: not created temp table {mappingTableSchemaResponse?.ErrorMessage}",
                        messageType: "error",
                        processId: processId,
                        processName: flpConfigurationRequestDto.ProcessName,
                        tableName: tableNameForBEValidation,
                        totalRows: resultResponse.TotalRows,
                        flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        FileStatusActivityEnum.Error,
                        FlpActivityLogStatusEnum.BEValidation
                    );

                    //return
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { "Something went wrong" },
                        Result = flpConvertToParquetResponseDto
                    });
                }

                //Not required firstTimeTableCreated
                (insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataInTempTableFromParquetStream(resultResponse.ParquetBlobClient, flpConfigurationRequestDto.FlpConfigurationId,
                                                connectionString, tableNameForBEValidation.Trim());


                if (!insertedData)
                {
                    _logger.LogError($"Backend checks failed:Not inserted data for backend checks {tableNameForBEValidation} flpConfigurationId {flpConfigurationRequestDto.FlpConfigurationId} tabName {configurationTableMappingDto.TabName}");
                    await AddFileProcessLosStatus(
                        fileType: fileType,
                        loginId: "",
                        message: $"Backend checks failed:Not inserted data for backend checks {configurationTableMappingDto.TableName}",
                        messageType: "error",
                        processId: processId,
                        processName: flpConfigurationRequestDto.ProcessName,
                        tableName: tableNameForBEValidation,
                        totalRows: resultResponse.TotalRows,
                        flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        FileStatusActivityEnum.Error,
                        FlpActivityLogStatusEnum.BEValidation
                       );

                    

                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { "Something went wrong" },
                        Result = flpConvertToParquetResponseDto
                    });


                }

               

               string bearerToken = GetBearerToken();
                var response = fileType == "excel" ? await validationRuleServiceHelper.ValidateExcelRules(bearerToken, false, flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.UploadedFileId, flpProcessTempFile?.Name ?? "", resultResponse.ParquetFilePath, destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey, destinationStorageAccountDto.StorageContainerName, null) :
                    await validationRuleServiceHelper.ValidateCsvTextRules(bearerToken, false, flpProcessTempFile, flpConfigurationRequestDto, resultResponse, destinationStorageAccountDto);

                (bool retMainTable, bool retHistoryTable) = await _ifileLoadingProcessRepository.dropMainTableAndHistoryTable(true, false, tableNameForBEValidation, "", connectionString);
                if (!retMainTable)
                {
                    await AddFileProcessLosStatus(
                           fileType: fileType,
                           loginId: "",
                           message: $"Error occurred: Dropped temp table: {configurationTableMappingDto.TableName}",
                           messageType: "error",
                           processId: processId,
                           processName: flpConfigurationRequestDto.ProcessName,
                           tableName: tableNameForBEValidation,
                           totalRows: resultResponse.TotalRows,
                           flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                           fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                           FileStatusActivityEnum.Error,
                           FlpActivityLogStatusEnum.BEValidation
                      );

                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { "Something went wrong" },
                        Result = flpConvertToParquetResponseDto
                    });
                }
                if (response.ResultStatus.Code == APIResultStatus.Completed.Code)
                {
                    var result = response.Result;
                    var responseMessage = result.ResponseMessage;
                    //Checked failed rows
                    if (result.FailedRows > 0)
                    {
                        await AddFileProcessLosStatus(
                            fileType: fileType,
                            loginId: "",
                            message: $"Error: Backend Checks Validation Failed for this file. Reason- {responseMessage} ",
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

                        _logger.LogError($"Error: Unable to complete Backend checks Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId},{responseMessage}");
                        //return
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { "Something went wrong" },
                            Result = flpConvertToParquetResponseDto
                        });
                    }
                    else
                    {
                        await AddFileProcessLosStatus(
                             fileType: fileType,
                             loginId: "",
                             message: $"Success: Backend checks Validation process completed",
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
                     message: $"Error: Backend checks Validation process Failed {response?.ResponseMessage.FirstOrDefault()}",
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

                    _logger.LogError($"Error: Unable to complete Backend checks Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}");
                    //return
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
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
                message: $"Success: Backend checks Validation processing stage is skipped",
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
            mappingTableSchemaResponse = null;
            if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.OnPrem)
            {
                mappingTableSchemaResponse = await _iTableSchemaValidationService.CreateBronzeTableFromSharedLocation(resultResponse.ParquetFilePath, connectionString, flpConfigurationRequestDto.ProcessName, configurationTableMappingDto.TableName, flpConfigurationRequestDto.FlpConfigurationId, tabName, slDestinationServerDto, resultResponse);
            }
            else
            {
                mappingTableSchemaResponse = await _iTableSchemaValidationService.CreateBronzeTableFromBlob(resultResponse.ParquetBlobClient, connectionString, flpConfigurationRequestDto.ProcessName, configurationTableMappingDto.TableName, flpConfigurationRequestDto.FlpConfigurationId, tabName, resultResponse);
            }

            if (mappingTableSchemaResponse != null && !mappingTableSchemaResponse.MatchSchema)
            {
                await AddFileProcessLosStatus(
                    fileType: fileType,
                    loginId: "",
                    message: $"Error: Bronze table not created {mappingTableSchemaResponse?.ErrorMessage}",
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

                //return
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Something went wrong" },
                    Result = flpConvertToParquetResponseDto
                });
            }


            //Not required firstTimeTableCreated
            if (!string.IsNullOrWhiteSpace(configurationTableMappingDto.KeyColumnList) && configurationTableMappingDto.mergeData)
            {
                var keyColumn = FlpConfigurationHelper.SplitString(configurationTableMappingDto.KeyColumnList, ",");
                var fileColumnNameList = await _iTableSchemaValidationService.GetFileColumnList(configurationTableMappingDto.FlpConfigurationId);
                if (fileColumnNameList.Any())
                {
                    keyColumnList = fileColumnNameList.Where(x => keyColumn.Contains(x.FileColumn)).Select(dc => dc.DbColumn).ToList();
                }
            }

            if (keyColumnList != null && configurationTableMappingDto.mergeData && keyColumnList.Any())
            {
                if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.OnPrem)
                {
                    if (mappingTableSchemaResponse != null && mappingTableSchemaResponse.FirstTimeTableCreated)
                    {
                        (insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataFromParquetSharedLocationAsync(resultResponse.ParquetFilePath,
                                              connectionString, configurationTableMappingDto.TableName.Trim(), flpConfigurationRequestDto.FlpConfigurationId, slDestinationServerDto, configurationTableMappingDto.mergeData, configurationTableMappingDto.createHistoryTable, configurationTableMappingDto.historyTableName, flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.UploadedFileName);
                    }
                    else
                    {
                        (insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataFromParquetSharedLocationAsyncV3(resultResponse.ParquetFilePath,
                                              connectionString, configurationTableMappingDto.TableName.Trim(), flpConfigurationRequestDto.FlpConfigurationId, slDestinationServerDto, keyColumnList,
                                              configurationTableMappingDto.createHistoryTable, configurationTableMappingDto.historyTableName, flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.UploadedFileName);
                    }

                    flpConvertToParquetResponseDto.InsertedRows = totalInsertedRows;

                }
                else
                {
                    if (mappingTableSchemaResponse != null && mappingTableSchemaResponse.FirstTimeTableCreated)
                        (insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataFromParquetStreamV2(resultResponse.ParquetBlobClient, flpConfigurationRequestDto.FlpConfigurationId,
                                               connectionString, configurationTableMappingDto.TableName.Trim(), configurationTableMappingDto.mergeData, configurationTableMappingDto.createHistoryTable, configurationTableMappingDto.historyTableName, flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.UploadedFileName);
                    else
                        (insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataFromParquetStreamV3(resultResponse.ParquetBlobClient, flpConfigurationRequestDto.FlpConfigurationId,
                                               connectionString, configurationTableMappingDto.TableName.Trim(), keyColumnList, configurationTableMappingDto.createHistoryTable, configurationTableMappingDto.historyTableName, flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.UploadedFileName);

                    flpConvertToParquetResponseDto.InsertedRows = totalInsertedRows;
                }


            }
            else
            {
                if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.OnPrem)
                {
                    (insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataFromParquetSharedLocationAsync(resultResponse.ParquetFilePath,
                                               connectionString, configurationTableMappingDto.TableName.Trim(), flpConfigurationRequestDto.FlpConfigurationId, slDestinationServerDto, flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.UploadedFileName);
                    flpConvertToParquetResponseDto.InsertedRows = totalInsertedRows;
                }
                else
                {

                    (insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataFromParquetStreamV2(resultResponse.ParquetBlobClient, flpConfigurationRequestDto.FlpConfigurationId,
                                              connectionString, configurationTableMappingDto.TableName.Trim(), flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.UploadedFileName);
                    flpConvertToParquetResponseDto.InsertedRows = totalInsertedRows;
                }
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
            (bool fileDelteed, string parquetFileResponse) = await DeleteParquetFileFromParquetLocation(processId, fileType, resultResponse, configurationTableMappingDto, flpConfigurationRequestDto, slDestinationServerDto);
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
            (bool tempFileDelteed, string tempFileResponse) = await DeleteTempFileFromTemptLocation(processId, fileType, flpProcessTempFile, resultResponse, configurationTableMappingDto, flpConfigurationRequestDto, slDestinationServerDto);
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
            flpProcessTempFile.FileSize = 0;
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

                if (flpConfigurationRequestDto.BlobClients !=null &&  !string.IsNullOrWhiteSpace(flpConfigurationRequestDto.BlobClients.Uri) && !string.IsNullOrWhiteSpace(flpConfigurationRequestDto.BlobClients.Name))
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
                    string sourceBlobContainer = flpConfigurationRequestDto.SourceContainerName;

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
                    BlobProperties properties = await sourceBlobClient.GetPropertiesAsync();
                    double fileSizeInMB = properties.ContentLength / (1024.0 * 1024.0);

                    //destination

                    string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey); ;
                    
                    string destinationContainerName = destinationStorageAccountDto.StorageContainerName;
                    string destinationBlobUrl = $"{flpConfigurationRequestDto.DestinationPath}temp/"; //Moved file into temporary storage
                    string destinationBlobName = $"{destinationBlobUrl}{backUpFileName}";// {sourceBlobClient.Name.Split('/').Last()}";

                    BlobClient destinationBlobClient = _iBlobStorageService.GetBlobClientDetails(destinationBlobName, destinationBlobConnectionString, destinationContainerName);
                    // (isCopiedFile, flpProcessTempFile) = await _iBlobStorageService.CopyFileSourceBlobToDestinationBlobAsync(sourceBlobClient, destinationBlobClient);
                    (isCopiedFile,  flpProcessTempFile) = await new BlobStorageService().CopyBlobUsingStreamAsync(sourceBlobClient, destinationBlobClient);


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
                    message: $"Error: File moving  failed to temporary storage",
                    messageType: "error",
                    processId: processId,
                    processName: flpConfigurationRequestDto.ProcessName,
                    tableName: configurationTableMappingDto.TableName,
                    totalRows: 0,
                    flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                    fileUploadedId: fileUploadedId,
                    FileStatusActivityEnum.Error,
                    FlpActivityLogStatusEnum.FileMovedToTempStorage);
                    _logger.LogError($"Error: File moving  failed to temporary storage for {flpConfigurationRequestDto.FlpConfigurationId}", "flpProcessTempFile is null");
                    return (null, false);
                }
                return (flpProcessTempFile, true);
            }
            catch (Exception ex)
            {
                await AddFileProcessLosStatus(
                                    fileType: fileType,
                                    loginId: "",
                                    message: $"Error: File moving  failed to temporary storage",
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

                _logger.LogError($"Error: File moving  failed to temporary storage for {flpConfigurationRequestDto.FlpConfigurationId}", ex.Message.ToString());
                return (null, false);
            }
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
                    string sourceBlobContainer = flpConfigurationRequestDto.SourceContainerName;

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

                    BlobClient destinationBlobClient = _iBlobStorageService.GetBlobClientDetails(destinationBlobName, destinationBlobConnectionString, destinationContainerName);
                    //(isCopiedFile, flpProcessTempFile) = await _iBlobStorageService.CopyFileSourceBlobToDestinationBlobAsync(sourceBlobClient, destinationBlobClient);
                    (isCopiedFile, flpProcessTempFile) = await new BlobStorageService().CopyBlobUsingStreamAsync(sourceBlobClient, destinationBlobClient);


                    if (fileSizeInMB > 50)
                    {
                        _logger.LogInformation($"File size is greater than 50 MB, creating CSV file for {flpConfigurationRequestDto.FlpConfigurationId}");
                        (isCsvFileCopiedFile, flpCsvProcessTempFile) = await _iBlobStorageService.CreateCsvFileFromSourceFile(sourceBlobClient, destinationBlobClient, destinationContainerName, destinationBlobConnectionString);
                        if (isCsvFileCopiedFile)
                        {
                            flpProcessTempFile.CsvFile = flpCsvProcessTempFile.CsvFile;
                        }
                    }
                    //File Size wiil be used for excel only
                    flpProcessTempFile.FileSize= fileSizeInMB;
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

                    if (isCopiedFile && flpProcessTempFile.FileSize > 50)
                    {
                        _logger.LogInformation($"File size is greater than 50 MB, creating CSV file for {flpConfigurationRequestDto.FlpConfigurationId}");
                        string csvBlobName = Path.ChangeExtension(destinationBlobClient.Name, ".csv");
                        BlobClient csvBlobClient = _iBlobStorageService.GetBlobClientDetails(csvBlobName,destinationBlobConnectionString, destinationContainerName);

                        (isCsvFileCopiedFile, flpCsvProcessTempFile) = await FlpConfigurationHelper.CreateCsvFromExcelUsingSmbLibraryAsync(flpConfigurationRequestDto.FlpConfigurationId, destinationBlobClient, sourceServer, csvBlobClient, _iISMBLibraryServices);
                        if (isCsvFileCopiedFile)
                        {
                            flpProcessTempFile.CsvFile = flpCsvProcessTempFile.CsvFile;
                        }


                    }
                    //File Size wiil be used for excel only
                   // flpProcessTempFile.FileSize = fileSizeInMB;

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
                    message: $"Error: File moving  failed to temporary storage",
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



        public async Task<(bool, string)> MoveParquetToArchiveAndDeletedMainFileV2(int destinationLocationTypeId, string parquetFilePath, BlobClient bcParquetSourceLocation, string blobConnectionString, string destinationContainerName, FileProcessLogHistoryDto fileLogHistory, SharedLocationDestinationServerDto slDestinationServerDto, ISMBLibraryServices _ismbLibraryServices)
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

        private async Task<(bool, string)> DeleteParquetFileFromParquetLocation(long processId, string fileType, ParquetFileResponseDto resultResponse, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationRequestDto, SharedLocationDestinationServerDto slDestinationServerDto)
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




        private async Task<(bool, string)> DeleteTempFileFromTemptLocation(long processId, string fileType, FlpProcessTempFile flpProcessTempFile, ParquetFileResponseDto resultResponse, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationRequestDto, SharedLocationDestinationServerDto slDestinationServerDto)
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
                if (fileType == "excel")
                {
                   //deleted = await _iBlobStorageService.DeleteAllBlobsAsync(flpProcessTempFile.ParquetBlobConnectionString, flpProcessTempFile.BlobContainerName);
                    deleted = await _iBlobStorageService.DeleteTempFileBlobAsync(flpProcessTempFile.Name, flpProcessTempFile.ParquetBlobConnectionString, flpProcessTempFile.BlobContainerName);
                    if(flpProcessTempFile.FileSize > 20)
                       await _iBlobStorageService.DeleteTempFileBlobAsync(flpProcessTempFile.CsvFile.CsvName, flpProcessTempFile.ParquetBlobConnectionString, flpProcessTempFile.BlobContainerName);
                   //deleted = await _iBlobStorageService.DeleteTempFileBlobAsync(flpProcessTempFile.ParquetBlobConnectionString, flpProcessTempFile.BlobContainerName);
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


    }
}
