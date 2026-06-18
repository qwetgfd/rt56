using AngleSharp.Io;
using Azure;
using Azure.Storage.Blobs;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using static System.Net.WebRequestMethods;

namespace Teleperformance.DataIngestion.DataAccess.Services.v1._0
{
    public class FileLoadingProcessService : IFileLoadingProcessService
    {
      
        private readonly ILogger<FileLoadingProcessService> _logger;
        private readonly IFileLoadingProcessConfiguration _fileLoadingProcessConfiguration;
        private readonly IFileLoadingProcessRepository _ifileLoadingProcessRepository;
        private readonly IFileProcessingService _iIFileProcessingService;
        private readonly ICsvToParquetConverterService _iCsvToParquetConverterService;
        private readonly ITxtToParquetConverterService _iTxtToParquetConverterService;
        private readonly IExcelToParquetConverterService _iExcelToParquetConverterService;
        

        public FileLoadingProcessService(ILogger<FileLoadingProcessService> logger, IFileLoadingProcessConfiguration fileLoadingProcessConfiguration, ICsvToParquetConverterService iCsvToParquetConverterService,IFileLoadingProcessRepository ifileLoadingProcessRepository, IFileProcessingService iIFileProcessingService, ITxtToParquetConverterService iTxtToParquetConverterService, IExcelToParquetConverterService iExcelToParquetConverterService)
        {
            _logger = logger;
            _fileLoadingProcessConfiguration = fileLoadingProcessConfiguration;
            _iCsvToParquetConverterService = iCsvToParquetConverterService;
            _ifileLoadingProcessRepository = ifileLoadingProcessRepository;
            _iIFileProcessingService = iIFileProcessingService;
            _iTxtToParquetConverterService = iTxtToParquetConverterService;
            _iExcelToParquetConverterService = iExcelToParquetConverterService;
        }



