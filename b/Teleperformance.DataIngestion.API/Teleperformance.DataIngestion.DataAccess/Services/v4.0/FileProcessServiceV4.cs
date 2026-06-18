using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0;
using Teleperformance.DataIngestion.Models.DTOs.v4._0;
using Teleperformance.DataIngestion.Models.DTOs.v4._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Enums.v4._0;
using Teleperformance.DataIngestion.Models.DTOs.v4._0.DataBricksAPIJobStatus;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;

namespace Teleperformance.DataIngestion.DataAccess.Services.v4._0
{
    public class FileProcessServiceV4: IFileProcessServiceV4
    {
        private readonly ILogger<FileProcessServiceV4> _logger;
        private readonly IFileLoadingProcessConfiguration _fileLoadingProcessConfiguration;
        private readonly ITextToParquetServiceV4 _iTxtToParquetConverterService;
        private readonly IExcelToParquetServiceV4 _iExcelToParquetService;
        private readonly ICsvToParquetServiceV4 _iCsvToParquetConverterService;
        private readonly IFlpProcessingServiceV4 _iIFileProcessingService;
        private readonly IValidateSchemaService _iValidateSchemaService;
        private readonly IDatabricksAPIDbRepository _databricksAPIDbREpository;
        public FileProcessServiceV4(ILogger<FileProcessServiceV4> logger, IFileLoadingProcessConfiguration fileLoadingProcessConfiguration, ITextToParquetServiceV4 iTxtToParquetConverterService, IFlpProcessingServiceV4 iIFileProcessingService, IValidateSchemaService iValidateSchemaService, ICsvToParquetServiceV4 iCsvToParquetConverterService, IExcelToParquetServiceV4 iExcelToParquetService, IDatabricksAPIDbRepository databricksAPIDbREpository)
        {
            _logger = logger;
            _fileLoadingProcessConfiguration = fileLoadingProcessConfiguration;
            _iTxtToParquetConverterService = iTxtToParquetConverterService;
            _iIFileProcessingService = iIFileProcessingService;
            _iValidateSchemaService = iValidateSchemaService;
            _iCsvToParquetConverterService = iCsvToParquetConverterService;
            _iExcelToParquetService = iExcelToParquetService;
            _databricksAPIDbREpository = databricksAPIDbREpository;
        }


