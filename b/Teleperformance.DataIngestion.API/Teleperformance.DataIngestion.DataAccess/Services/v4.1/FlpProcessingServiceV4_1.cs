using AngleSharp.Dom;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DocumentFormat.OpenXml.Office.CustomUI;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NLog;
using NPOI.SS.Formula.Functions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.Common.Crypto;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v3._0;
using Teleperformance.DataIngestion.DatabricksAPI.Services;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService;
using Teleperformance.DataIngestion.Models.DTOs.v4._0;
//using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.ValidateFileRules;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingProcess.TableSchemaValidation;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Enums.v4._0;
using Teleperformance.DataIngestion.Models.Helpers;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using Teleperformance.DataIngestion.Models.Models.v4._1;
using static Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus.StatusResponseDto;
using ConfigurationTableMappingDto = Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess.ConfigurationTableMappingDto;
using FileProcessLogHistoryDto = Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingProcess.FileProcessLogHistoryDto;
using FileSettings = Teleperformance.DataIngestion.Models.Models.v4._1.FileSettings;

namespace Teleperformance.DataIngestion.DataAccess.Services.v4._1
{
 
    public class FlpProcessingServiceV4_1 : IFlpProcessingServiceV4_1
    {

        private readonly IFileLoadingProcessConfigurationRepositoryV4_1 _ifileLoadingProcessRepository;
        private readonly ILogger<FlpProcessingServiceV4_1> _logger;
        private readonly IBlobStorageServiceV4_1 _iBlobStorageService;
        private readonly ISMBLibraryServices _iISMBLibraryServices;
        private readonly IFileLoadingProcessRepository _fileDatabaseProcessRepo;
        private readonly IValidateSchemaServiceV4_1 _iTableSchemaValidationService;
        private readonly IBronzeDbRepository _iInsertedDataToBronzeDbRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IValidateSchemaService _iTableSchemaValidationService2;

        public FlpProcessingServiceV4_1(IFileLoadingProcessConfigurationRepositoryV4_1 ifileLoadingProcessRepository, ILogger<FlpProcessingServiceV4_1> logger, IBlobStorageServiceV4_1 iBlobStorageService, ISMBLibraryServices iISMBLibraryServices, IFileLoadingProcessRepository fileDatabaseProcessRepo, IValidateSchemaServiceV4_1 iTableSchemaValidationService, IBronzeDbRepository iInsertedDataToBronzeDbRepository, IValidateSchemaService iTableSchemaValidationService2, IHttpContextAccessor httpContextAccessor)
        {
            _ifileLoadingProcessRepository = ifileLoadingProcessRepository;
            _logger = logger;
            _iBlobStorageService = iBlobStorageService;
            _iISMBLibraryServices = iISMBLibraryServices;
            _fileDatabaseProcessRepo = fileDatabaseProcessRepo;
            _iTableSchemaValidationService = iTableSchemaValidationService;
            _iInsertedDataToBronzeDbRepository = iInsertedDataToBronzeDbRepository;
            _httpContextAccessor = httpContextAccessor;
            _iTableSchemaValidationService2 = iTableSchemaValidationService2;
        }


