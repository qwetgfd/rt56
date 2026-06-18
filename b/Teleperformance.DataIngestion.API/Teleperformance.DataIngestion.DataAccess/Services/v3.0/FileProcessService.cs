using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using Teleperformance.DataIngestion.Models.Models.v4._1;
using static Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus.StatusResponseDto;

namespace Teleperformance.DataIngestion.DataAccess.Services.v3._0
{
    public class FileProcessService: IFileProcessService
    {
        private readonly ILogger<FileProcessService> _logger;
        private readonly IFileLoadingProcessConfiguration _fileLoadingProcessConfiguration;
        private readonly ITextToParquetService _iTxtToParquetConverterService;
        private readonly IExcelToParquetService _iExcelToParquetService;
        private readonly ICsvToParquetService _iCsvToParquetConverterService;
        private readonly IFlpProcessingService _iIFileProcessingService;
        private readonly IValidateSchemaService _iValidateSchemaService;

        public FileProcessService(ILogger<FileProcessService> logger, IFileLoadingProcessConfiguration fileLoadingProcessConfiguration, ITextToParquetService iTxtToParquetConverterService, IFlpProcessingService iIFileProcessingService, IValidateSchemaService iValidateSchemaService, ICsvToParquetService iCsvToParquetConverterService, IExcelToParquetService iExcelToParquetService)
        {
            _logger = logger;
            _fileLoadingProcessConfiguration = fileLoadingProcessConfiguration;
            _iTxtToParquetConverterService = iTxtToParquetConverterService;
            _iIFileProcessingService = iIFileProcessingService;
            _iValidateSchemaService = iValidateSchemaService;
            _iCsvToParquetConverterService = iCsvToParquetConverterService;
            _iExcelToParquetService = iExcelToParquetService;
        }


        public async Task<APIResponse<FlpProcessResponseDto>> ProcessExcelFile(FlpRequestDto flpRequestDto)
        {
            string uploadedFileId = FlpConfigurationHelper.GetUploadedFileId(flpRequestDto);

            if (string.IsNullOrWhiteSpace(uploadedFileId))
            {
                _logger.LogError($"Uploaded File Id is null for {flpRequestDto?.FlpConfigurationId ?? ""}");
            }
            var configurationResult = await _fileLoadingProcessConfiguration.GetFlpProcessByConfigurationId(flpRequestDto.FlpConfigurationId, uploadedFileId);

            if (configurationResult.ResponseCode == 200)
            {

                var flpConfigurationRequestDto = configurationResult.Result;
                DestinationStorageAccountDto destinationStorageAccountDto = null;
                SharedLocationDestinationServerDto sharedLocationDestinationServerDto = null;
                if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure)
                {

                    flpConfigurationRequestDto.BlobClients = flpRequestDto.BlobClients;
                    flpConfigurationRequestDto.UploadedFileId = flpRequestDto.BlobClients.UploadedId;
                    var blobClients = flpConfigurationRequestDto.BlobClients;
                    if (blobClients == null)
                    {

                        await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Error);
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { "Not found blob client details" },
                            Result = null
                        });
                    }