        public async Task<APIResponse<FlpProcessResponseDto>> ProcessCsvFile(FlpRequestDto flpRequestDto)
        {
            var configurationResult = await _fileLoadingProcessConfiguration.GetFlpProcessByConfigurationId(flpRequestDto.FlpConfigurationId,"");
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
                else if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.SharePoint)
                {
                    #region SharePoint Workspace - AY
                    flpConfigurationRequestDto.SourcePath = flpRequestDto.SharePointFileLocation.FileUrl;
                    flpConfigurationRequestDto.UploadedFileId = flpRequestDto.SharePointFileLocation.UploadedId;

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
                    #endregion
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
                await UpdateProcessStatus(flpConfigurationRequestDto, res);

                if (!string.IsNullOrWhiteSpace(flpConfigurationRequestDto.UploadedFileId))
                {
                    int totalRows = res.Result?.TotalRows ?? 0;
                    int insertedRows = res.Result?.InsertedRows ?? 0;
                    int duplicateRows = res.Result?.DuplicateRows ?? 0;
                    string blobName = res.Result?.BlobName?? string.Empty;
                    
                    await _fileLoadingProcessConfiguration.AddBackUpFileDetails(flpConfigurationRequestDto.UploadedFileId, flpConfigurationRequestDto.FlpConfigurationId, res.Result?.BackUpFileName, null, totalRows, insertedRows, duplicateRows,blobName);
                }
                else
                {
                    _logger.LogError($"Not updated backup file details for {flpConfigurationRequestDto.FlpConfigurationId}");
                }
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


       
        public async Task<APIResponse<FlpProcessResponseDto>> ProcessTxtFile(FlpRequestDto flpRequestDto)
        {
          
            var configurationResult = await _fileLoadingProcessConfiguration.GetFlpProcessByConfigurationId(flpRequestDto.FlpConfigurationId,"");
          
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
                else if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.SharePoint)
                {
                    #region SharePoint Workspace - AY
                    flpConfigurationRequestDto.SourcePath = flpRequestDto.SharePointFileLocation.FileUrl;
                    flpConfigurationRequestDto.UploadedFileId = flpRequestDto.SharePointFileLocation.UploadedId;

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
                    #endregion
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
                await UpdateProcessStatus(flpConfigurationRequestDto, res);

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


        public async Task<APIResponse<FlpProcessResponseDto>> ProcessExcelFile(FlpRequestDto flpRequestDto)
        {
            var configurationResult = await _fileLoadingProcessConfiguration.GetFlpProcessByConfigurationId(flpRequestDto.FlpConfigurationId,"");
            if (configurationResult.ResponseCode == 200)
            {
                var flpConfigurationRequestDto = configurationResult.Result;
                DestinationStorageAccountDto destinationStorageAccountDto = null;
                SharedLocationDestinationServerDto sharedLocationDestinationServerDto = null;
                if ( flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.Azure)
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
                    if (!isValidFile1 && !isValidFile2)
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
                    if (!isValidFile1 && !isValidFile2)
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
                else if (flpConfigurationRequestDto.LocationTypeId == (int)SourceLocationTypeEnum.SharePoint)
                {
                    #region SharePoint Workspace - AY
                    flpConfigurationRequestDto.SourcePath = flpRequestDto.SharePointFileLocation.FileUrl;
                    flpConfigurationRequestDto.UploadedFileId = flpRequestDto.SharePointFileLocation.UploadedId;

                    var isValidFile1 = await _fileLoadingProcessConfiguration.IsValidFile(flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.SourcePath, ".xlsx");
                    var isValidFile2 = await _fileLoadingProcessConfiguration.IsValidFile(flpConfigurationRequestDto.FlpConfigurationId, flpConfigurationRequestDto.SourcePath, ".xls");
                    if (!isValidFile1 && !isValidFile2)
                    {
                        await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(flpConfigurationRequestDto.FlpConfigurationId, APIResultStatus.Error);
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { "Invalid file extention" },
                            Result = null
                        });
                    }
                    #endregion
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
                
                 await UpdateProcessStatus(flpConfigurationRequestDto, res);
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

           
            ConfigurationTableMappingDto configurationTableMappingDto = (await GetMappingTableNameList(flpConfigurationRequestDto?.FlpConfigurationId ?? string.Empty, null, fileType, false))
                                      .FirstOrDefault();



            

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
                flpConvertToParquetResponseDto.BlobName = flpProcessTempFile?.Name??"";
                FlpActivityLogStatusEnum currentStatus = FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation;

               
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
                          FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation);
                ParquetFileResponseDto resultResponse = null;
                //var csvToParquetStream = new FlpCsvToParquet(_activityLoggerRepository, _dataRepository, _ismbLibraryServices);
                if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                {
                    resultResponse = await _iCsvToParquetConverterService.ConvertDataToParquet(configurationTableMappingDto,flpConfigurationRequestDto, flpProcessTempFile);
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
                    resultResponse = await _iCsvToParquetConverterService.ConvertDataToParquetOnPremSharedLocation(flpProcessTempFile?.sourceTempFilePath ?? "", configurationTableMappingDto,flpConfigurationRequestDto, destinationServerModel);
                }
                if (resultResponse != null &&  resultResponse.ParquetFileCreated)
                {
                    var response = await _iIFileProcessingService.ParquetFileProcessToBronzeTable(processId, fileType, fileLocation,null, connectionString, currentStatus, flpProcessTempFile, resultResponse,configurationTableMappingDto, flpConfigurationRequestDto, destinationStorageAccountDto, slDestinationServerDto);
                    return response;
                }
                else
                {
                    string errorMessage = resultResponse?.ErrorMessage ?? "Something went wrong";
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
                _logger.LogError($"No records found in flpConfigurations.{flpConfigurationRequestDto.FlpConfigurationId}",$"Error:{ex.Message}");               
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Something went wrong" },
                    Result = flpConvertToParquetResponseDto
                });
            }
        }