        public async Task<APIResponse<FlpProcessTabResponseDto>> ParquetFileProcessToBronzeTable(long processId, string fileType, string fileLocation,  string connectionString, FlpActivityLogStatusEnum currentStatus, FlpProcessTempFileModel flpProcessTempFile, ParquetFileResponseDtoV4_1 resultResponse,ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDtoV4_1 flpConfigurationRequestDto, DestinationStorageAccountDtoV4_1 destinationStorageAccountDto, SharedLocationDestinationServerDtoV4_1 slDestinationServerDto)
        {
            FlpProcessTabResponseDto flpConvertToParquetResponseDto = new FlpProcessTabResponseDto();

            flpConvertToParquetResponseDto.TotalRows = resultResponse.TotalRows;
            flpConvertToParquetResponseDto.DuplicateRows = resultResponse.DuplicateRows;
            flpConvertToParquetResponseDto.BlobName = flpProcessTempFile.Name;
            List<string> keyColumnList = null;
            MappingTableSchemaResult mappingTableSchemaResponse = null;
            var validationRuleServiceHelper = new ValidationRuleServiceHelper(_logger);
            string tableNameForBEValidation = "";
            bool insertedData = false;
            int totalInsertedRows = 0;

            await AddFileProcessLosStatus(
                 tabName: configurationTableMappingDto.TabName,
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
                 FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation,
                 null
            );

            
            //Checked UI Validation
            if (flpConfigurationRequestDto.UIValidation)
            {                                                             
                await AddFileProcessLosStatus(
                 tabName: configurationTableMappingDto.TabName,
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
                 FlpActivityLogStatusEnum.UIValidation,null
                );

                //Calling UI Validation API
                string bearerToken = GetBearerToken();
                var response = await validationRuleServiceHelper.ValidateExcelRules(bearerToken, true, flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.UploadedFileId,
                                      flpProcessTempFile.Name, resultResponse.ParquetFilePath, destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey,
                                      destinationStorageAccountDto.StorageContainerName, configurationTableMappingDto.TabName);
                if (response?.ResultStatus.Code == APIResultStatus.Completed.Code)
                {
                    //Completed status
                    var result = response?.Result;
                    var responseMessage = result?.ResponseMessage;                    
                    //Checked failed rows
                    if (result?.FailedRows > 0)
                    {
                        await AddFileProcessLosStatus(
                            tabName: configurationTableMappingDto.TabName,
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
                            FlpActivityLogStatusEnum.UIValidation, null
                        );

                        _logger.LogError($"Error: Unable to complete UI Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId},{responseMessage}");
                        //return
                        return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { "Something went wrong" },
                            Result = flpConvertToParquetResponseDto
                        });
                    }
                    else
                    {
                        await AddFileProcessLosStatus(
                            tabName: configurationTableMappingDto.TabName,
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
                            FlpActivityLogStatusEnum.UIValidation, null
                       );


                        //Dropped silver table if exists
                        //flpConfigurationRequestDto.silverTableName = configurationTableMappingDto.TableName;//+"_SILVER";

                        //if (configurationTableMappingDto.DropMainTable)
                        //{
                        //    //currentStatus = FlpActivityLogStatusEnum.DroppedMainTable;
                        //    await AddFileProcessLosStatus(
                        //           tabName: configurationTableMappingDto.TabName,
                        //           fileType: fileType,
                        //           loginId: "",
                        //           message: $"Dropping Silver table in processing:{flpConfigurationRequestDto.silverTableName}",
                        //           messageType: "info",
                        //           processId: processId,
                        //           processName: flpConfigurationRequestDto.ProcessName,
                        //           tableName: flpConfigurationRequestDto.silverTableName,
                        //           totalRows: resultResponse.TotalRows,
                        //           flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        //           fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        //           FileStatusActivityEnum.Processing,
                        //           FlpActivityLogStatusEnum.DropedSilverTable, null
                        //      );
                        //    (bool retMainTable, bool retHistoryTable) = await _fileDatabaseProcessRepo.dropMainTableAndHistoryTable(configurationTableMappingDto.DropMainTable, false, flpConfigurationRequestDto.silverTableName, "", connectionString);
                        //    if (retMainTable)
                        //    {
                        //        await AddFileProcessLosStatus(
                        //               tabName: configurationTableMappingDto.TabName,
                        //               fileType: fileType,
                        //               loginId: "",
                        //               message: $"Dropped Silver table: {flpConfigurationRequestDto.silverTableName}",
                        //               messageType: "info",
                        //               processId: processId,
                        //               processName: flpConfigurationRequestDto.ProcessName,
                        //               tableName: flpConfigurationRequestDto.silverTableName,
                        //               totalRows: resultResponse.TotalRows,
                        //               flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        //               fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        //               FileStatusActivityEnum.ProcessCompleted,
                        //               FlpActivityLogStatusEnum.DropedSilverTable, null
                        //          );
                        //    }
                        //    else
                        //    {
                        //        await AddFileProcessLosStatus(
                        //            tabName: configurationTableMappingDto.TabName,
                        //            fileType: fileType,
                        //            loginId: "",
                        //            message: $"Error in dropping Main table: {flpConfigurationRequestDto.silverTableName}",
                        //            messageType: "error",
                        //            processId: processId,
                        //            processName: flpConfigurationRequestDto.ProcessName,
                        //            tableName: configurationTableMappingDto.TableName,
                        //            totalRows: resultResponse.TotalRows,
                        //            flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        //            fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        //            FileStatusActivityEnum.Error,
                        //            FlpActivityLogStatusEnum.DropedSilverTable, null
                        //        );

                        //        _logger.LogError($"Error in dropping Silver table: {flpConfigurationRequestDto.silverTableName}");
                        //        //return
                        //        return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                        //        {
                        //            ResultStatus = APIResultStatus.InvalidParameters,
                        //            ResponseMessage = new List<string> { "Something went wrong" },
                        //            Result = null
                        //        });
                        //    }

                        //}
                        //else
                        //{
                        //    await AddFileProcessLosStatus(
                        //        tabName: configurationTableMappingDto.TabName,
                        //       fileType: fileType,
                        //       loginId: "",
                        //       message: $"Not dropped Silver table: {flpConfigurationRequestDto.silverTableName}",
                        //       messageType: "info",
                        //       processId: processId,
                        //       processName: flpConfigurationRequestDto.ProcessName,
                        //       tableName: flpConfigurationRequestDto.silverTableName,
                        //       totalRows: resultResponse.TotalRows,
                        //       flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        //       fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        //       FileStatusActivityEnum.Skip,
                        //       FlpActivityLogStatusEnum.DropedSilverTable, null
                        //   );
                        //}

                        //MappingTableSchemaResult mappingTableSchemaResponseV2 = await _iTableSchemaValidationService.CreateBronzeTableFromBlob(resultResponse.ParquetBlobClient, connectionString, flpConfigurationRequestDto.ProcessName, configurationTableMappingDto.TableName, flpConfigurationRequestDto.FlpConfigurationId, configurationTableMappingDto.TabName, resultResponse);

                        //if (mappingTableSchemaResponseV2 != null && !mappingTableSchemaResponseV2.MatchSchema)
                        //{
                        //    await AddFileProcessLosStatus(
                        //        tabName: configurationTableMappingDto.TabName,
                        //        fileType: fileType,
                        //        loginId: "",
                        //        message: $"Error: Silver table not created {mappingTableSchemaResponseV2?.ErrorMessage}",
                        //        messageType: "error",
                        //        processId: processId,
                        //        processName: flpConfigurationRequestDto.ProcessName,
                        //        tableName: configurationTableMappingDto.TableName,
                        //        totalRows: resultResponse.TotalRows,
                        //        flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        //        fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        //        FileStatusActivityEnum.Error,
                        //        FlpActivityLogStatusEnum.SilverTableDataInserted, null
                        //    );

                        //    //return
                        //    return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                        //    {
                        //        ResultStatus = APIResultStatus.InvalidParameters,
                        //        ResponseMessage = new List<string> { "Something went wrong" },
                        //        Result = null
                        //    });
                        //}

                        ////Not required firstTimeTableCreated
                        //if (!string.IsNullOrWhiteSpace(configurationTableMappingDto.KeyColumnList) && configurationTableMappingDto.mergeData)
                        //{
                        //    var keyColumn = FlpConfigurationHelper.SplitString(configurationTableMappingDto.KeyColumnList, ",");
                        //    var fileColumnNameList = await _iTableSchemaValidationService.GetFileColumnList(configurationTableMappingDto.FlpConfigurationId);
                        //    if (fileColumnNameList.Any())
                        //    {
                        //        keyColumnList = fileColumnNameList.Where(x => keyColumn.Contains(x.FileColumn)).Select(dc => dc.DbColumn).ToList();
                        //    }
                        //}

                        //if (keyColumnList != null && configurationTableMappingDto.mergeData && keyColumnList.Any())
                        //{
                        //    //configurationTableMappingDto.TableName = flpConfigurationRequestDto.silverTableName;
                        //    if (mappingTableSchemaResponseV2 != null && mappingTableSchemaResponseV2.FirstTimeTableCreated)
                        //        (insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataFromParquetStreamV2(resultResponse.ParquetBlobClient, flpConfigurationRequestDto.FlpConfigurationId,
                        //                               connectionString, flpConfigurationRequestDto.silverTableName, configurationTableMappingDto.mergeData, configurationTableMappingDto.createHistoryTable, flpConfigurationRequestDto.silverTableName + "_History", flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.UploadedFileName);
                        //    else
                        //        (insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataFromParquetStreamV3(resultResponse.ParquetBlobClient, flpConfigurationRequestDto.FlpConfigurationId,
                        //                               connectionString, flpConfigurationRequestDto.silverTableName, keyColumnList, configurationTableMappingDto.createHistoryTable, flpConfigurationRequestDto.silverTableName + "_History", flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.UploadedFileName);

                        //    flpConvertToParquetResponseDto.InsertedRows = totalInsertedRows;


                        //}
                        //else
                        //{
                        //    if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.OnPrem)
                        //    {
                        //        //(insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataFromParquetSharedLocationAsync(resultResponse.ParquetFilePath,
                        //        //                           connectionString, flpConfigurationRequestDto.silverTableName, flpConfigurationRequestDto.FlpConfigurationId, slDestinationServerDto, flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.UploadedFileName);
                        //        //flpConvertToParquetResponseDto.InsertedRows = totalInsertedRows;
                        //    }
                        //    else
                        //    {

                        //        (insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataFromParquetStreamV2(resultResponse.ParquetBlobClient, flpConfigurationRequestDto.FlpConfigurationId,
                        //                                  connectionString, flpConfigurationRequestDto.silverTableName, flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.UploadedFileName);
                        //        flpConvertToParquetResponseDto.InsertedRows = totalInsertedRows;
                        //    }
                        //}


                        //if (insertedData)
                        //{
                        //    await AddFileProcessLosStatus(
                        //      tabName: configurationTableMappingDto.TabName,
                        //      fileType: fileType,
                        //      loginId: "",
                        //      message: $"Success: Inserting data in table {flpConfigurationRequestDto.silverTableName}",
                        //      messageType: "info",
                        //      processId: processId,
                        //      processName: flpConfigurationRequestDto.ProcessName,
                        //      tableName: flpConfigurationRequestDto.silverTableName,
                        //      totalRows: resultResponse.TotalRows,
                        //      flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        //      fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        //      FileStatusActivityEnum.ProcessCompleted,
                        //      FlpActivityLogStatusEnum.SilverTableDataInserted, null);

                        //}
                        //else
                        //{
                        //    await AddFileProcessLosStatus(
                        //     tabName: configurationTableMappingDto.TabName,
                        //     fileType: fileType,
                        //     loginId: "",
                        //     message: $"Error: Inserting data in table {flpConfigurationRequestDto.silverTableName}",
                        //     messageType: "error",
                        //     processId: processId,
                        //     processName: flpConfigurationRequestDto.ProcessName,
                        //     tableName: configurationTableMappingDto.TableName,
                        //     totalRows: resultResponse.TotalRows,
                        //     flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        //     fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        //     FileStatusActivityEnum.Error,
                        //     FlpActivityLogStatusEnum.SilverTableDataInserted, null
                        // );

                        //    // _logger.LogError($"Error Inserting data in table : {flpConfigurationRequestDto.TableName}");

                        //    //return
                        //    return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                        //    {
                        //        ResultStatus = APIResultStatus.InvalidParameters,
                        //        ResponseMessage = new List<string> { "Something went wrong" },
                        //        Result = null
                        //    });


                        //}
                    }
                }
                else
                {
                    await AddFileProcessLosStatus(
                      tabName: configurationTableMappingDto.TabName,
                      fileType: fileType,
                      loginId: "",
                      message: $"Error: UI Validation process Failed. {response?.ResponseMessage.FirstOrDefault()}",
                      messageType: "info",
                      processId: processId,
                      processName: flpConfigurationRequestDto.ProcessName,
                      tableName: configurationTableMappingDto.TableName,
                      totalRows: resultResponse.TotalRows,
                      flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                      fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                      FileStatusActivityEnum.Error,
                      FlpActivityLogStatusEnum.UIValidation, null
                     );

                    _logger.LogError($"Error: Unable to complete UI Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}");
                    //return
                    return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
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
                    tabName: configurationTableMappingDto.TabName,
                    fileType: fileType,
                    loginId: "",
                    message: $"Success: UI Validation processing stage skipped",
                    messageType: "info",
                    processId: processId,
                    processName: flpConfigurationRequestDto.ProcessName,
                    tableName: configurationTableMappingDto.TableName,
                    totalRows: resultResponse.TotalRows,
                    flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                    fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                    FileStatusActivityEnum.Skip,
                    FlpActivityLogStatusEnum.UIValidation, null
                   );
            }