                    var isValidFile1 = await _fileLoadingProcessConfiguration.IsValidFile(flpConfigurationRequestDto.FlpConfigurationId, blobClients.Name, ".xlsx");
                    var isValidFile2 = await _fileLoadingProcessConfiguration.IsValidFile(flpConfigurationRequestDto.FlpConfigurationId, blobClients.Name, ".xls");
                    var isValidFile3 = await _fileLoadingProcessConfiguration.IsValidFile(flpConfigurationRequestDto.FlpConfigurationId, blobClients.Name, ".xlsb");
                    if (!isValidFile1 && !isValidFile2 && !isValidFile3)
                    {
                        await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Error);
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { "Invalid file extention" },
                            Result = null
                        });
                    }

                }
                else if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.OnPrem)
                {
                    flpConfigurationRequestDto.SourcePath = flpRequestDto.OnPremFileLocation.FileUrl;
                    flpConfigurationRequestDto.UploadedFileId = flpRequestDto.OnPremFileLocation.UploadedId;

                    var isValidFile1 = await _fileLoadingProcessConfiguration.IsValidFile(flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.SourcePath, ".xlsx");
                    var isValidFile2 = await _fileLoadingProcessConfiguration.IsValidFile(flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.SourcePath, ".xls");
                    var isValidFile3 = await _fileLoadingProcessConfiguration.IsValidFile(flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.SourcePath, ".xlsb");
                    if (!isValidFile1 && !isValidFile2 && !isValidFile3)
                    {
                        await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Error);
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { "Invalid file extention" },
                            Result = null
                        });
                    }
                }
                if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                {
                    destinationStorageAccountDto = await _fileLoadingProcessConfiguration.DestinationstorageAccountInfo(flpConfigurationRequestDto.FlpConfigurationId);
                }
                else if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.OnPrem)
                {
                    sharedLocationDestinationServerDto = await _fileLoadingProcessConfiguration.SharedLocationDestinationServerDetails(flpConfigurationRequestDto.FlpConfigurationId);

                }
                var res = await ExcelProcess(flpConfigurationRequestDto, destinationStorageAccountDto, sharedLocationDestinationServerDto);
              

                if (!string.IsNullOrWhiteSpace(flpConfigurationRequestDto.UploadedFileId))
                {
                    int totalRows = res.Result?.TotalRows ?? 0;
                    int insertedRows = res.Result?.InsertedRows ?? 0;
                    int duplicateRows = res.Result?.DuplicateRows ?? 0;
                    string blobName = res.Result?.BlobName ?? string.Empty;
                    await _fileLoadingProcessConfiguration.AddBackUpFileDetails(flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.FlpConfigurationId, res.Result?.BackUpFileName, null, totalRows, insertedRows, duplicateRows, blobName);
                }
                else
                {
                    _logger.LogError($"Not updated backup file details for {flpConfigurationRequestDto.FlpConfigurationId}");
                }
                await UpdateProcessStatus(flpConfigurationRequestDto, res);
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = res.ResultStatus,
                    ResponseMessage = res.ResponseMessage,
                    Result = res.Result
                });

            }
            else
            {
                await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpRequestDto.FlpConfigurationId, APIResultStatus.Error);
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = configurationResult.ResultStatus,
                    ResponseMessage = new List<string> { $"Not found records for {flpRequestDto.FlpConfigurationId}" },
                    Result = null
                });
            }
        }

        public async Task<APIResponse<FlpProcessResponseDto>> ExcelProcess(FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto)
        {
            FlpProcessResponseDto flpConvertToParquetResponseDto = new FlpProcessResponseDto();
            string fileType = "excel";

            ConfigurationTableMappingDto configurationTableMappingDto = (await GetMappingTableNameList(flpConfigurationRequestDto?.FlpConfigurationId ?? string.Empty, null, "xlsx", false)).FirstOrDefault();

            if (configurationTableMappingDto == null)
            {

                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { $"Not found flpConfigurationlist for {flpConfigurationRequestDto.FlpConfigurationId} " },
                    Result = null
                });
            }

            if (!FlpConfigurationHelper.ValidString(configurationTableMappingDto.DatabaseConnectionSecret))
            {
                _logger.LogError($"Not found DatabaseConnectionSecret for {flpConfigurationRequestDto.FlpConfigurationId}");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { $"Not found DatabaseConnectionSecret for {flpConfigurationRequestDto.FlpConfigurationId}" },
                    Result = null
                });
            }


            //flpConfigurationRequestDto.TableName = tableName;
            string connectionString = KeyVault.GetKeyVaultValue(configurationTableMappingDto.DatabaseConnectionSecret).Result;
            long processId = long.Parse(DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            string fileLocation = FlpConfigurationHelper.GetFileLocation(flpConfigurationRequestDto);
            string fileUploadedId = "";// flpConfigurationRequestDto.BlobClients !=null? flpConfigurationRequestDto.BlobClients.UploadedId:flpConfigurationRequestDto.UploadedFileId;
            string backupFileName = "";

            if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure)
            {
                fileUploadedId = flpConfigurationRequestDto.BlobClients.UploadedId;
                backupFileName = FlpConfigurationHelper.GetBackUpFileName(flpConfigurationRequestDto.BlobClients.Name, processId.ToString());
            }
            else
            {
                backupFileName = FlpConfigurationHelper.GetBackUpFileName(flpConfigurationRequestDto.SourcePath, processId.ToString());
                fileUploadedId = flpConfigurationRequestDto.UploadedFileId;

            }

            try
            {
                //FlpConfigurationMethods.GetBackUpFileName(flpConfigurationRequestDto.BlobClients.Name, processId.ToString());
                flpConvertToParquetResponseDto.BackUpFileName = backupFileName;
                //(FlpProcessTempFile? flpProcessTempFile, bool movedFileToTemp) = await _iIFileProcessingService.MoveSourceExcelFileToTemporaryDestinationAndDelete(processId, fileType, fileLocation, fileUploadedId, backupFileName, configurationTableMappingDto, flpConfigurationRequestDto, destinationStorageAccountDto, slDestinationServerDto);
                //if (!movedFileToTemp)
                //{
                //    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                //    {
                //        ResultStatus = APIResultStatus.InvalidParameters,
                //        ResponseMessage = new List<string> { "Something went wrong" },
                //        Result = null
                //    });
                //}

                (FlpProcessTempFile? flpProcessTempFile, bool movedFileToTemp) = await _iIFileProcessingService.MoveSourceFileToTemporaryDestinationAndDelete(processId, fileType, fileLocation, fileUploadedId, backupFileName, configurationTableMappingDto, flpConfigurationRequestDto, destinationStorageAccountDto, slDestinationServerDto);
                if (!movedFileToTemp)
                {
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Something went wrong" },
                        Result = null
                    });
                }

                FlpActivityLogStatusEnum currentStatus = FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation;
                flpConvertToParquetResponseDto.BlobName = flpProcessTempFile?.Name ?? "";

                await _iIFileProcessingService.AddFileProcessLosStatus(
                   fileType: fileType,
                   loginId: "",
                   message: $"File schema validation in progress",
                   messageType: "info",
                   processId: processId,
                   processName: flpConfigurationRequestDto.ProcessName,
                   tableName: configurationTableMappingDto.TableName,
                   totalRows: 0,
                   flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                   fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                   FileStatusActivityEnum.Processing,
                   FlpActivityLogStatusEnum.FileSchemaValidated
                );

                ParquetFileResponseDto resultResponse = null;
                //var csvToParquetStream = new FlpCsvToParquet(_activityLoggerRepository, _dataRepository, _ismbLibraryServices);
                flpConfigurationRequestDto.ProcessId = processId;
                flpConfigurationRequestDto.FileType = fileType;
                bool addRowNo = false;
                if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                {
                    //if(flpProcessTempFile?.FileSize > 50)
                    //{
                    //    resultResponse = await _iCsvToParquetConverterService.ConvertDataToParquetExcel(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);
                    //}
                    //else
                    //{
                    //    resultResponse = await _iExcelToParquetService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);
                    //}
                    //
                    if (flpConfigurationRequestDto.UIValidation || flpConfigurationRequestDto.BEValidation)
                    {
                        _logger.LogInformation($"Updating RowNo for  {flpConfigurationRequestDto.FlpConfigurationId} and uploadFileId {flpConfigurationRequestDto.UploadedFileId}, tabName {configurationTableMappingDto.TabName}");
                        //Call the API to update row no
                        var token = _iIFileProcessingService.GetBearerToken();
                        var skipRows = configurationTableMappingDto.SkipRows == 0 ? 1 : configurationTableMappingDto.SkipRows;
                        var validationRuleServiceHelper = new ValidationRuleServiceHelper(_logger);
                        var result = await validationRuleServiceHelper.AddExcelRowNo(flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.UploadedFileId, configurationTableMappingDto.TabName,
                             token, flpConfigurationRequestDto.DestinationPath, destinationStorageAccountDto.StorageContainerName, flpProcessTempFile?.Uri ?? "", destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.SasKey, skipRows, skipRows + 1, false, configurationTableMappingDto.IsHeaderProvided,configurationTableMappingDto.SkipEmptyLines);
                        if (result.ResultStatus.Code != APIResultStatus.Completed.Code)
                        {
                            addRowNo = true;
                            //Add error log
                            await _iIFileProcessingService.AddFileProcessLosStatus(
                               fileType: fileType,
                               loginId: "",
                               message: $"Error: {result.ResponseMessage.FirstOrDefault()}",
                               messageType: "info",
                               processId: processId,
                               processName: flpConfigurationRequestDto.ProcessName,
                               tableName: configurationTableMappingDto.TableName,
                               totalRows: 0,
                               flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                               fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                               FileStatusActivityEnum.Error,
                               FlpActivityLogStatusEnum.FileSchemaValidated
                            );

                        }
                        else
                        {
                            resultResponse = await _iExcelToParquetService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);

                        }
                        // resultResponse = await _iTxtToParquetConverterService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);

                    }
                    else
                    {
                        resultResponse = await _iExcelToParquetService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);
                        

                    }
                    //resultResponse = await _iTxtToParquetConverterService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);
                }
                else
                {
                    CheckConnectivitySMBLibraryModel destinationServerModel = new CheckConnectivitySMBLibraryModel
                    {
                        serverIP = slDestinationServerDto.ServerName,
                        username = slDestinationServerDto.UserName,
                        password = slDestinationServerDto.Password,
                        sharedFolderName = slDestinationServerDto.FolderName,
                        domain = slDestinationServerDto.Domain
                    };
                    resultResponse = await _iExcelToParquetService.ConvertDataToParquetOnPremSharedLocation(flpProcessTempFile?.sourceTempFilePath ?? "", configurationTableMappingDto, flpConfigurationRequestDto, destinationServerModel);
                }
                if (resultResponse != null && resultResponse.ParquetFileCreated)
                {
                    var response = await _iIFileProcessingService.ParquetFileProcessToBronzeTable(processId, fileType, fileLocation, null, connectionString, currentStatus, flpProcessTempFile, resultResponse, configurationTableMappingDto, flpConfigurationRequestDto, destinationStorageAccountDto, slDestinationServerDto);
                    return response;
                }
                else
                {
                    string errorMessage = resultResponse?.ErrorMessage ?? "Something went wrong";
                    if (!addRowNo && resultResponse?.flpActivityLogStatusEnum != FlpActivityLogStatusEnum.FileSchemaValidated)
                    {
                        //Add error log
                        await _iIFileProcessingService.AddFileProcessLosStatus(
                              fileType: fileType,
                              loginId: "",
                              message: $"Error:{errorMessage}",
                              messageType: "error",
                              processId: processId,
                              processName: flpConfigurationRequestDto.ProcessName,
                              tableName: configurationTableMappingDto.TableName,// flpConfigurationRequestDto.TableName,
                              totalRows: 0,
                              flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                              fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                              FileStatusActivityEnum.Error,
                              FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation
                         );
                    }
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { errorMessage },
                        Result = flpConvertToParquetResponseDto
                    });
                }


            }
            catch (Exception ex)
            {
                _logger.LogError($"No records found in flpConfigurations.{flpConfigurationRequestDto.FlpConfigurationId}", $"Error:{ex.Message}");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Something went wrong" },
                    Result = flpConvertToParquetResponseDto
                });
            }
        }

        public async Task<APIResponse<FlpProcessResponseDto>> ProcessCsvFile(FlpRequestDto flpRequestDto)
        {
            string uploadedFileId = FlpConfigurationHelper.GetUploadedFileId(flpRequestDto);

            if (string.IsNullOrWhiteSpace(uploadedFileId))
            {
                _logger.LogError($"Uploaded File Id is null for {flpRequestDto?.FlpConfigurationId ?? ""}");
            }
            var configurationResult = await _fileLoadingProcessConfiguration.GetFlpProcessByConfigurationId(flpRequestDto.FlpConfigurationId, uploadedFileId);

            if (configurationResult.ResponseCode == 200)
            {

                var flpConfigurationRequestDto = configurationResult.Result;
                DestinationStorageAccountDto destinationStorageAccountDto = null;
                SharedLocationDestinationServerDto sharedLocationDestinationServerDto = null;
                if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure)
                {

                    flpConfigurationRequestDto.BlobClients = flpRequestDto.BlobClients;
                    flpConfigurationRequestDto.UploadedFileId = flpRequestDto.BlobClients.UploadedId;
                    var blobClients = flpConfigurationRequestDto.BlobClients;
                    if (blobClients == null)
                    {

                        await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Error);
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { "Not found blob client details" },
                            Result = null
                        });
                    }

                    var isValidFile = await _fileLoadingProcessConfiguration.IsValidFile(flpConfigurationRequestDto.FlpConfigurationId, blobClients.Name, ".csv");

                    if (!isValidFile)
                    {

                        await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Error);
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { "Invalid file extention" },
                            Result = null
                        });
                    }

                }
                else if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.OnPrem)
                {
                    flpConfigurationRequestDto.SourcePath = flpRequestDto.OnPremFileLocation.FileUrl;
                    flpConfigurationRequestDto.UploadedFileId = flpRequestDto.OnPremFileLocation.UploadedId;

                    var isValidFile = await _fileLoadingProcessConfiguration.IsValidFile(flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.SourcePath, ".csv");
                    if (!isValidFile)
                    {
                        await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Error);
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { "Invalid file extention" },
                            Result = null
                        });
                    }
                }
                if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                {
                    destinationStorageAccountDto = await _fileLoadingProcessConfiguration.DestinationstorageAccountInfo(flpConfigurationRequestDto.FlpConfigurationId);
                }
                else if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.OnPrem)
                {
                    sharedLocationDestinationServerDto = await _fileLoadingProcessConfiguration.SharedLocationDestinationServerDetails(flpConfigurationRequestDto.FlpConfigurationId);

                }
                var res = await CsvProcess(flpConfigurationRequestDto, destinationStorageAccountDto, sharedLocationDestinationServerDto);
                

                if (!string.IsNullOrWhiteSpace(flpConfigurationRequestDto.UploadedFileId))
                {
                    int totalRows = res.Result?.TotalRows ?? 0;
                    int insertedRows = res.Result?.InsertedRows ?? 0;
                    int duplicateRows = res.Result?.DuplicateRows ?? 0;
                    string blobName = res.Result?.BlobName ?? string.Empty;
                    await _fileLoadingProcessConfiguration.AddBackUpFileDetails(flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.FlpConfigurationId, res.Result?.BackUpFileName, null, totalRows, insertedRows, duplicateRows, blobName);
                }
                else
                {
                    _logger.LogError($"Not updated backup file details for {flpConfigurationRequestDto.FlpConfigurationId}");
                }

                await UpdateProcessStatus(flpConfigurationRequestDto, res);

                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = res.ResultStatus,
                    ResponseMessage = res.ResponseMessage,
                    Result = res.Result
                });

            }
            else
            {
                await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpRequestDto.FlpConfigurationId, APIResultStatus.Error);
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = configurationResult.ResultStatus,
                    ResponseMessage = new List<string> { $"Not found records for {flpRequestDto.FlpConfigurationId}" },
                    Result = null
                });
            }
        }

        public async Task<APIResponse<FlpProcessResponseDto>> CsvProcess(FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto)
        {
            FlpProcessResponseDto flpConvertToParquetResponseDto = new FlpProcessResponseDto();
            string fileType = "csv";

            ConfigurationTableMappingDto configurationTableMappingDto = (await GetMappingTableNameList(flpConfigurationRequestDto?.FlpConfigurationId ?? string.Empty, null, fileType, false)).FirstOrDefault();

            if (configurationTableMappingDto == null)
            {

                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { $"Not found flpConfigurationlist for {flpConfigurationRequestDto.FlpConfigurationId} " },
                    Result = null
                });
            }

            if (!FlpConfigurationHelper.ValidString(configurationTableMappingDto.DatabaseConnectionSecret))
            {
                _logger.LogError($"Not found DatabaseConnectionSecret for {flpConfigurationRequestDto.FlpConfigurationId}");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { $"Not found DatabaseConnectionSecret for {flpConfigurationRequestDto.FlpConfigurationId}" },
                    Result = null
                });
            }


            //flpConfigurationRequestDto.TableName = tableName;
            string connectionString = KeyVault.GetKeyVaultValue(configurationTableMappingDto.DatabaseConnectionSecret).Result;
            long processId = long.Parse(DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            string fileLocation = FlpConfigurationHelper.GetFileLocation(flpConfigurationRequestDto);
            string fileUploadedId = "";// flpConfigurationRequestDto.BlobClients !=null? flpConfigurationRequestDto.BlobClients.UploadedId:flpConfigurationRequestDto.UploadedFileId;
            string backupFileName = "";

            if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure)
            {
                fileUploadedId = flpConfigurationRequestDto.BlobClients.UploadedId;
                backupFileName = FlpConfigurationHelper.GetBackUpFileName(flpConfigurationRequestDto.BlobClients.Name, processId.ToString());
            }
            else
            {
                backupFileName = FlpConfigurationHelper.GetBackUpFileName(flpConfigurationRequestDto.SourcePath, processId.ToString());
                fileUploadedId = flpConfigurationRequestDto.UploadedFileId;

            }

            try
            {
                //FlpConfigurationMethods.GetBackUpFileName(flpConfigurationRequestDto.BlobClients.Name, processId.ToString());
                flpConvertToParquetResponseDto.BackUpFileName = backupFileName;
                (FlpProcessTempFile? flpProcessTempFile, bool movedFileToTemp) = await _iIFileProcessingService.MoveSourceFileToTemporaryDestinationAndDelete(processId, fileType, fileLocation, fileUploadedId, backupFileName, configurationTableMappingDto, flpConfigurationRequestDto, destinationStorageAccountDto, slDestinationServerDto);
                if (!movedFileToTemp)
                {
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Something went wrong" },
                        Result = null
                    });
                }

                FlpActivityLogStatusEnum currentStatus = FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation;
                flpConvertToParquetResponseDto.BlobName = flpProcessTempFile?.Name ?? "";

                await _iIFileProcessingService.AddFileProcessLosStatus(
                   fileType: fileType,
                   loginId: "",
                   message: $"File schema validation in progress",
                   messageType: "info",
                   processId: processId,
                   processName: flpConfigurationRequestDto.ProcessName,
                   tableName: configurationTableMappingDto.TableName,
                   totalRows: 0,
                   flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                   fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                   FileStatusActivityEnum.Processing,
                   FlpActivityLogStatusEnum.FileSchemaValidated
                );

                ParquetFileResponseDto resultResponse = null;
                //var csvToParquetStream = new FlpCsvToParquet(_activityLoggerRepository, _dataRepository, _ismbLibraryServices);
                flpConfigurationRequestDto.ProcessId = processId;
                flpConfigurationRequestDto.FileType = fileType;
                bool AddRowNo = false;



                if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                {
                    if (flpConfigurationRequestDto.UIValidation || flpConfigurationRequestDto.BEValidation)
                    {
                        _logger.LogInformation($"Updating RowNo for  {flpConfigurationRequestDto.FlpConfigurationId} and uploadFileId {flpConfigurationRequestDto.UploadedFileId}, tabName {configurationTableMappingDto.TabName}");
                        //Call the API to update row no
                        var token = _iIFileProcessingService.GetBearerToken();
                        var skipRows = configurationTableMappingDto.SkipRows == 0 ? 1 : configurationTableMappingDto.SkipRows;
                        var validationRuleServiceHelper = new ValidationRuleServiceHelper(_logger);
                        var result = await validationRuleServiceHelper.AddCsvRowNo(flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.UploadedFileId, configurationTableMappingDto.Delimiter,
                             token, flpConfigurationRequestDto.DestinationPath, destinationStorageAccountDto.StorageContainerName, flpProcessTempFile?.Uri ?? "", destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.SasKey, skipRows, skipRows + 1, false,configurationTableMappingDto.IsHeaderProvided,configurationTableMappingDto.SkipEmptyLines);
                        if (result.ResultStatus.Code != APIResultStatus.Completed.Code)
                        {
                            AddRowNo = true;

                            //Add error log
                            await _iIFileProcessingService.AddFileProcessLosStatus(
                               fileType: fileType,
                               loginId: "",
                               message: $"Error: {result.ResponseMessage.FirstOrDefault()}",
                               messageType: "info",
                               processId: processId,
                               processName: flpConfigurationRequestDto.ProcessName,
                               tableName: configurationTableMappingDto.TableName,
                               totalRows: 0,
                               flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                               fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                               FileStatusActivityEnum.Error,
                               FlpActivityLogStatusEnum.FileSchemaValidated
                            );

                        }else
                            resultResponse = await _iCsvToParquetConverterService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);

                    }
                    else
                        resultResponse = await _iCsvToParquetConverterService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);

                }
                else
                {
                    CheckConnectivitySMBLibraryModel destinationServerModel = new CheckConnectivitySMBLibraryModel
                    {
                        serverIP = slDestinationServerDto.ServerName,
                        username = slDestinationServerDto.UserName,
                        password = slDestinationServerDto.Password,
                        sharedFolderName = slDestinationServerDto.FolderName,
                        domain = slDestinationServerDto.Domain
                    };
                    resultResponse = await _iCsvToParquetConverterService.ConvertDataToParquetOnPremSharedLocation(flpProcessTempFile?.sourceTempFilePath ?? "", configurationTableMappingDto, flpConfigurationRequestDto, destinationServerModel);
                }
                if (resultResponse != null && resultResponse.ParquetFileCreated)
                {
                    var response = await _iIFileProcessingService.ParquetFileProcessToBronzeTable(processId, fileType, fileLocation, null, connectionString, currentStatus, flpProcessTempFile, resultResponse, configurationTableMappingDto, flpConfigurationRequestDto, destinationStorageAccountDto, slDestinationServerDto);                   
                    return response;
                                  
                
                }
                else
                {
                    string errorMessage = resultResponse?.ErrorMessage ?? "Something went wrong";
                    if (!AddRowNo && resultResponse?.flpActivityLogStatusEnum != FlpActivityLogStatusEnum.FileSchemaValidated)
                    {
                        //Add error log
                        await _iIFileProcessingService.AddFileProcessLosStatus(
                              fileType: fileType,
                              loginId: "",
                              message: $"Error:{errorMessage}",
                              messageType: "error",
                              processId: processId,
                              processName: flpConfigurationRequestDto.ProcessName,
                              tableName: configurationTableMappingDto.TableName,// flpConfigurationRequestDto.TableName,
                              totalRows: 0,
                              flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                              fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                              FileStatusActivityEnum.Error,
                              FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation
                         );
                    }
                    // Set the row counts even for unsuccessful responses
                    flpConvertToParquetResponseDto.TotalRows = resultResponse?.TotalRows ?? 0;
                    flpConvertToParquetResponseDto.DuplicateRows = resultResponse?.DuplicateRows ?? 0;
                    flpConvertToParquetResponseDto.InsertedRows = resultResponse?.InsertedRows ?? 0;
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { errorMessage },
                        Result = flpConvertToParquetResponseDto
                    });
                }


            }
            catch (Exception ex)
            {
                _logger.LogError($"No records found in flpConfigurations.{flpConfigurationRequestDto.FlpConfigurationId}", $"Error:{ex.Message}");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Something went wrong" },
                    Result = flpConvertToParquetResponseDto
                });
            }
        }
        public async Task<APIResponse<FlpProcessResponseDto>> ProcessTxtFile(FlpRequestDto flpRequestDto)
        {
            string uploadedFileId = FlpConfigurationHelper.GetUploadedFileId(flpRequestDto);

            if (string.IsNullOrWhiteSpace(uploadedFileId))
            {
                _logger.LogError($"Uploaded File Id is null for {flpRequestDto?.FlpConfigurationId ?? ""}");
            }
            var configurationResult = await _fileLoadingProcessConfiguration.GetFlpProcessByConfigurationId(flpRequestDto.FlpConfigurationId, uploadedFileId);

            if (configurationResult.ResponseCode == 200)
            {

                var flpConfigurationRequestDto = configurationResult.Result;
                DestinationStorageAccountDto destinationStorageAccountDto = null;
                SharedLocationDestinationServerDto sharedLocationDestinationServerDto = null;
                if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure)
                {

                    flpConfigurationRequestDto.BlobClients = flpRequestDto.BlobClients;
                    flpConfigurationRequestDto.UploadedFileId = flpRequestDto.BlobClients.UploadedId;
                    var blobClients = flpConfigurationRequestDto.BlobClients;
                    if (blobClients == null)
                    {

                        await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Error);
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { "Not found blob client details" },
                            Result = null
                        });
                    }

                    var isValidFile = await _fileLoadingProcessConfiguration.IsValidFile(flpConfigurationRequestDto.FlpConfigurationId, blobClients.Name, ".txt");

                    if (!isValidFile)
                    {

                        await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Error);
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { "Invalid file extention" },
                            Result = null
                        });
                    }

                }
                else if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.OnPrem)
                {
                    flpConfigurationRequestDto.SourcePath = flpRequestDto.OnPremFileLocation.FileUrl;
                    flpConfigurationRequestDto.UploadedFileId = flpRequestDto.OnPremFileLocation.UploadedId;

                    var isValidFile = await _fileLoadingProcessConfiguration.IsValidFile(flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.SourcePath, ".txt");
                    if (!isValidFile)
                    {
                        await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Error);
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { "Invalid file extention" },
                            Result = null
                        });
                    }
                }
                if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                {
                    destinationStorageAccountDto = await _fileLoadingProcessConfiguration.DestinationstorageAccountInfo(flpConfigurationRequestDto.FlpConfigurationId);
                }
                else if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.OnPrem)
                {
                    sharedLocationDestinationServerDto = await _fileLoadingProcessConfiguration.SharedLocationDestinationServerDetails(flpConfigurationRequestDto.FlpConfigurationId);

                }
                var res = await TxtProcess(flpConfigurationRequestDto, destinationStorageAccountDto, sharedLocationDestinationServerDto);                

                if (!string.IsNullOrWhiteSpace(flpConfigurationRequestDto.UploadedFileId))
                {
                    int totalRows = res.Result?.TotalRows ?? 0;
                    int insertedRows = res.Result?.InsertedRows ?? 0;
                    int duplicateRows = res.Result?.DuplicateRows ?? 0;
                    string blobName = res.Result?.BlobName ?? string.Empty;
                    await _fileLoadingProcessConfiguration.AddBackUpFileDetails(flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.FlpConfigurationId, res.Result?.BackUpFileName, null, totalRows, insertedRows, duplicateRows, blobName);
                }
                else
                {
                    _logger.LogError($"Not updated backup file details for {flpConfigurationRequestDto.FlpConfigurationId}");
                }
                await UpdateProcessStatus(flpConfigurationRequestDto, res);
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = res.ResultStatus,
                    ResponseMessage = res.ResponseMessage,
                    Result = res.Result
                });

            }
            else
            {
                await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpRequestDto.FlpConfigurationId, APIResultStatus.Error);
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = configurationResult.ResultStatus,
                    ResponseMessage = new List<string> { $"Not found records for {flpRequestDto.FlpConfigurationId}" },
                    Result = null
                });
            }
        }

        public async Task<APIResponse<FlpProcessResponseDto>> TxtProcess(FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto)
        {
            FlpProcessResponseDto flpConvertToParquetResponseDto = new FlpProcessResponseDto();
            string fileType = "txt";

            ConfigurationTableMappingDto configurationTableMappingDto = (await GetMappingTableNameList(flpConfigurationRequestDto?.FlpConfigurationId ?? string.Empty, null, fileType, false)).FirstOrDefault();

            if (configurationTableMappingDto == null)
            {

                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { $"Not found flpConfigurationlist for {flpConfigurationRequestDto.FlpConfigurationId} " },
                    Result = null
                });
            }

            if (!FlpConfigurationHelper.ValidString(configurationTableMappingDto.DatabaseConnectionSecret))
            {
                _logger.LogError($"Not found DatabaseConnectionSecret for {flpConfigurationRequestDto.FlpConfigurationId}");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { $"Not found DatabaseConnectionSecret for {flpConfigurationRequestDto.FlpConfigurationId}" },
                    Result = null
                });
            }


            //flpConfigurationRequestDto.TableName = tableName;
            string connectionString = KeyVault.GetKeyVaultValue(configurationTableMappingDto.DatabaseConnectionSecret).Result;
            long processId = long.Parse(DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            string fileLocation = FlpConfigurationHelper.GetFileLocation(flpConfigurationRequestDto);
            string fileUploadedId = "";// flpConfigurationRequestDto.BlobClients !=null? flpConfigurationRequestDto.BlobClients.UploadedId:flpConfigurationRequestDto.UploadedFileId;
            string backupFileName = "";

            if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure)
            {
                fileUploadedId = flpConfigurationRequestDto.BlobClients.UploadedId;
                backupFileName = FlpConfigurationHelper.GetBackUpFileName(flpConfigurationRequestDto.BlobClients.Name, processId.ToString());
            }
            else
            {
                backupFileName = FlpConfigurationHelper.GetBackUpFileName(flpConfigurationRequestDto.SourcePath, processId.ToString());
                fileUploadedId = flpConfigurationRequestDto.UploadedFileId;

            }

            try
            {
                //FlpConfigurationMethods.GetBackUpFileName(flpConfigurationRequestDto.BlobClients.Name, processId.ToString());
                flpConvertToParquetResponseDto.BackUpFileName = backupFileName;
                (FlpProcessTempFile? flpProcessTempFile, bool movedFileToTemp) = await _iIFileProcessingService.MoveSourceFileToTemporaryDestinationAndDelete(processId, fileType, fileLocation, fileUploadedId, backupFileName, configurationTableMappingDto, flpConfigurationRequestDto, destinationStorageAccountDto, slDestinationServerDto);
                if (!movedFileToTemp)
                {
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Something went wrong" },
                        Result = null
                    });
                }

                FlpActivityLogStatusEnum currentStatus = FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation;
                flpConvertToParquetResponseDto.BlobName = flpProcessTempFile?.Name ?? "";

                await _iIFileProcessingService.AddFileProcessLosStatus(
                   fileType: fileType,
                   loginId: "",
                   message: $"File schema validation in progress",
                   messageType: "info",
                   processId: processId,
                   processName: flpConfigurationRequestDto.ProcessName,
                   tableName: configurationTableMappingDto.TableName,
                   totalRows: 0,
                   flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                   fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                   FileStatusActivityEnum.Processing,
                   FlpActivityLogStatusEnum.FileSchemaValidated
                );
                
               /*
                await _iIFileProcessingService.AddFileProcessLosStatus(
                          fileType: fileType,
                          loginId: "",
                          message: $"File conversion process in progress",
                          messageType: "info",
                          processId: processId,
                          processName: flpConfigurationRequestDto.ProcessName,
                          tableName: configurationTableMappingDto.TableName,//flpConfigurationRequestDto.TableName,
                          totalRows: 0,
                          flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                          fileUploadedId: fileUploadedId,
                          FileStatusActivityEnum.Processing,
                          FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation);*/
                ParquetFileResponseDto resultResponse = null;
                //var csvToParquetStream = new FlpCsvToParquet(_activityLoggerRepository, _dataRepository, _ismbLibraryServices);
                flpConfigurationRequestDto.ProcessId=processId;
                flpConfigurationRequestDto.FileType = fileType;
                bool AddRowNo = false;

                if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                {
                    if (flpConfigurationRequestDto.UIValidation || flpConfigurationRequestDto.BEValidation)
                    {
                        _logger.LogInformation($"Updating RowNo for  {flpConfigurationRequestDto.FlpConfigurationId} and uploadFileId {flpConfigurationRequestDto.UploadedFileId}, tabName {configurationTableMappingDto.TabName}");
                        //Call the API to update row no
                        var token = _iIFileProcessingService.GetBearerToken();
                        var skipRows = configurationTableMappingDto.SkipRows == 0 ? 1 : configurationTableMappingDto.SkipRows;
                        var validationRuleServiceHelper = new ValidationRuleServiceHelper(_logger);
                        var result = await validationRuleServiceHelper.AddTextRowNo(flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.UploadedFileId, configurationTableMappingDto.Delimiter,
                             token, flpConfigurationRequestDto.DestinationPath, destinationStorageAccountDto.StorageContainerName, flpProcessTempFile?.Uri ?? "",
                             destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.SasKey, skipRows, skipRows + 1,false,  configurationTableMappingDto.IsHeaderProvided, configurationTableMappingDto.SkipEmptyLines);
                        if (result.ResultStatus.Code != APIResultStatus.Completed.Code)
                        {
                            AddRowNo = true;
                            //Add error log
                            await _iIFileProcessingService.AddFileProcessLosStatus(
                               fileType: fileType,
                               loginId: "",
                               message: $"Error: {result.ResponseMessage.FirstOrDefault()}",
                               messageType: "info",
                               processId: processId,
                               processName: flpConfigurationRequestDto.ProcessName,
                               tableName: configurationTableMappingDto.TableName,
                               totalRows: 0,
                               flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                               fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                               FileStatusActivityEnum.Error,
                               FlpActivityLogStatusEnum.FileSchemaValidated
                            );

                        }
                        else
                            resultResponse = await _iTxtToParquetConverterService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);

                    }
                    else
                        resultResponse = await _iTxtToParquetConverterService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);
                    // resultResponse = await _iTxtToParquetConverterService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);

                }
                else
                {
                    CheckConnectivitySMBLibraryModel destinationServerModel = new CheckConnectivitySMBLibraryModel
                    {
                        serverIP = slDestinationServerDto.ServerName,
                        username = slDestinationServerDto.UserName,
                        password = slDestinationServerDto.Password,
                        sharedFolderName = slDestinationServerDto.FolderName,
                        domain = slDestinationServerDto.Domain
                    };
                    resultResponse = await _iTxtToParquetConverterService.ConvertDataToParquetOnPremSharedLocation(flpProcessTempFile?.sourceTempFilePath ?? "", configurationTableMappingDto, flpConfigurationRequestDto, destinationServerModel);
                }
                if (resultResponse != null && resultResponse.ParquetFileCreated)
                {
                    var response = await _iIFileProcessingService.ParquetFileProcessToBronzeTable(processId, fileType, fileLocation, null, connectionString, currentStatus, flpProcessTempFile, resultResponse, configurationTableMappingDto, flpConfigurationRequestDto, destinationStorageAccountDto, slDestinationServerDto);
                    return response;
                }              
                else
                {
                    string errorMessage = resultResponse?.ErrorMessage ?? "Something went wrong";
                    if (!AddRowNo && resultResponse?.flpActivityLogStatusEnum != FlpActivityLogStatusEnum.FileSchemaValidated)
                    {                        
                        //Add error log
                        await _iIFileProcessingService.AddFileProcessLosStatus(
                              fileType: fileType,
                              loginId: "",
                              message: $"Error:{errorMessage}",
                              messageType: "error",
                              processId: processId,
                              processName: flpConfigurationRequestDto.ProcessName,
                              tableName: configurationTableMappingDto.TableName,// flpConfigurationRequestDto.TableName,
                              totalRows: 0,
                              flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                              fileUploadedId: flpConfigurationRequestDto.UploadedFileId,
                              FileStatusActivityEnum.Error,
                              FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation
                         );
                    }
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { errorMessage },
                        Result = flpConvertToParquetResponseDto
                    });
                }


            }
            catch (Exception ex)
            {
                _logger.LogError($"No records found in flpConfigurations.{flpConfigurationRequestDto.FlpConfigurationId}", $"Error:{ex.Message}");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Something went wrong" },
                    Result = flpConvertToParquetResponseDto
                });
            }
        }

        private async Task<List<ConfigurationTableMappingDto>> GetMappingTableNameList(string flpConfigurationId, string? tabName, string fileType, bool isFirstSheet)
        {
            // Fetch the data based on the configuration ID
            var dbResult = await _fileLoadingProcessConfiguration.ConfigurationTableMapping(flpConfigurationId);

            // No need to map its already mapped , just filter
            var result = dbResult.Where(x =>
                             (fileType == "csv" || fileType == "txt") ||
                             ((fileType == "xlsx" || fileType == "xls") && !isFirstSheet && x.TabName == tabName) ||
                             ((fileType == "xlsx" || fileType == "xls") && isFirstSheet)).ToList();

            return result;
        }
        private async Task UpdateProcessStatus(FlpConfigurationResponseDto flpConfigurationRequestDto, APIResponse<FlpProcessResponseDto> apiResponse)
        {
            if (apiResponse.ResultStatus.Code == APIResultStatus.Completed.Code)
            {
                await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.UploadedFileId, APIResultStatus.Completed);
                await _fileLoadingProcessConfiguration.UpdateProcessSchedulerStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Completed);


            }
            else
            {
                await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.UploadedFileId, APIResultStatus.Error);
                await _fileLoadingProcessConfiguration.UpdateProcessSchedulerStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Error);

            }

        }
    }
}