        public async Task<APIResponse<FlpProcessResponseDto>> TxtProcess(FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto)
        {
            FlpProcessResponseDto flpConvertToParquetResponseDto = new FlpProcessResponseDto();
            string fileType = "txt";

            ConfigurationTableMappingDto configurationTableMappingDto = (await GetMappingTableNameList(flpConfigurationRequestDto?.FlpConfigurationId ?? string.Empty, null, fileType, false))
                                      .FirstOrDefault();

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
                          message: $"File conversion process in progress",
                          messageType: "info",
                          processId: processId,
                          processName: flpConfigurationRequestDto.ProcessName,
                          tableName: configurationTableMappingDto.TableName,//flpConfigurationRequestDto.TableName,
                          totalRows: 0,
                          flpConfigurationId: flpConfigurationRequestDto.FlpConfigurationId,
                          fileUploadedId: fileUploadedId,
                          FileStatusActivityEnum.Processing,
                          FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation);
                ParquetFileResponseDto resultResponse = null;
                //var csvToParquetStream = new FlpCsvToParquet(_activityLoggerRepository, _dataRepository, _ismbLibraryServices);
                if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                {
                    resultResponse = await _iTxtToParquetConverterService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);
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


        public async Task<APIResponse<FlpProcessResponseDto>> ExcelProcess(FlpConfigurationResponseDto flpConfigurationRequestDto, DestinationStorageAccountDto destinationStorageAccountDto, SharedLocationDestinationServerDto slDestinationServerDto)
        {
            FlpProcessResponseDto flpConvertToParquetResponseDto = new FlpProcessResponseDto();
            string fileType = "excel";

           
            ConfigurationTableMappingDto configurationTableMappingDto = (await GetMappingTableNameList(flpConfigurationRequestDto?.FlpConfigurationId ?? string.Empty, null, "xlsx", true))
                                      .FirstOrDefault();

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
                flpConvertToParquetResponseDto.BlobName = flpProcessTempFile?.Name ?? "";
                FlpActivityLogStatusEnum currentStatus = FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation;

                

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
                          FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation);
                ParquetFileResponseDto resultResponse = null;
                //var csvToParquetStream = new FlpCsvToParquet(_activityLoggerRepository, _dataRepository, _ismbLibraryServices);
                if (flpConfigurationRequestDto.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                {
                    resultResponse = await _iExcelToParquetConverterService.ConvertDataToParquet(configurationTableMappingDto, flpConfigurationRequestDto, flpProcessTempFile);
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
                    resultResponse = await _iExcelToParquetConverterService.ConvertDataToParquetOnPremSharedLocation(flpProcessTempFile?.sourceTempFilePath ?? "", configurationTableMappingDto, flpConfigurationRequestDto, destinationServerModel);
                }


                if (resultResponse != null && resultResponse.ParquetFileCreated)
                {
                    var response = await _iIFileProcessingService.ParquetFileProcessToBronzeTable(processId, fileType, fileLocation, null, connectionString, currentStatus, flpProcessTempFile, resultResponse, configurationTableMappingDto, flpConfigurationRequestDto, destinationStorageAccountDto, slDestinationServerDto);
                        return response;                    
                    
                }
                else
                {
                    string errorMessage = resultResponse?.ErrorMessage ?? "Something went wrong";
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

        public async Task<APIResponse<bool>> UpdateProcessSchedulerLastDate(string flpConfigurationId)
        {
            var dbResponse = await _fileLoadingProcessConfiguration.UpdateProcessSchedulerLastDate(flpConfigurationId);
            return await Task.FromResult(new APIResponse<bool>
            {
                ResultStatus = dbResponse?APIResultStatus.Completed:APIResultStatus.Error,
                ResponseMessage = new List<string> { $"Updated scheduler" },
                Result = dbResponse
            });
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