            if (flpConfigurationRequestDto.BEValidation)
            {
                tableNameForBEValidation = configurationTableMappingDto.TableName + "_Temp";
                await AddFileProcessLosStatus(
                          tabName: tableNameForBEValidation,
                          fileType: fileType,
                          loginId: "",
                          message: $"Success: BE Validation processing starting",
                          messageType: "info",
                          processId: processId,
                          processName: flpConfigurationRequestDto.ProcessName,
                          tableName: tableNameForBEValidation,
                          totalRows: resultResponse.TotalRows,
                          flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                          fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                          FileStatusActivityEnum.Processing,
                          FlpActivityLogStatusEnum.BEValidation, null
                       );
                
                //Need to add the records from the parquet file to bronze table
                mappingTableSchemaResponse = null;
                mappingTableSchemaResponse = await _iTableSchemaValidationService2.CreateTempTableFromBlob(resultResponse.ParquetBlobClient, connectionString, flpConfigurationRequestDto.ProcessName, tableNameForBEValidation, flpConfigurationRequestDto.FlpConfigurationId, tableNameForBEValidation, resultResponse);

                if (mappingTableSchemaResponse != null && !mappingTableSchemaResponse.MatchSchema)
                {
                    _logger.LogError($"Multisheet Error:temporary table not created {mappingTableSchemaResponse?.ErrorMessage} for table {tableNameForBEValidation} flpConfigurationId {flpConfigurationRequestDto.FlpConfigurationId} tabName {configurationTableMappingDto.TabName}");
                    await AddFileProcessLosStatus(
                        tabName: tableNameForBEValidation,
                        fileType: fileType,
                        loginId: "",
                        message: $"Error: temp table not created  {mappingTableSchemaResponse?.ErrorMessage}",
                        messageType: "error",
                        processId: processId,
                        processName: flpConfigurationRequestDto.ProcessName,
                        tableName: tableNameForBEValidation,
                        totalRows: resultResponse.TotalRows,
                        flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        FileStatusActivityEnum.Error,
                        FlpActivityLogStatusEnum.BEValidation,
                         null
                    );                    
                    return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { "Something went wrong" },
                        Result = null
                    });
                }

                //Not required firstTimeTableCreated
                (insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataInTempTableFromParquetStream(resultResponse.ParquetBlobClient, flpConfigurationRequestDto.FlpConfigurationId,
                                                connectionString, tableNameForBEValidation.Trim());
               // flpConvertToParquetResponseDto.InsertedRows = totalInsertedRows;

                if (!insertedData)
                {
                    _logger.LogError($"Multisheet Error:BE Validations Inserting data in table for table {tableNameForBEValidation} flpConfigurationId {flpConfigurationRequestDto.FlpConfigurationId} tabName {configurationTableMappingDto.TabName}");
                    await AddFileProcessLosStatus(
                     tabName: tableNameForBEValidation,
                     fileType: fileType,
                     loginId: "",
                     message: $"Error: Inserting data in table {tableNameForBEValidation}",
                     messageType: "error",
                     processId: processId,
                     processName: flpConfigurationRequestDto.ProcessName,
                     tableName: tableNameForBEValidation,
                     totalRows: resultResponse.TotalRows,
                     flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                     fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                     FileStatusActivityEnum.Error,
                     FlpActivityLogStatusEnum.BEValidation,
                         null
                   );

                    //return

                    return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { "Something went wrong" },
                        Result = flpConvertToParquetResponseDto
                    });


                }
               
               
                string bearerToken = GetBearerToken();
                var response = await validationRuleServiceHelper.ValidateExcelRules(bearerToken, false, flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.UploadedFileId,
                                       flpProcessTempFile.Name, resultResponse.ParquetFilePath, destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey, destinationStorageAccountDto.StorageContainerName,  configurationTableMappingDto.TabName);
                //Delete temporary table after BE Validation
                (bool retMainTable, bool retHistoryTable) = await _fileDatabaseProcessRepo.dropMainTableAndHistoryTable(true, false, tableNameForBEValidation, "", connectionString);
                if (!retMainTable)
                {
                    _logger.LogError($"Error occurred for table {tableNameForBEValidation} flpConfigurationId {flpConfigurationRequestDto.FlpConfigurationId} tabName {configurationTableMappingDto.TabName}");
                    await AddFileProcessLosStatus(
                     tabName: tableNameForBEValidation,
                     fileType: fileType,
                     loginId: "",
                     message: $"Error: Not dropped temp table {tableNameForBEValidation}",
                     messageType: "error",
                     processId: processId,
                     processName: flpConfigurationRequestDto.ProcessName,
                     tableName: tableNameForBEValidation,
                     totalRows: resultResponse.TotalRows,
                     flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                     fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                     FileStatusActivityEnum.Error,
                     FlpActivityLogStatusEnum.BEValidation,
                         null
                   );

                    //return

                    return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { "Something went wrong" },
                        Result = flpConvertToParquetResponseDto
                    });
                }
               
                if (response.ResultStatus.Code == APIResultStatus.Completed.Code)
                {
                    //Completed status
                    var result = response.Result;
                    var responseMessage = result.ResponseMessage;
                    //Checked failed rows
                    if (result.FailedRows > 0)
                    {
                        //Here we need to delete the records from bronze table which are failed during BE Validation based on conditions flpConfigurationId, uploadedFileId

                        await AddFileProcessLosStatus(
                            tabName: configurationTableMappingDto.TabName,
                            fileType: fileType,
                            loginId: "",
                            message: $"Error: BE Validation Failed for this file. Reason- {responseMessage} ",
                            messageType: "error",
                            processId: processId,
                            processName: flpConfigurationRequestDto.ProcessName,
                            tableName: configurationTableMappingDto.TableName,
                            totalRows: resultResponse.TotalRows,
                            flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                            fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                            FileStatusActivityEnum.Error,
                            FlpActivityLogStatusEnum.BEValidation, null
                        );

                        _logger.LogError($"Error: Unable to complete UI Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId},{responseMessage}");
                        //return
                        return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { "Something went wrong" },
                            Result = flpConvertToParquetResponseDto
                        });
                    }
                    else
                    {
                        await AddFileProcessLosStatus(
                            tabName: configurationTableMappingDto.TabName,
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
                            FlpActivityLogStatusEnum.BEValidation, null
                         );
                        //if (!configurationTableMappingDto.DropMainTable)
                        //{
                        //    await AddFileProcessLosStatus(
                        //    tabName: configurationTableMappingDto.TabName,
                        //    fileType: fileType,
                        //    loginId: "",
                        //    message: $"Success: DroppedGoldTable process skipped",
                        //    messageType: "info",
                        //    processId: processId,
                        //    processName: flpConfigurationRequestDto.ProcessName,
                        //    tableName: configurationTableMappingDto.TableName,
                        //    totalRows: resultResponse.TotalRows,
                        //    flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        //    fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        //    FileStatusActivityEnum.Skip,
                        //    FlpActivityLogStatusEnum.DropedGoldTable, null
                        // );

                        //}
                        //else
                        //{
                        //    await AddFileProcessLosStatus(
                        //   tabName: configurationTableMappingDto.TabName,
                        //   fileType: fileType,
                        //   loginId: "",
                        //   message: $"Success: DroppedGoldTable processing starting",
                        //   messageType: "info",
                        //   processId: processId,
                        //   processName: flpConfigurationRequestDto.ProcessName,
                        //   tableName: configurationTableMappingDto.TableName,
                        //   totalRows: resultResponse.TotalRows,
                        //   flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        //   fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        //   FileStatusActivityEnum.Processing,
                        //   FlpActivityLogStatusEnum.DropedGoldTable, null
                        //   );
                        //    await AddFileProcessLosStatus(
                        //  tabName: configurationTableMappingDto.TabName,
                        //  fileType: fileType,
                        //  loginId: "",
                        //  message: $"Success: DroppedGoldTable processing completed",
                        //  messageType: "info",
                        //  processId: processId,
                        //  processName: flpConfigurationRequestDto.ProcessName,
                        //  tableName: configurationTableMappingDto.TableName,
                        //  totalRows: resultResponse.TotalRows,
                        //  flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        //  fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        //  FileStatusActivityEnum.ProcessCompleted,
                        //  FlpActivityLogStatusEnum.DropedGoldTable, null
                        //  );
                        //}

                        //await AddFileProcessLosStatus(
                        //    tabName: configurationTableMappingDto.TabName,
                        //    fileType: fileType,
                        //    loginId: "",
                        //    message: $"Success: DroppedGoldTable inserted processing started",
                        //    messageType: "info",
                        //    processId: processId,
                        //    processName: flpConfigurationRequestDto.ProcessName,
                        //    tableName: configurationTableMappingDto.TableName,
                        //    totalRows: resultResponse.TotalRows,
                        //    flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        //    fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        //    FileStatusActivityEnum.Processing,
                        //    FlpActivityLogStatusEnum.GoldTableDataInserted, null
                        // );

                        //await AddFileProcessLosStatus(
                        //        tabName: configurationTableMappingDto.TabName,
                        //        fileType: fileType,
                        //        loginId: "",
                        //        message: $"Success: DroppedGoldTable inserted processing completed",
                        //        messageType: "info",
                        //        processId: processId,
                        //        processName: flpConfigurationRequestDto.ProcessName,
                        //        tableName: configurationTableMappingDto.TableName,
                        //        totalRows: resultResponse.TotalRows,
                        //        flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                        //        fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                        //        FileStatusActivityEnum.ProcessCompleted,
                        //        FlpActivityLogStatusEnum.GoldTableDataInserted, null
                        //     );



                    }
                }
                else
                {
                    await AddFileProcessLosStatus(
                      tabName: configurationTableMappingDto.TabName,
                      fileType: fileType,
                      loginId: "",
                      message: $"Error: BE Validation process Failed. {response?.ResponseMessage.FirstOrDefault()}",
                      messageType: "info",
                      processId: processId,
                      processName: flpConfigurationRequestDto.ProcessName,
                      tableName: configurationTableMappingDto.TableName,
                      totalRows: resultResponse.TotalRows,
                      flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                      fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                      FileStatusActivityEnum.Error,
                      FlpActivityLogStatusEnum.BEValidation, null
                     );

                    _logger.LogError($"Error: Unable to complete BE Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}");
                    //return
                    return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
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
                          tabName: configurationTableMappingDto.TabName,
                          fileType: fileType,
                          loginId: "",
                          message: $"Success: BE Validation processing stage skipped",
                          messageType: "info",
                          processId: processId,
                          processName: flpConfigurationRequestDto.ProcessName,
                          tableName: configurationTableMappingDto.TableName,
                          totalRows: resultResponse.TotalRows,
                          flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                          fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                          FileStatusActivityEnum.Skip,
                          FlpActivityLogStatusEnum.BEValidation, null
                       );
            }


            if (configurationTableMappingDto.DropMainTable)
            {
                await AddFileProcessLosStatus(
                      tabName: configurationTableMappingDto.TabName,
                      fileType: fileType,
                      loginId: "",
                      message: $"Dropping Main table in processing:{configurationTableMappingDto.TableName}  for flpConfigurationId {flpConfigurationRequestDto.FlpConfigurationId} tabName {configurationTableMappingDto.TabName}",
                      messageType: "info",
                      processId: processId,
                      processName: flpConfigurationRequestDto.ProcessName,
                      tableName: configurationTableMappingDto.TableName,
                      totalRows: resultResponse.TotalRows,
                      flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                      fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                      FileStatusActivityEnum.Processing,
                      FlpActivityLogStatusEnum.DroppedMainTable, null
                 );

                (bool retMainTable, bool retHistoryTable) = await _fileDatabaseProcessRepo.dropMainTableAndHistoryTable(configurationTableMappingDto.DropMainTable, false, configurationTableMappingDto.TableName, "", connectionString);

                if (retMainTable)
                {
                    await AddFileProcessLosStatus(
                           tabName: configurationTableMappingDto.TabName,
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
                           FlpActivityLogStatusEnum.DroppedMainTable,
                           null
                      );
                }
                else
                {
                    await AddFileProcessLosStatus(
                         tabName: configurationTableMappingDto.TabName,
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
                        FlpActivityLogStatusEnum.DroppedMainTable,
                        null
                    );

                    _logger.LogError($"Multisheet Error:Error in dropping Main table: {configurationTableMappingDto.TableName} for flpConfigurationId {flpConfigurationRequestDto.FlpConfigurationId} tabName {configurationTableMappingDto.TabName}");
                    //return
                    return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
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
                   tabName: configurationTableMappingDto.TabName,
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
                   FlpActivityLogStatusEnum.DroppedMainTable,
                   null
               );
            }

            await AddFileProcessLosStatus(
                     tabName: configurationTableMappingDto.TabName,
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
                     FlpActivityLogStatusEnum.DataInsertedToBronzeTable,
                     null
                 );


            mappingTableSchemaResponse = null;
            if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
            {
                mappingTableSchemaResponse = await _iTableSchemaValidationService.CreateBronzeTableFromBlob(resultResponse.ParquetBlobClient, connectionString, flpConfigurationRequestDto.ProcessName, configurationTableMappingDto.TableName, flpConfigurationRequestDto.FlpConfigurationId, configurationTableMappingDto.TabName, resultResponse);
            }

            if (mappingTableSchemaResponse != null && !mappingTableSchemaResponse.MatchSchema)
            {
                _logger.LogError($"Multisheet Error:Bronze table not created {mappingTableSchemaResponse?.ErrorMessage} for table {configurationTableMappingDto.TableName} flpConfigurationId {flpConfigurationRequestDto.FlpConfigurationId} tabName {configurationTableMappingDto.TabName}");
                await AddFileProcessLosStatus(
                    tabName: configurationTableMappingDto.TabName,
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
                    FlpActivityLogStatusEnum.DataInsertedToBronzeTable,
                     null
                );

                //return
                return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
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
                    //if (mappingTableSchemaResponse != null && mappingTableSchemaResponse.FirstTimeTableCreated)
                    //{
                    //    //(insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataFromParquetSharedLocationAsync(resultResponse.ParquetFilePath,
                    //    //                      connectionString, configurationTableMappingDto.TableName.Trim(), flpConfigurationRequestDto.FlpConfigurationId, slDestinationServerDto, configurationTableMappingDto.mergeData, configurationTableMappingDto.createHistoryTable, configurationTableMappingDto.historyTableName, flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.UploadedFileName);
                    //}
                    //else
                    //{
                    //    (insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataFromParquetSharedLocationAsyncV3(resultResponse.ParquetFilePath,
                    //                          connectionString, configurationTableMappingDto.TableName.Trim(), flpConfigurationRequestDto.FlpConfigurationId, slDestinationServerDto, keyColumnList,
                    //                          configurationTableMappingDto.createHistoryTable, configurationTableMappingDto.historyTableName, flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.UploadedFileName);
                    //}

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
                    //(insertedData, totalInsertedRows) = await _iInsertedDataToBronzeDbRepository.InsertDataFromParquetSharedLocationAsync(resultResponse.ParquetFilePath,
                    //                           connectionString, configurationTableMappingDto.TableName.Trim(), flpConfigurationRequestDto.FlpConfigurationId, slDestinationServerDto, flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.UploadedFileName);
                    //flpConvertToParquetResponseDto.InsertedRows = totalInsertedRows;
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
                  tabName: configurationTableMappingDto.TabName,
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
                  FlpActivityLogStatusEnum.DataInsertedToBronzeTable,
                     null
              );

            }
            else
            {
                _logger.LogError($"Multisheet Error:Inserting data in table for table {configurationTableMappingDto.TableName} flpConfigurationId {flpConfigurationRequestDto.FlpConfigurationId} tabName {configurationTableMappingDto.TabName}");
                await AddFileProcessLosStatus(
                 tabName: configurationTableMappingDto.TabName,
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
                 FlpActivityLogStatusEnum.DataInsertedToBronzeTable,
                     null
               );

                //return

                return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
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
                    tabName = configurationTableMappingDto.TabName,
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
                (bool retMovedFile, string response) = await MoveParquetToArchiveAndDeletedMainFileV2(configurationTableMappingDto.TabName, flpConfigurationRequestDto.DestinationLocationTypeId ?? 0,
                    resultResponse.ParquetFilePath, resultResponse.ParquetBlobClient, flpProcessTempFile.ParquetBlobConnectionString,
                    flpProcessTempFile.BlobContainerName, flpLogHistory, slDestinationServerDto, _iISMBLibraryServices);


                if (retMovedFile)
                {
                    flpConvertToParquetResponseDto.BackUpFileName = response;
                }
                else
                {
                    _logger.LogError($"Multisheet Error:: Parquet file moving into Archived folder for {flpConfigurationRequestDto.FlpConfigurationId}");
                    //return
                    return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { response },
                        Result = flpConvertToParquetResponseDto
                    });

                }

            }
            else
            {
                _logger.LogError($"Multisheet Error:Not moved file in archived location for tableName {configurationTableMappingDto.TableName} flpConfigurationId {flpConfigurationRequestDto.FlpConfigurationId} tabName {configurationTableMappingDto.TabName}");
                await AddFileProcessLosStatus(
                    tabName: configurationTableMappingDto.TabName,
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
                    FlpActivityLogStatusEnum.ParquetFileArchived,
                     null
                );
            }

            //Deleting parquet file path 
            (bool fileDelteed, string parquetFileResponse) = await DeleteParquetFileFromParquetLocation(processId, fileType, resultResponse, configurationTableMappingDto, flpConfigurationRequestDto, slDestinationServerDto);
            if (!fileDelteed)
            {
                return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { parquetFileResponse },
                    Result = flpConvertToParquetResponseDto
                });
            }           

            return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Process Completed" },
                Result = flpConvertToParquetResponseDto
            });


        }

        public async Task<APIResponse<FlpDatabricksProcessResponseDtoV4_1>> ParquetFileProcessToDataLake(long processId, string fileType, string fileLocation, string connectionString, FlpActivityLogStatusEnum currentStatus, FlpProcessTempFileModel flpProcessTempFile, ParquetFileResponseDtoV4_1 resultResponse, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDtoV4_1 flpConfigurationRequestDto, DestinationStorageAccountDtoV4_1 destinationStorageAccountDto, SharedLocationDestinationServerDtoV4_1 slDestinationServerDto, DatabricksStorageAccountDto4_1 databricksStorageAccountDto)
        {
            FlpDatabricksProcessResponseDtoV4_1 flpDatabricksProcessResponseDto = new FlpDatabricksProcessResponseDtoV4_1();
            flpDatabricksProcessResponseDto.TotalRows = resultResponse.TotalRows;
            flpDatabricksProcessResponseDto.InsertedRows = resultResponse.InsertedRows;
            flpDatabricksProcessResponseDto.DuplicateRows = resultResponse.DuplicateRows;
            flpDatabricksProcessResponseDto.BlobName = flpProcessTempFile.Name;
            var validationRuleServiceHelper = new ValidationRuleServiceHelper(_logger);
            string errorMessage = string.Empty;
            await AddFileProcessLosStatus(
              tabName:configurationTableMappingDto.TabName,
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
               FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation,
                     null
             );


            //UI Validation & BE Validation
            //Checked UI Validation
            if (flpConfigurationRequestDto.UIValidation)
            {
                await AddFileProcessLosStatus(
                 tabName: configurationTableMappingDto.TabName,
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
                 FlpActivityLogStatusEnum.UIValidation, null
                );

                //Calling UI Validation API
                string bearerToken = GetBearerToken();
                var response1 = await validationRuleServiceHelper.ValidateExcelRules(bearerToken, true, flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.UploadedFileId,
                                      flpProcessTempFile.Name, resultResponse.ParquetFilePath, destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey,
                                      destinationStorageAccountDto.StorageContainerName, configurationTableMappingDto.TabName);
                if (response1?.ResultStatus.Code == APIResultStatus.Completed.Code)
                {
                    //Completed status
                    var result = response1?.Result;
                    var responseMessage = result?.ResponseMessage;
                    //Checked failed rows
                    if (result?.FailedRows > 0)
                    {
                        await AddFileProcessLosStatus(
                            tabName: configurationTableMappingDto.TabName,
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
                            FlpActivityLogStatusEnum.UIValidation, null
                        );

                        _logger.LogError($"Error: Unable to complete UI Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId},{responseMessage}");
                        
                        //return
                        return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDtoV4_1>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { $"Error: Unable to complete BE Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}" },
                            Result = flpDatabricksProcessResponseDto
                        });
                    }
                    else
                    {
                        await AddFileProcessLosStatus(
                            tabName: configurationTableMappingDto.TabName,
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
                            FlpActivityLogStatusEnum.UIValidation, null
                       );

                       
                    }
                }
                else
                {
                    await AddFileProcessLosStatus(
                      tabName: configurationTableMappingDto.TabName,
                      fileType: fileType,
                      loginId: "",
                      message: $"Error: UI Validation process Failed. {response1?.ResponseMessage.FirstOrDefault()}",
                      messageType: "info",
                      processId: processId,
                      processName: flpConfigurationRequestDto.ProcessName,
                      tableName: configurationTableMappingDto.TableName,
                      totalRows: resultResponse.TotalRows,
                      flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                      fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                      FileStatusActivityEnum.Error,
                      FlpActivityLogStatusEnum.UIValidation, null
                     );

                    _logger.LogError($"Error: Unable to complete UI Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}");
                    //return                    
                    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDtoV4_1>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { $"Error: Unable to complete BE Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}" },
                        Result = flpDatabricksProcessResponseDto
                    });
                }

            }
            else
            {
                await AddFileProcessLosStatus(
                    tabName: configurationTableMappingDto.TabName,
                    fileType: fileType,
                    loginId: "",
                    message: $"Success: UI Validation processing stage skipped",
                    messageType: "info",
                    processId: processId,
                    processName: flpConfigurationRequestDto.ProcessName,
                    tableName: configurationTableMappingDto.TableName,
                    totalRows: resultResponse.TotalRows,
                    flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                    fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                    FileStatusActivityEnum.Skip,
                    FlpActivityLogStatusEnum.UIValidation, null
                   );
            }

            if (flpConfigurationRequestDto.BEValidation)
            {
                await AddFileProcessLosStatus(
                           tabName: configurationTableMappingDto.TabName,
                           fileType: fileType,
                           loginId: "",
                           message: $"Success: BE Validation processing starting",
                           messageType: "info",
                           processId: processId,
                           processName: flpConfigurationRequestDto.ProcessName,
                           tableName: configurationTableMappingDto.TableName,
                           totalRows: resultResponse.TotalRows,
                           flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                           fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                           FileStatusActivityEnum.Processing,
                           FlpActivityLogStatusEnum.BEValidation, null
                        );
                string bearerToken = GetBearerToken();
                var res2 = await validationRuleServiceHelper.ValidateExcelRules(bearerToken, false, flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.UploadedFileId,
                                       flpProcessTempFile.Name, resultResponse.ParquetFilePath, destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey, destinationStorageAccountDto.StorageContainerName, configurationTableMappingDto.TabName);
                if (res2.ResultStatus.Code == APIResultStatus.Completed.Code)
                {
                    //Completed status
                    var result = res2.Result;
                    var responseMessage = result.ResponseMessage;
                    //Checked failed rows
                    if (result.FailedRows > 0)
                    {
                        await AddFileProcessLosStatus(
                            tabName: configurationTableMappingDto.TabName,
                            fileType: fileType,
                            loginId: "",
                            message: $"Error: BE Validation Failed for this file. Reason- {responseMessage} ",
                            messageType: "error",
                            processId: processId,
                            processName: flpConfigurationRequestDto.ProcessName,
                            tableName: configurationTableMappingDto.TableName,
                            totalRows: resultResponse.TotalRows,
                            flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                            fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                            FileStatusActivityEnum.Error,
                            FlpActivityLogStatusEnum.BEValidation, null
                        );

                        _logger.LogError($"Error: Unable to complete UI Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId},{responseMessage}");
                        
                        //return
                        return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDtoV4_1>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { $"Error: Unable to complete BE Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}" },
                            Result = flpDatabricksProcessResponseDto
                        });
                    }
                    else
                    {
                        await AddFileProcessLosStatus(
                            tabName: configurationTableMappingDto.TabName,
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
                            FlpActivityLogStatusEnum.BEValidation, null
                         );
                      


                    }
                }
                else
                {
                    await AddFileProcessLosStatus(
                      tabName: configurationTableMappingDto.TabName,
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
                      FlpActivityLogStatusEnum.BEValidation, null
                     );

                    _logger.LogError($"Error: Unable to complete BE Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}");
                    //return
                    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDtoV4_1>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { $"Error: Unable to complete BE Validation  for {flpConfigurationRequestDto.FlpConfigurationId}, {flpConfigurationRequestDto.UploadedFileId}" },
                        Result = flpDatabricksProcessResponseDto
                    });
                }
            }
            else
            {
                await AddFileProcessLosStatus(
                          tabName: configurationTableMappingDto.TabName,
                          fileType: fileType,
                          loginId: "",
                          message: $"Success: BE Validation processing stage skipped",
                          messageType: "info",
                          processId: processId,
                          processName: flpConfigurationRequestDto.ProcessName,
                          tableName: configurationTableMappingDto.TableName,
                          totalRows: resultResponse.TotalRows,
                          flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                          fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                          FileStatusActivityEnum.Skip,
                          FlpActivityLogStatusEnum.BEValidation, null
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
           

            (bool retMovedFile2, string response2) = await MoveParquetFileToDeltaLakeStorageAccount(configurationTableMappingDto.TabName,flpConfigurationRequestDto.DetalakeStorageAccountPath,
                resultResponse.ParquetFilePath, resultResponse.ParquetBlobClient, flpProcessTempFile.ParquetBlobConnectionString, flpLogHistory2,
                _iISMBLibraryServices, databricksStorageAccountDto);
            if (retMovedFile2)
            {
                flpDatabricksProcessResponseDto.DataLakeStorageAccountPath = response2;
            }
            else
            {
                errorMessage = $"Error: Parquet file moving into Location for {flpConfigurationRequestDto.FlpConfigurationId}";
                _logger.LogError(errorMessage);
                //return
                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDtoV4_1>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = flpDatabricksProcessResponseDto
                });

            }

            //do_not_acrhive false
            if (!configurationTableMappingDto.DoNotArchiveFile)
            {
                //currentStatus = FlpActivityLogStatusEnum.ParquetFileArchived;
                //Moving folder into archive
                var flpLogHistory = new FileProcessLogHistoryDto()
                {
                    tabName = configurationTableMappingDto.TabName,
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
                (bool retMovedFile, string response1) = await MoveParquetToArchiveAndDeletedMainFileV2(configurationTableMappingDto.TabName, flpConfigurationRequestDto.DestinationLocationTypeId ?? 0,
                    resultResponse.ParquetFilePath, resultResponse.ParquetBlobClient, flpProcessTempFile.ParquetBlobConnectionString,
                    flpProcessTempFile.BlobContainerName, flpLogHistory, slDestinationServerDto, _iISMBLibraryServices);


                if (retMovedFile)
                {
                    flpDatabricksProcessResponseDto.BackUpFileName = response1;
                }
                else
                {
                    errorMessage = $"Multisheet Error:: Parquet file moving into Archived folder for {flpConfigurationRequestDto.FlpConfigurationId}";
                    _logger.LogError(errorMessage);
                    //return
                    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDtoV4_1>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { errorMessage },
                        Result = null
                    });

                }

            }
            else
            {
                errorMessage = $"Multisheet Error:Not moved file in archived location for tableName {configurationTableMappingDto.TableName} flpConfigurationId {flpConfigurationRequestDto.FlpConfigurationId} tabName {configurationTableMappingDto.TabName}";
                _logger.LogError(errorMessage);
                await AddFileProcessLosStatus(
                    tabName: configurationTableMappingDto.TabName,
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
                    FlpActivityLogStatusEnum.ParquetFileArchived,
                     null
                );
            }


            //Deleting parquet file path 
            (bool fileDelteed, string parquetFileResponse) = await DeleteParquetFileFromParquetLocation(processId, fileType, resultResponse, configurationTableMappingDto, flpConfigurationRequestDto, slDestinationServerDto);
            if (!fileDelteed)
            {
                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDtoV4_1>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { parquetFileResponse },
                    Result = null
                });
            }
            
            //DataBricks Job process started
            //Databricks configuration
            await AddFileProcessLosStatus(
                        tabName:configurationTableMappingDto.TabName,
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
                        FlpActivityLogStatusEnum.DatabricksJobProcess,
                     null
            );

            var configurationFileColumns = await _iTableSchemaValidationService.GetFileColumnListV2ByTabName(flpConfigurationRequestDto.FlpConfigurationId, configurationTableMappingDto.TabName);
            var columnWithDataType = string.Join(",", configurationFileColumns.Select(column => $"{column.DbColumn}={column.dataType}"));
            // Initialize the DatabricksJobRunner
            var jobRunner = new DatabricksJobRunner(flpConfigurationRequestDto.DatabricksInstance, flpConfigurationRequestDto.DataBricksAPIVersion, flpConfigurationRequestDto.DataBricksAPIToken);
            flpDatabricksProcessResponseDto.DataLakeStorageAccountPath = Uri.UnescapeDataString(flpDatabricksProcessResponseDto.DataLakeStorageAccountPath).Replace("\\", "/") ?? "";
            //Uploaded json file path into blob storage
            string jsonFilePath = FlpConfigurationHelperV4_1.CreatedJsonFilePath(response2, flpConfigurationRequestDto.ProcessName);
            List<string> splitArray = FlpConfigurationHelper.SplitColumnsWithoutCleanColumn(databricksStorageAccountDto.SasKey, "?");
            string sasKey = splitArray.Count > 1 ? splitArray[1] : splitArray[0];
            string jsonFileFullPath = $"{jsonFilePath}?{sasKey}";
            bool ret  = await _iBlobStorageService.UploadColumnJsonAsync(configurationFileColumns, $"{jsonFileFullPath}" );
            if (!ret)
            {
                await AddFileProcessLosStatus(
                                       tabName: configurationTableMappingDto.TabName,
                                       fileType: fileType,
                                       loginId: "",
                                       message: $"Error:Not uploaded column json file in blob.",
                                       messageType: "error",
                                       processId: processId,
                                       processName: flpConfigurationRequestDto.ProcessName,
                                       tableName: configurationTableMappingDto.TableName,
                                       totalRows: resultResponse.TotalRows,
                                       flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                       fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                                       FileStatusActivityEnum.Error,
                                       FlpActivityLogStatusEnum.DatabricksJobProcess,
                                    null
                           );
                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDtoV4_1>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { parquetFileResponse },
                    Result = null
                });
            }
            //Adding json file mapping to database
            bool isJsonMappingAddedInDb = await databricksJsonFileColumn(jsonFilePath,columnWithDataType, flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.UploadedFileId, configurationTableMappingDto.TabName);
            if (!isJsonMappingAddedInDb)
            {
                await AddFileProcessLosStatus(
                                      tabName: configurationTableMappingDto.TabName,
                                      fileType: fileType,
                                      loginId: "",
                                      message: $"Error:Not uploaded column json file in database. {jsonFilePath}",
                                      messageType: "error",
                                      processId: processId,
                                      processName: flpConfigurationRequestDto.ProcessName,
                                      tableName: configurationTableMappingDto.TableName,
                                      totalRows: resultResponse.TotalRows,
                                      flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                      fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                                      FileStatusActivityEnum.Error,
                                      FlpActivityLogStatusEnum.DatabricksJobProcess,
                                   null
                          );
                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDtoV4_1>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { parquetFileResponse },
                    Result = null
                });
            }
           // string columnWithDataTypeJsonFilePath = jsonFilePath;

            var jobParameters = new
            {
                deltaTableName = configurationTableMappingDto.TableName ?? "",
                historyTableName = configurationTableMappingDto.historyTableName ?? "",
                dropMainTable = configurationTableMappingDto.DropMainTable,
                AllowOverwrite = configurationTableMappingDto.mergeData,
                SaveOverwriteLog = configurationTableMappingDto.createHistoryTable,
                databricksCatalogue = configurationTableMappingDto.UnityCatalog,
                dataLakeStoragePath = flpDatabricksProcessResponseDto.DataLakeStorageAccountPath ?? "",
                dataLakeStorageAccountName = databricksStorageAccountDto.FlpStorageAccount ?? "",
                dataLakeStorageContainerName = databricksStorageAccountDto.StorageContainerName ?? "",
                processModified = flpConfigurationRequestDto.ProcessModified,
                keyColumnList = configurationTableMappingDto.KeyColumnList ?? "",
                columnWithDataType = jsonFilePath ?? "",// columnWithDataType ?? "",
                fileUploadId = flpConfigurationRequestDto.UploadedFileId,
                fileName = flpConfigurationRequestDto.UploadedFileName,
                validateSchema = configurationTableMappingDto.ValidateFileSchema,
                campaignId = configurationTableMappingDto.campaignId??""
            };

            // Run the job
            if (string.IsNullOrWhiteSpace(flpConfigurationRequestDto.JobId))
            {
                await AddFileProcessLosStatus(
                   tabName:configurationTableMappingDto.TabName,
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
                   databricksAPIResponse: null
               );
            }


            //  var dataBricksStages = await _cache.GetDataBricksStagesAsync();
            //  var databricksTerminationDetails = await _cache.GetDatabricksTerminationDetailsAsync();
            var response = await jobRunner.RunJobAsync(Convert.ToInt64(flpConfigurationRequestDto.JobId), jobParameters);
            var updatingJsonContent = Convert.ToBoolean(KeyVault.GetKeyVaultValue("TPDataIngestionAddingJobRequestBody").Result);
            if (updatingJsonContent)
            {
                string convertedParamInJson = JsonConvert.SerializeObject(jobParameters, Formatting.Indented);
                _logger.LogError("Error: Job Runner Request body for: " + convertedParamInJson);
            }
           
            if (response != null && response.JobRunSuccess)
            {
                flpDatabricksProcessResponseDto.RunId = response.RunId;


                await AddFileProcessLosStatus(
                    tabName: configurationTableMappingDto.TabName,
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
                    databricksAPIResponse: response.ResponseContent
                );
                //Update RunId in db
                //Will start the writing catalog procss - run the job tracking status
                await AddFileProcessLosStatus(
                    tabName: configurationTableMappingDto.TabName,
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
                    FlpActivityLogStatusEnum.WritingDataBricksCatalog,
                    databricksAPIResponse: response?.ResponseContent
                );
                _logger.LogInformation($"Info:Writing Catalog- JobId: {flpConfigurationRequestDto.JobId}, RunId: {response.RunId}, calling API");
                var jobRunStatusAPIResponse = await jobRunner.GetJobRunStatusAsync(flpDatabricksProcessResponseDto.RunId ?? 0);
                if (jobRunStatusAPIResponse != null && (int)jobRunStatusAPIResponse.StatusCode == 200)
                {
                    if (jobRunStatusAPIResponse.JobSatus != null)
                    {

                        // var jobStatusState = jobRunStatusAPIResponse.JobSatus.Status.State;
                        var jobStatusState = jobRunStatusAPIResponse.JobSatus.State;
                        _logger.LogInformation($"Info:Writing Catalog- call status {jobStatusState} for tab {configurationTableMappingDto.TabName}, calling API");
                        // var stageDetails = dataBricksStages?.FirstOrDefault(x => x.Stage == jobStatusState);
                        if (jobStatusState != null)
                        {
                            var lifeCycleStateId = EnumHelper.GetLifeCycleStateEnumValueFromDescription(jobStatusState.LifeCycleState);
                            var resultStateId = EnumHelper.GetResultStateEnumEnumValueFromDescription(jobStatusState.ResultState);
                            flpDatabricksProcessResponseDto.LifeCycleStateId = lifeCycleStateId;
                            flpDatabricksProcessResponseDto.ResultStateId = resultStateId;
                            var jobStatusMessage = $"life_cycle_state {jobStatusState.LifeCycleState} and result_state {jobStatusState.ResultState}";
                            if (lifeCycleStateId == (int)LifeCycleStateEnum.TERMINATED && resultStateId == (int)ResultStateEnum.SUCCESS)
                            {
                                //Completed
                                await AddFileProcessLosStatus(
                                        tabName: configurationTableMappingDto.TabName,
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
                                               tabName: configurationTableMappingDto.TabName,
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
                                    tabName: configurationTableMappingDto.TabName,
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

                            return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDtoV4_1>
                            {
                                ResultStatus = APIResultStatus.Error,
                                ResponseMessage = new List<string> { $"Error: not found jobStatusState details" },
                                Result = flpDatabricksProcessResponseDto
                            });
                        }


                    }
                    else
                    {
                        //Error Not fetched job status in history table: update only error status
                        _logger.LogInformation($"Error:Writing Catalog- JobId: {flpConfigurationRequestDto.JobId},RunId: {response.RunId},Not found JobStatus calling API");
                        await AddFileProcessLosStatus(tabName: configurationTableMappingDto.TabName,
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

                        return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDtoV4_1>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { $"Error: Not found job status from Job/getrun API" },
                            Result = flpDatabricksProcessResponseDto
                        });
                    }
                }
                else
                {
                    //Skip this condition - will check by function app next time
                    //Error Not fetched job status in history table: update only error status
                    _logger.LogInformation($"Error:Writing Catalog- JobId: {flpConfigurationRequestDto.JobId}, RunId: {response.RunId}, calling API");
                    await AddFileProcessLosStatus(
                              tabName: configurationTableMappingDto.TabName,
                               fileType: fileType,
                              loginId: "",
                               message: $"Error: {jobRunStatusAPIResponse?.Message ?? "Internal server error in JobRunStatusAPI"}",
                               messageType: "error",
                               processId: processId,
                               processName: flpConfigurationRequestDto.ProcessName,
                               tableName: configurationTableMappingDto.TableName,
                               totalRows: resultResponse.TotalRows,
                               flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                               fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                               FileStatusActivityEnum.Error,
                               FlpActivityLogStatusEnum.WritingDataBricksCatalog,
                               databricksAPIResponse: jobRunStatusAPIResponse.ResponseContent ?? ""
                           );

                    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDtoV4_1>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { $"Error: Not found details from Job/getrun API  for this {flpDatabricksProcessResponseDto.RunId}" },
                        Result = flpDatabricksProcessResponseDto
                    });
                }
            }
            else
            {

                await AddFileProcessLosStatus(
                    tabName: configurationTableMappingDto.TabName,
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

                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDtoV4_1>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { $"Error: Error: {response.StatusCode}, Details: {response.Message}" },
                    Result = flpDatabricksProcessResponseDto
                });
            }

            return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDtoV4_1>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { errorMessage },
                Result = flpDatabricksProcessResponseDto
            });

        }
        public async Task<(bool, string)> MoveParquetToArchiveAndDeletedMainFileV2(string tabName,int destinationLocationTypeId, string parquetFilePath, BlobClient bcParquetSourceLocation, string blobConnectionString, string destinationContainerName, FileProcessLogHistoryDto fileLogHistory, SharedLocationDestinationServerDtoV4_1 slDestinationServerDto, ISMBLibraryServices _ismbLibraryServices)
        {
            //string currentTimeString = $"{DateTime.Now:yyyy}/{DateTime.Now.ToString("yyyy/MMM/dd", new CultureInfo("en-US"))}/{DateTime.Now.Date}";
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
                         tabName:tabName,
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
                         FlpActivityLogStatusEnum.ParquetFileArchived,null
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
                                tabName:tabName,
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
                                FlpActivityLogStatusEnum.ParquetFileArchived,null);
                            //Deleting source file
                            string retArchivedFilePath = Path.Combine(parquetFileModel.serverIP, parquetFileModel.sharedFolderName, archivedFilePath);
                            return (true, retArchivedFilePath);
                        }
                        else
                        {
                            await AddFileProcessLosStatus(
                                tabName: tabName,
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
                                FlpActivityLogStatusEnum.ParquetFileArchived,null);
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
                             tabName: tabName,
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
                             FlpActivityLogStatusEnum.ParquetFileArchived,null);
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
                                tabName: tabName,
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
                                FlpActivityLogStatusEnum.ParquetFileArchived,null);
                            //Deleting source file
                            return (true, flpProcessTempFile.Uri);
                        }
                        else
                        {
                            await AddFileProcessLosStatus(
                                tabName: tabName,
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
                                FlpActivityLogStatusEnum.ParquetFileArchived,null);
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
                       tabName: tabName,
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
                       FlpActivityLogStatusEnum.ParquetFileArchived,null);
                return (false, ex.Message.ToString());
            }

        }

        public async Task<(bool, string)> MoveParquetFileToDeltaLakeStorageAccount(string tabName,string detalakeStorageAccountPath, string parquetFilePath, BlobClient bcParquetSourceLocation,
         string blobConnectionString, FileProcessLogHistoryDto fileLogHistory, ISMBLibraryServices _ismbLibraryServices, DatabricksStorageAccountDto4_1 databricksStorageAccountDto)
        {
           // string currentTimeString = DateTime.Now.ToString("yyyyMMdd");
            string currentTimeString = DateTime.Now.ToString("yyyy/MMMM/dd");
            string directoryPath = ""; // Gets the directory part of the path
            string destinationFolderPath = "";

            destinationFolderPath = Path.GetDirectoryName(bcParquetSourceLocation.Name).Replace("\\", "/");
            // destinationFolderPath = $"{directoryPath}/{currentTimeString}";
            try
            {
                //detalakeStorageAccountPath = "parquetfile/source/";

                await AddFileProcessLosStatus(
                         tabName: tabName,
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
                         FlpActivityLogStatusEnum.PlacedParquetFile,
                     null
                     );
                string filePath = "";
                BlobClient destinationBlob = null;
                // string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(databricksStorageAccountDto.FlpStorageAccount, databricksStorageAccountDto.StorageAccountKey);
                string destinationBlobConnectionString = "";// FlpConfigurationHelper.GetBlobConnectionString(databricksStorageAccountDto.FlpStorageAccount, databricksStorageAccountDto.StorageAccountKey);
                string destinationBlobName = $"{detalakeStorageAccountPath}{bcParquetSourceLocation.Name.Split('/').Last()}";
                // Parse the source blob URL
                // Initialize the BlobServiceClient for the destination

                //BlobServiceClient destinationBlobServiceClient = new BlobServiceClient(destinationBlobConnectionString);             
                //// Get the destination container client
                //// BlobContainerClient destinationContainerClient = destinationBlobServiceClient.GetBlobContainerClient(destinationContainerName);
                //BlobContainerClient destinationContainerClient = destinationBlobServiceClient.GetBlobContainerClient(databricksStorageAccountDto.StorageContainerName);
                //// Get a reference to the destination blob
                //BlobClient destinationBlob = destinationContainerClient.GetBlobClient(destinationBlobName);


                if (databricksStorageAccountDto.SasKeyToken)
                {
                    //string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(databricksStorageAccountDto.FlpStorageAccount, databricksStorageAccountDto.StorageAccountKey);
                    destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionStringBySasKey(databricksStorageAccountDto.FlpStorageAccount, databricksStorageAccountDto.SasKey);
                    destinationBlob = _iBlobStorageService.GetBlobClientDetailsBySasToken(destinationBlobName, destinationBlobConnectionString);
                }
                else
                {
                    // string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(databricksStorageAccountDto.FlpStorageAccount, databricksStorageAccountDto.StorageAccountKey);
                     destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(databricksStorageAccountDto.FlpStorageAccount, databricksStorageAccountDto.StorageAccountKey);                    
                    destinationBlob = _iBlobStorageService.GetBlobClientDetails(destinationBlobName, destinationBlobConnectionString, databricksStorageAccountDto.StorageContainerName);
                }


                if (bcParquetSourceLocation != null)
                {

                    (bool ret, var flpProcessTempFile) = await _iBlobStorageService.CopyBlobUsingStreamAsync(bcParquetSourceLocation, destinationBlob);

                    if (ret)
                    {
                        if (ret && databricksStorageAccountDto.SasKeyToken)
                        {
                            var cleanUri = new Uri(flpProcessTempFile.Uri);
                            flpProcessTempFile.Uri = cleanUri.GetLeftPart(UriPartial.Path);
                        }
                        await AddFileProcessLosStatus(
                            tabName: tabName,
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
                            FlpActivityLogStatusEnum.PlacedParquetFile,
                     null);
                        //Deleting source file
                        return (true, flpProcessTempFile.Uri);
                    }
                    else
                    {
                        await AddFileProcessLosStatus(
                             tabName: tabName,
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
                            FileStatusActivityEnum.Error,
                            FlpActivityLogStatusEnum.PlacedParquetFile,
                     null);
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
                        tabName: tabName,
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
                       FlpActivityLogStatusEnum.PlacedParquetFile,
                     null);
                return (false, ex.Message.ToString());
            }

        }

        public async Task<(FlpProcessTempFileModel?, bool)> MoveSourceExcelFileToTemporaryDestinationAndDelete( long processId, string fileType, string fileLocation, string fileUploadedId, string backUpFileName, FlpConfigurationResponseDtoV4_1 flpConfigurationRequestDto, DestinationStorageAccountDtoV4_1 destinationStorageAccountDto, SharedLocationDestinationServerDtoV4_1 slDestinationServerDto, List<FileSettings> fileSettings)
        {
            FlpProcessTempFileModel flpProcessTempFile = new FlpProcessTempFileModel();
            FlpProcessTempFileModel flpCsvProcessTempFile = new FlpProcessTempFileModel();
            bool isCopiedFile = false;
            bool isCsvFileCopiedFile = false;
            BlobClient sourceBlobClient = null;
            string sourceUrl = "";
            string relativePath = "";
            //source
            //Adding Log
            FlpActivityLogStatusEnum currentStatus = FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation;
            CheckConnectivitySMBLibraryModel sourceServer = null;

            try
            {
                currentStatus = FlpActivityLogStatusEnum.FileMovedToTempStorage;

                foreach (var fileSetting in fileSettings)
                {
                    string tabName = fileSetting.tabName;
                    string tableName = fileSetting.databaseSettings?.table_name ?? "";

                    await AddFileProcessLosStatus(
                            tabName: tabName,
                            fileType: fileType,
                            loginId: "",
                            message: $"Moving File into temp folder from location: {fileLocation}",
                            messageType: "info",
                            processId: processId,
                            processName: flpConfigurationRequestDto.ProcessName,
                            tableName: tableName,
                            totalRows: 0,
                            flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                            fileUploadedId: fileUploadedId,
                            FileStatusActivityEnum.Processing,
                            FlpActivityLogStatusEnum.FileMovedToTempStorage,
                     null
                        );

                }                
                if (flpConfigurationRequestDto.BlobClients != null && !string.IsNullOrWhiteSpace(flpConfigurationRequestDto.BlobClients.Uri) && !string.IsNullOrWhiteSpace(flpConfigurationRequestDto.BlobClients.Name))
                {
                    flpConfigurationRequestDto.BlobClients.Uri = Uri.UnescapeDataString(flpConfigurationRequestDto.BlobClients.Uri);
                    flpConfigurationRequestDto.BlobClients.Name = Uri.UnescapeDataString(flpConfigurationRequestDto.BlobClients.Name);
                }
                if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure &&
                    flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                {
                    //string sourceBlobUrl = flpConfigurationRequestDto.BlobClients.Uri;
                    //string sourceBlobName = flpConfigurationRequestDto.BlobClients.Name;
                    //string sourceBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(flpConfigurationRequestDto.SourceStorageAccount, flpConfigurationRequestDto.SourceStorageAccountKey);
                    //string sourceBlobContainer = flpConfigurationRequestDto.SourceContainerName;

                    ////sourceBlobClient = _iBlobStorageService.GetBlobClientDetails(flpConfigurationRequestDto.BlobClients.Name, sourceBlobConnectionString, sourceBlobContainer);
                    //sourceBlobClient = _iBlobStorageService.GetBlobClientDetails(sourceBlobName, sourceBlobConnectionString, sourceBlobContainer);


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

                    (isCopiedFile, flpProcessTempFile) = await _iBlobStorageService.CopyBlobUsingStreamAsync(sourceBlobClient, destinationBlobClient);


                    //if (fileSizeInMB > 50)
                    //{
                    //    _logger.LogInformation($"File size is greater than 50 MB, creating CSV file for {flpConfigurationRequestDto.FlpConfigurationId}");
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
                if (isCopiedFile && flpProcessTempFile != null)
                {

                   
                    foreach (var fileSetting in fileSettings)
                    {
                        string tabName = fileSetting.tabName;
                        string tableName = fileSetting.databaseSettings?.table_name ?? "";

                        await AddFileProcessLosStatus(
                                tabName: tabName,
                                fileType: fileType,
                                loginId: "",
                                message: $"File Moved successfully into destination temp folder from location {fileLocation}",
                                messageType: "info",
                                processId: processId,
                                processName: flpConfigurationRequestDto.ProcessName,
                                tableName: tableName,
                                totalRows: 0,
                                flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                fileUploadedId: fileUploadedId,
                                FileStatusActivityEnum.ProcessCompleted,
                                FlpActivityLogStatusEnum.FileMovedToTempStorage,
                     null
                            );

                    }
                    if (flpProcessTempFile != null)
                    {
                        currentStatus = FlpActivityLogStatusEnum.DeletedFileFromMainLocation;

                       
                        foreach (var fileSetting in fileSettings)
                        {
                            string tabName = fileSetting.tabName;
                            string tableName = fileSetting.databaseSettings?.table_name ?? "";

                            await AddFileProcessLosStatus(
                                    tabName: tabName,
                                    fileType: fileType,
                                    loginId: "",
                                    message: $"started deleting from location:{fileLocation}",
                                    messageType: "info",
                                    processId: processId,
                                    processName: flpConfigurationRequestDto.ProcessName,
                                    tableName: tableName,
                                    totalRows: 0,
                                    flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                    fileUploadedId: fileUploadedId,
                                    FileStatusActivityEnum.Processing,
                                    FlpActivityLogStatusEnum.DeletedFileFromMainLocation,
                     null
                                );

                        }                    
                        var isDeleted = false;
                        string responseMsg = "";

                        if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure)
                        {
                            isDeleted = true;// await _iBlobStorageService.DeleteBlobAsync(sourceBlobClient);
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

                            foreach (var fileSetting in fileSettings)
                            {
                                string tabName = fileSetting.tabName;
                                string tableName = fileSetting.databaseSettings?.table_name ?? "";

                                await AddFileProcessLosStatus(
                                        tabName: tabName,
                                        fileType: fileType,
                                        loginId: "",
                                        message: $"deleted from location:{fileLocation}",
                                        messageType: "info",
                                        processId: processId,
                                        processName: flpConfigurationRequestDto.ProcessName,
                                        tableName: tableName,
                                        totalRows: 0,
                                        flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                        fileUploadedId: fileUploadedId,
                                        FileStatusActivityEnum.ProcessCompleted,
                                        FlpActivityLogStatusEnum.DeletedFileFromMainLocation,
                     null
                                    );

                            }

                        }
                        else
                        {

                            foreach (var fileSetting in fileSettings)
                            {
                                string tabName = fileSetting.tabName;
                                string tableName = fileSetting.databaseSettings?.table_name ?? "";

                                await AddFileProcessLosStatus(
                                        tabName: tabName,
                                        fileType: fileType,
                                        loginId: "",
                                        message: $"Error: Not deleted from location:{fileLocation}",
                                        messageType: "error",
                                        processId: processId,
                                        processName: flpConfigurationRequestDto.ProcessName,
                                        tableName: tableName,
                                        totalRows: 0,
                                        flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                        fileUploadedId: fileUploadedId,
                                        FileStatusActivityEnum.Error,
                                        FlpActivityLogStatusEnum.DeletedFileFromMainLocation,
                     null
                                    );

                            }
                         
                            _logger.LogError($"Multisheet Error: Not deleted from location:{fileLocation} for FlpConfigurationId {flpConfigurationRequestDto.FlpConfigurationId}", $"{responseMsg}");
                            return (flpProcessTempFile, false);

                        }


                    }
                }
                else
                {

                    foreach (var fileSetting in fileSettings)
                    {
                        string tabName = fileSetting.tabName;
                        string tableName = fileSetting.databaseSettings?.table_name ?? "";

                        await AddFileProcessLosStatus(
                                tabName: tabName,
                                fileType: fileType,
                                loginId: "",
                                message: $"Error: File moving  failed to templorary storage",
                                messageType: "error",
                                processId: processId,
                                processName: flpConfigurationRequestDto.ProcessName,
                                tableName: tableName,
                                totalRows: 0,
                                flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                                fileUploadedId: fileUploadedId,
                                FileStatusActivityEnum.Error,
                                FlpActivityLogStatusEnum.FileMovedToTempStorage,
                     null
                            );

                    }
                  
                    _logger.LogError($"Multisheet Error: File moving  failed to templorary storage for {flpConfigurationRequestDto.FlpConfigurationId}", "flpProcessTempFile is null");
                    return (null, false);
                }
                return (flpProcessTempFile, true);
            }
            catch (Exception ex)
            {
                

                _logger.LogError($"Multisheet Error: File moving  failed to templorary storage for {flpConfigurationRequestDto.FlpConfigurationId}", ex.Message.ToString());
                return (null, false);
            }
        }


     
        public async Task AddFileProcessLosStatus(string tabName,string fileType, string loginId, string message, string messageType,
      long processId, string processName, string tableName, int totalRows, string flpConfigurationId, string fileUploadedId,
      FileStatusActivityEnum fileStatusActivityEnum, FlpActivityLogStatusEnum flpActivityLogStatusEnum, string databricksAPIResponse)
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
                databricksAPIResponse = databricksAPIResponse,
                tabName = tabName

            };
            var ret = await _ifileLoadingProcessRepository.InsertFileProcessStatus(fileProcessLogHistoryDto);
            if (!ret)
            {
                _logger.LogError($"Error: Not inserted records for {flpConfigurationId}");
            }
        }

        private async Task<bool> databricksJsonFileColumn(string fileURL, string columns,string flpConfigurationId, string uploadFileId,string tabName)
        {

            var dbResult = await _ifileLoadingProcessRepository.AddDatabricksJsonFileColumn(fileURL,columns,flpConfigurationId,uploadFileId,tabName);
            if (string.Compare(dbResult.Result, "Success", true) == 0)
            {
                return true;
            }           
            return false;
        }


        public async Task<(bool, string)> DeleteTempFileFromTempLocation(int destinationLocationTypeId, string fileType,string processName, string flpConfigurationId,string uploadedFileId,
            FlpProcessTempFileModel flpProcessTempFile,
            List<FlpProcessTabResponseDto> fileSettings)
        {
            bool deleted = false;
            string messsage = "";

            //Deleting temp folder 

            //foreach (var fileSetting in fileSettings)
            //{
               
            //    await AddFileProcessLosStatus(
            //            tabName: fileSetting.TabName,
            //            fileType: fileType,
            //            loginId: "",
            //            message: $"file is deleting is starting from temp folder{flpProcessTempFile.sourceTempFilePath}",
            //            messageType: "info",
            //            processId: fileSetting.ProcessId,
            //            processName: processName,
            //            tableName: fileSetting.TableName,
            //            totalRows: fileSetting.TotalRows,
            //            flpConfigurationId: flpConfigurationId,
            //            fileUploadedId: uploadedFileId,
            //            FileStatusActivityEnum.Processing,
            //            FlpActivityLogStatusEnum.FileDeletedFromTemp,
            //             null
            //        );

            //}          
            //Call deleting function
            if (destinationLocationTypeId  == (int)DestinationLocationTypeEnum.Azure)
            {
                deleted = await _iBlobStorageService.DeleteTempFileBlobAsync(flpProcessTempFile.Name, flpProcessTempFile.ParquetBlobConnectionString, flpProcessTempFile.BlobContainerName);               
                if (!deleted)
                {
                    messsage = "something went wrong.";
                }
            }

            if (!deleted)
            {
                _logger.LogError($"Multisheet Delete Temp File Error: unable to delete file from location for flpConfiguration:{flpConfigurationId} & uploadedFileId {uploadedFileId}");
            }
            else
            {
                _logger.LogInformation($"Multisheet Delete Temp File: Deleted file from location for flpConfiguration:{flpConfigurationId} & uploadedFileId {uploadedFileId}");
            }


                //if (deleted)
                //{
                //    foreach (var fileSetting in fileSettings)
                //    {

                //        await AddFileProcessLosStatus(
                //                    tabName: fileSetting.TabName,
                //                    fileType: fileType,
                //                    loginId: "",
                //                    message: $"deleted file from temp location:{flpProcessTempFile.sourceTempFilePath}",
                //                    messageType: "info",
                //                    processId: fileSetting.ProcessId,
                //                    processName: processName,
                //                    tableName: fileSetting.TableName,
                //                    totalRows: fileSetting.TotalRows,
                //                    flpConfigurationId: flpConfigurationId,
                //                    fileUploadedId: uploadedFileId,
                //                    FileStatusActivityEnum.ProcessCompleted,
                //                    FlpActivityLogStatusEnum.FileDeletedFromTemp,
                //                        null
                //         );


                //    }




                //}
                //else
                //{

                //    foreach (var fileSetting in fileSettings)
                //    {


                //        await AddFileProcessLosStatus(
                //                    tabName: fileSetting.TabName,
                //                    fileType: fileType,
                //                    loginId: "",
                //                    message: $"deleted file from location:{flpProcessTempFile.sourceTempFilePath}",
                //                    messageType: "info",
                //                    processId: fileSetting.ProcessId,
                //                    processName: processName,
                //                    tableName: fileSetting.TableName,
                //                    totalRows: fileSetting.TotalRows,
                //                    flpConfigurationId: flpConfigurationId,
                //                    fileUploadedId: uploadedFileId,
                //                    FileStatusActivityEnum.Error,
                //                    FlpActivityLogStatusEnum.FileDeletedFromTemp,
                //                        null
                //         );


                //    }             

                //  //  _logger.LogError($"Multisheet Error: deleting file from location:{resultResponse.ParquetBlobClient.Name}");

                //}

                return (deleted, messsage);
        }


        private async Task<(bool, string)> DeleteParquetFileFromParquetLocation(long processId, string fileType, ParquetFileResponseDto resultResponse, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDtoV4_1 flpConfigurationRequestDto, SharedLocationDestinationServerDtoV4_1 slDestinationServerDto)
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
             tabName: configurationTableMappingDto.TabName,
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
             FlpActivityLogStatusEnum.DeletedParquetLocation,
                     null
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
                 tabName: configurationTableMappingDto.TabName,
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
                 FlpActivityLogStatusEnum.DeletedParquetLocation,
                     null
                );
            }
            else
            {
                await AddFileProcessLosStatus(
                 tabName: configurationTableMappingDto.TabName,
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
                 FlpActivityLogStatusEnum.DeletedParquetLocation,
                     null
                );
                _logger.LogError($"Error: deleting file from location:{flpConfigurationRequestDto.FlpConfigurationId}");
                //return

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