        public async Task<APIResponse<FlpDatabricksProcessResponseDto>> ProcessExcelFile(FlpRequestDto flpRequestDto)
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
                DatabricksStorageAccountDto databricksStorageAccountDto = null;
                if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure)
                {

                    flpConfigurationRequestDto.BlobClients = flpRequestDto.BlobClients;
                    flpConfigurationRequestDto.UploadedFileId = flpRequestDto.BlobClients.UploadedId;
                    var blobClients = flpConfigurationRequestDto.BlobClients;
                    if (blobClients == null)
                    {

                        await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Error);
                        return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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
                        return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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
                        return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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
                databricksStorageAccountDto = await _fileLoadingProcessConfiguration.DatabricksStorageAccountInfo(flpConfigurationRequestDto.FlpConfigurationId);
                var res = await ExcelProcess(flpConfigurationRequestDto, destinationStorageAccountDto, sharedLocationDestinationServerDto, databricksStorageAccountDto);
              

                if (!string.IsNullOrWhiteSpace(flpConfigurationRequestDto.UploadedFileId))
                {
                    int totalRows = res.Result?.TotalRows ?? 0;
                    int insertedRows = res.Result?.InsertedRows ?? 0;
                    int duplicateRows = res.Result?.DuplicateRows ?? 0;
                    string blobName = res.Result?.BlobName ?? string.Empty;
                    string datalakeStorageAccountPath = res.Result?.DataLakeStorageAccountPath ?? string.Empty;
                    float fileSize = res.Result?.fileSize ?? 0;
                    string csvTempBlobName = res.Result?.csvTempBlobName ?? string.Empty;                    
                    await _fileLoadingProcessConfiguration.AddBackUpFileDetails(flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.FlpConfigurationId, res.Result?.BackUpFileName, null, totalRows, insertedRows, duplicateRows, blobName, datalakeStorageAccountPath,fileSize,csvTempBlobName);
                }
                else
                {
                    _logger.LogError($"Not updated backup file details for {flpConfigurationRequestDto.FlpConfigurationId}");
                }

                await UpdateProcessStatus(flpConfigurationRequestDto, res);
                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = res.ResultStatus,
                    ResponseMessage = res.ResponseMessage,
                    Result = res.Result
                });

            }
            else
            {
                await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpRequestDto.FlpConfigurationId, APIResultStatus.Error);
                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = configurationResult.ResultStatus,
                    ResponseMessage = new List<string> { $"Not found records for {flpRequestDto.FlpConfigurationId}" },
                    Result = null
                });
            }
        }

        public async Task<APIResponse<FlpDatabricksProcessResponseDto>> ExcelProcess(FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto, DatabricksStorageAccountDto databricksStorageAccountDto)
        {
            FlpDatabricksProcessResponseDto flpConvertToParquetResponseDto = new FlpDatabricksProcessResponseDto();
            string fileType = "excel";

            ConfigurationTableMappingDto configurationTableMappingDto = (await GetMappingTableNameList(flpConfigurationRequestDto?.FlpConfigurationId ?? string.Empty, null, "xlsx", false)).FirstOrDefault();

            if (configurationTableMappingDto == null)
            {

                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { $"Not found flpConfigurationlist for {flpConfigurationRequestDto.FlpConfigurationId} " },
                    Result = null
                });
            }

            

            //flpConfigurationRequestDto.TableName = tableName;
            string connectionString = "";// KeyVault.GetKeyVaultValue(configurationTableMappingDto.DatabaseConnectionSecret).Result;
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
                (FlpProcessTempFile? flpProcessTempFile, bool movedFileToTemp) = await _iIFileProcessingService.MoveSourceExcelFileToTemporaryDestinationAndDelete(processId, fileType, fileLocation, fileUploadedId, backupFileName, configurationTableMappingDto, flpConfigurationRequestDto, destinationStorageAccountDto, slDestinationServerDto);
                if (!movedFileToTemp)
                {
                    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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
                    // resultResponse = await _iExcelToParquetService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);

                    //flpConvertToParquetResponseDto.fileSize = (float)(flpProcessTempFile?.FileSize ?? 0);
                    //flpConvertToParquetResponseDto.csvTempBlobName = flpProcessTempFile?.CsvFile?.CsvName??"";

                    // resultResponse = await _iExcelToParquetService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);
                    //if (flpProcessTempFile?.FileSize > 50)
                    //{                        
                    //    resultResponse = await _iCsvToParquetConverterService.ConvertDataToParquetExcel(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);
                    //}
                    //else
                    //{
                    //    resultResponse = await _iExcelToParquetService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);
                    //}

                    if (flpConfigurationRequestDto.UIValidation || flpConfigurationRequestDto.BEValidation)
                    {
                        _logger.LogInformation($"Updating RowNo for  {flpConfigurationRequestDto.FlpConfigurationId} and uploadFileId {flpConfigurationRequestDto.UploadedFileId}, tabName {configurationTableMappingDto.TabName}");
                        //Call the API to update row no
                        var token = _iIFileProcessingService.GetBearerToken();
                        var skipRows = configurationTableMappingDto.SkipRows == 0 ? 1 : configurationTableMappingDto.SkipRows;
                        var validationRuleServiceHelper = new ValidationRuleServiceHelper(_logger);
                        var result = await validationRuleServiceHelper.AddExcelRowNo(flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.UploadedFileId, configurationTableMappingDto.TabName,
                             token, flpConfigurationRequestDto.DestinationPath, destinationStorageAccountDto.StorageContainerName, flpProcessTempFile?.Uri ?? "", destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.SasKey, skipRows, skipRows + 1, false,configurationTableMappingDto.IsHeaderProvided,configurationTableMappingDto.SkipEmptyLines);
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
                    }
                    else
                    {
                        resultResponse = await _iExcelToParquetService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);

                    }

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
                    var response = await _iIFileProcessingService.ParquetFileProcessToDataLake(processId, fileType, fileLocation, null, connectionString, currentStatus, flpProcessTempFile, resultResponse, configurationTableMappingDto, flpConfigurationRequestDto, destinationStorageAccountDto, slDestinationServerDto,databricksStorageAccountDto);
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
                    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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
                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Something went wrong" },
                    Result = flpConvertToParquetResponseDto
                });
            }
        }

        public async Task<APIResponse<FlpDatabricksProcessResponseDto>> ProcessCsvFile(FlpRequestDto flpRequestDto)
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
                DatabricksStorageAccountDto databricksStorageAccountDto = null;
                if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure)
                {

                    flpConfigurationRequestDto.BlobClients = flpRequestDto.BlobClients;
                    flpConfigurationRequestDto.UploadedFileId = flpRequestDto.BlobClients.UploadedId;
                    var blobClients = flpConfigurationRequestDto.BlobClients;
                    if (blobClients == null)
                    {

                        await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Error);
                        return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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
                        return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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
                        return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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
                databricksStorageAccountDto = await _fileLoadingProcessConfiguration.DatabricksStorageAccountInfo(flpConfigurationRequestDto.FlpConfigurationId);
                var res = await CsvProcess(flpConfigurationRequestDto, destinationStorageAccountDto, sharedLocationDestinationServerDto, databricksStorageAccountDto);
               

                if (!string.IsNullOrWhiteSpace(flpConfigurationRequestDto.UploadedFileId))
                {
                    int totalRows = res.Result?.TotalRows ?? 0;
                    int insertedRows = res.Result?.InsertedRows ?? 0;
                    int duplicateRows = res.Result?.DuplicateRows ?? 0;
                    string blobName = res.Result?.BlobName ?? string.Empty;
                    string datalakeStorageAccountPath = res.Result?.DataLakeStorageAccountPath ?? string.Empty;
                    float fileSize = res.Result?.fileSize ?? 0;
                    string csvTempBlobName = res.Result?.csvTempBlobName ?? string.Empty;
                    await _fileLoadingProcessConfiguration.AddBackUpFileDetails(flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.FlpConfigurationId, res.Result?.BackUpFileName, null, totalRows, insertedRows, duplicateRows, blobName, datalakeStorageAccountPath, fileSize, csvTempBlobName);
                }
                else
                {
                    _logger.LogError($"Not updated backup file details for {flpConfigurationRequestDto.FlpConfigurationId}");
                }
                await UpdateProcessStatus(flpConfigurationRequestDto, res);
                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = res.ResultStatus,
                    ResponseMessage = res.ResponseMessage,
                    Result = res.Result
                });


            }
            else
            {
                await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpRequestDto.FlpConfigurationId, APIResultStatus.Error);
                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = configurationResult.ResultStatus,
                    ResponseMessage = new List<string> { $"Not found records for {flpRequestDto.FlpConfigurationId}" },
                    Result = null
                });
            }
        }

        public async Task<APIResponse<FlpDatabricksProcessResponseDto>> CsvProcess(FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto, DatabricksStorageAccountDto databricksStorageAccountDto)
        {
            FlpDatabricksProcessResponseDto flpConvertToParquetResponseDto = new FlpDatabricksProcessResponseDto();
            string fileType = "csv";

            ConfigurationTableMappingDto configurationTableMappingDto = (await GetMappingTableNameList(flpConfigurationRequestDto?.FlpConfigurationId ?? string.Empty, null, fileType, false)).FirstOrDefault();

            if (configurationTableMappingDto == null)
            {

                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { $"Not found flpConfigurationlist for {flpConfigurationRequestDto.FlpConfigurationId} " },
                    Result = null
                });
            }

            //if (!FlpConfigurationHelper.ValidString(configurationTableMappingDto.DatabaseConnectionSecret))
            //{
            //    _logger.LogError($"Not found DatabaseConnectionSecret for {flpConfigurationRequestDto.FlpConfigurationId}");
            //    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
            //    {
            //        ResultStatus = APIResultStatus.InvalidParameters,
            //        ResponseMessage = new List<string> { $"Not found DatabaseConnectionSecret for {flpConfigurationRequestDto.FlpConfigurationId}" },
            //        Result = null
            //    });
            //}


            //flpConfigurationRequestDto.TableName = tableName;
            string connectionString = "";// KeyVault.GetKeyVaultValue(configurationTableMappingDto.DatabaseConnectionSecret).Result;
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
                    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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
                            resultResponse = await _iCsvToParquetConverterService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);
                        }
                    }
                    else
                    {
                        resultResponse = await _iCsvToParquetConverterService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);
                    }
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
                    var response = await _iIFileProcessingService.ParquetFileProcessToDataLake(processId, fileType, fileLocation, null, connectionString, currentStatus, flpProcessTempFile, resultResponse, configurationTableMappingDto, flpConfigurationRequestDto, destinationStorageAccountDto, slDestinationServerDto,databricksStorageAccountDto);
                    return response;
                }
                else
                {
                    string errorMessage = resultResponse?.ErrorMessage ?? "Something went wrong";
                    if (!addRowNo &&  resultResponse?.flpActivityLogStatusEnum != FlpActivityLogStatusEnum.FileSchemaValidated)
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
                    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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
                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Something went wrong" },
                    Result = flpConvertToParquetResponseDto
                });
            }
        }
        public async Task<APIResponse<FlpDatabricksProcessResponseDto>> ProcessTxtFile(FlpRequestDto flpRequestDto)
        {
            string uploadedFileId = FlpConfigurationHelper.GetUploadedFileId(flpRequestDto);
            
            if (string.IsNullOrWhiteSpace(uploadedFileId))
            {
                _logger.LogError($"Uploaded File Id is null for {flpRequestDto?.FlpConfigurationId??""}");
            }
            var configurationResult = await _fileLoadingProcessConfiguration.GetFlpProcessByConfigurationId(flpRequestDto.FlpConfigurationId, uploadedFileId);

            if (configurationResult.ResponseCode == 200)
            {

                var flpConfigurationRequestDto = configurationResult.Result;
                DestinationStorageAccountDto destinationStorageAccountDto = null;
                DatabricksStorageAccountDto databricksStorageAccountDto = null;
                SharedLocationDestinationServerDto sharedLocationDestinationServerDto = null;
                if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure)
                {

                    flpConfigurationRequestDto.BlobClients = flpRequestDto.BlobClients;
                    flpConfigurationRequestDto.UploadedFileId = flpRequestDto.BlobClients.UploadedId;
                    var blobClients = flpConfigurationRequestDto.BlobClients;
                    if (blobClients == null)
                    {

                        await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Error);
                        return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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
                        return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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
                        return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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

                databricksStorageAccountDto = await _fileLoadingProcessConfiguration.DatabricksStorageAccountInfo(flpConfigurationRequestDto.FlpConfigurationId);

                var res = await TxtProcess(flpConfigurationRequestDto, destinationStorageAccountDto, sharedLocationDestinationServerDto,databricksStorageAccountDto);
                

                if (!string.IsNullOrWhiteSpace(flpConfigurationRequestDto.UploadedFileId))
                {
                    int totalRows = res.Result?.TotalRows ?? 0;
                    int insertedRows = res.Result?.InsertedRows ?? 0;
                    int duplicateRows = res.Result?.DuplicateRows ?? 0;
                    string blobName = res.Result?.BlobName ?? string.Empty;
                    string datalakeStorageAccountPath = res.Result?.DataLakeStorageAccountPath??string.Empty;
                    float fileSize = res.Result?.fileSize ?? 0;
                    string csvTempBlobName = res.Result?.csvTempBlobName ?? string.Empty;
                    await _fileLoadingProcessConfiguration.AddBackUpFileDetails(flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.FlpConfigurationId, res.Result?.BackUpFileName, null, totalRows, insertedRows, duplicateRows, blobName, datalakeStorageAccountPath, fileSize, csvTempBlobName);
                }
                else
                {
                    _logger.LogError($"Not updated backup file details for {flpConfigurationRequestDto.FlpConfigurationId}");
                }

                await UpdateProcessStatus(flpConfigurationRequestDto, res);
                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = res.ResultStatus,
                    ResponseMessage = res.ResponseMessage,
                    Result = res.Result
                });

            }
            else
            {
                await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpRequestDto.FlpConfigurationId, APIResultStatus.Error);
                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = configurationResult.ResultStatus,
                    ResponseMessage = new List<string> { $"Not found records for {flpRequestDto.FlpConfigurationId}" },
                    Result = null
                });
            }
        }

        public async Task<APIResponse<FlpDatabricksProcessResponseDto>> TxtProcess(FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto, DatabricksStorageAccountDto databricksStorageAccountDto)
        {
            FlpDatabricksProcessResponseDto flpConvertToParquetResponseDto = new FlpDatabricksProcessResponseDto();
            string fileType = "txt";

            ConfigurationTableMappingDto configurationTableMappingDto = (await GetMappingTableNameList(flpConfigurationRequestDto?.FlpConfigurationId ?? string.Empty, null, fileType, false)).FirstOrDefault();

            if (configurationTableMappingDto == null)
            {

                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { $"Not found flpConfigurationlist for {flpConfigurationRequestDto.FlpConfigurationId} " },
                    Result = null
                });
            }

            //if (!FlpConfigurationHelper.ValidString(configurationTableMappingDto.DatabaseConnectionSecret))
            //{
            //    _logger.LogError($"Not found DatabaseConnectionSecret for {flpConfigurationRequestDto.FlpConfigurationId}");
            //    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
            //    {
            //        ResultStatus = APIResultStatus.InvalidParameters,
            //        ResponseMessage = new List<string> { $"Not found DatabaseConnectionSecret for {flpConfigurationRequestDto.FlpConfigurationId}" },
            //        Result = null
            //    });
            //}


            //flpConfigurationRequestDto.TableName = tableName;
            string connectionString = "";// KeyVault.GetKeyVaultValue(configurationTableMappingDto.DatabaseConnectionSecret).Result;
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
                    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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
                   // resultResponse = await _iTxtToParquetConverterService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);
                    if (flpConfigurationRequestDto.UIValidation || flpConfigurationRequestDto.BEValidation)
                    {
                        _logger.LogInformation($"Updating RowNo for  {flpConfigurationRequestDto.FlpConfigurationId} and uploadFileId {flpConfigurationRequestDto.UploadedFileId}, tabName {configurationTableMappingDto.TabName}");
                        //Call the API to update row no
                        var token = _iIFileProcessingService.GetBearerToken();
                        var skipRows = configurationTableMappingDto.SkipRows == 0 ? 1 : configurationTableMappingDto.SkipRows;
                        var validationRuleServiceHelper = new ValidationRuleServiceHelper(_logger);
                        var result = await validationRuleServiceHelper.AddTextRowNo(flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.UploadedFileId, configurationTableMappingDto.Delimiter,
                             token, flpConfigurationRequestDto.DestinationPath, destinationStorageAccountDto.StorageContainerName, flpProcessTempFile?.Uri ?? "", destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.SasKey, skipRows, skipRows + 1, false,configurationTableMappingDto.IsHeaderProvided, configurationTableMappingDto.SkipEmptyLines);
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
                            resultResponse = await _iTxtToParquetConverterService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);
                        }
                    }
                    else
                    {
                        resultResponse = await _iTxtToParquetConverterService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);
                    }
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
                    await  _iIFileProcessingService.AddFileProcessLosStatus(
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
                    var response = await _iIFileProcessingService.ParquetFileProcessToDataLake(processId, fileType, fileLocation, null, connectionString, currentStatus, flpProcessTempFile, resultResponse, configurationTableMappingDto, flpConfigurationRequestDto, destinationStorageAccountDto, slDestinationServerDto, databricksStorageAccountDto);
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
                    return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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
                return await Task.FromResult(new APIResponse<FlpDatabricksProcessResponseDto>
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
        private async Task UpdateProcessStatus(FlpConfigurationResponseDto flpConfigurationRequestDto, APIResponse<FlpDatabricksProcessResponseDto> apiResponse)
        {
            var result = apiResponse.Result;
            FlpProcessLogHistoryStatusDto processLogHistoryStatus = null;
            _logger.LogInformation($"UpdateProcessStatus line no 873: {flpConfigurationRequestDto.FlpConfigurationId} - {apiResponse.ResultStatus.Code} - {apiResponse.ResponseMessage?.FirstOrDefault()??""}");
            if (apiResponse.ResultStatus.Code == APIResultStatus.Completed.Code)
            {
                
                _logger.LogInformation($"UpdateProcessStatus line no 876: {flpConfigurationRequestDto.FlpConfigurationId} - {apiResponse.ResultStatus.Code} - {apiResponse.ResponseMessage?.FirstOrDefault() ?? ""}");
                if (result.LifeCycleStateId == (int)LifeCycleStateEnum.TERMINATED && result.ResultStateId == (int)ResultStateEnum.SUCCESS)
                {
                    //Update runid with success status
                    processLogHistoryStatus = new FlpProcessLogHistoryStatusDto()
                    {
                        FlpProcessStatusId = (int)FlpProcessStatusEnum.Processed,
                        FileUploadedId = flpConfigurationRequestDto.UploadedFileId,
                        FlpConfigurationId = flpConfigurationRequestDto.FlpConfigurationId,
                        DatabricksStageId = result.JobStatusId,
                        RunId = result.RunId,
                        TerminationDetailsId = result.TerminationStatusId,
                        LifeCycleStateId = result.LifeCycleStateId,   
                        ResultStateId = result.ResultStateId,   
                        SkipUpdateHistoryStatus = true

                    };
                    await _databricksAPIDbREpository.UpdateLogHistoryStatus(processLogHistoryStatus);
                    await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.UploadedFileId, APIResultStatus.Completed);
                }
                else
                {
                    processLogHistoryStatus = new FlpProcessLogHistoryStatusDto()
                    {
                        FlpProcessStatusId = (int)FlpProcessStatusEnum.NotProcessed,
                        FileUploadedId = flpConfigurationRequestDto.UploadedFileId,
                        FlpConfigurationId = flpConfigurationRequestDto.FlpConfigurationId,
                        DatabricksStageId = result.JobStatusId,
                        RunId = result.RunId,
                        TerminationDetailsId = result.TerminationStatusId,
                        LifeCycleStateId = result.LifeCycleStateId,
                        ResultStateId = result.ResultStateId,
                        SkipUpdateHistoryStatus = true

                    };
                    await _databricksAPIDbREpository.UpdateLogHistoryStatus(processLogHistoryStatus);
                }
               await _fileLoadingProcessConfiguration.UpdateProcessSchedulerStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Completed);
            }
            else
            {                
                //Update runid with success status
                processLogHistoryStatus = new FlpProcessLogHistoryStatusDto()
                {
                    FlpProcessStatusId = (int)FlpProcessStatusEnum.Error,
                    FileUploadedId = flpConfigurationRequestDto.UploadedFileId,
                    FlpConfigurationId = flpConfigurationRequestDto.FlpConfigurationId,
                    DatabricksStageId = result?.JobStatusId,
                    RunId = result?.RunId,
                    TerminationDetailsId = result?.TerminationStatusId,
                    LifeCycleStateId = result?.LifeCycleStateId,
                    ResultStateId = result?.ResultStateId,
                    SkipUpdateHistoryStatus = true
                };
                await _databricksAPIDbREpository.UpdateLogHistoryStatus(processLogHistoryStatus);
                await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.UploadedFileId, APIResultStatus.Error);
                await _fileLoadingProcessConfiguration.UpdateProcessSchedulerStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Error);

            }

        }
    }
}
