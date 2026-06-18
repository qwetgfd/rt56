using AngleSharp.Dom;
using Azure.Storage.Blobs;
using CsvHelper;
using DocumentFormat.OpenXml.Math;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SixLabors.ImageSharp.ColorSpaces;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.Common.Crypto;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService;
using Teleperformance.DataIngestion.Models.DTOs.v4._0;
using Teleperformance.DataIngestion.Models.DTOs.v4._0.DataBricksAPIJobStatus;

using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.ValidateFileRules;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v4._1;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Enums.v3._0;
using Teleperformance.DataIngestion.Models.Enums.v4._0;
using Teleperformance.DataIngestion.Models.Enums.v4._1;
using Teleperformance.DataIngestion.Models.Models.v4._1;
using static Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus.StatusResponseDto;
using ConfigurationTableMappingDto = Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess.ConfigurationTableMappingDto;

namespace Teleperformance.DataIngestion.DataAccess.Services.v4._1
{
    public class FileProcessServiceV4_1: IFileProcessServiceV4_1
    {

        private readonly ILogger<FileProcessServiceV4_1> _logger;
        private readonly IProcessConfigurationServiceV4_1 _processConfigurationService;
        private readonly IFileLoadingProcessConfigurationServiceV4_1 _fileLoadingProcessConfigurationService;
        private readonly IFlpProcessingServiceV4_1 _flpProcessingService;
        private readonly IExcelToParquetServiceV4_1 _excelToParquetService;
        private readonly IDatabricksDbRepository _databricksDbRepository;
        private readonly ILandingLayerService _landingLayerService;


        public FileProcessServiceV4_1(ILogger<FileProcessServiceV4_1> logger, 
            IProcessConfigurationServiceV4_1 processConfigurationService, 
            IFileLoadingProcessConfigurationServiceV4_1 fileLoadingProcessConfigurationService
            , IFlpProcessingServiceV4_1 flpProcessingService,IExcelToParquetServiceV4_1 excelToParquetService,IDatabricksDbRepository databricksDbRepository, ILandingLayerService landingLayerService)
        {
            _logger = logger;
            _processConfigurationService = processConfigurationService;
            _fileLoadingProcessConfigurationService = fileLoadingProcessConfigurationService;
            _flpProcessingService = flpProcessingService;
            _excelToParquetService = excelToParquetService;
            _databricksDbRepository = databricksDbRepository;
            _landingLayerService = landingLayerService;
        }

        public async Task<APIResponse<FlpProcessResponseDto>> ProcessExcelFile(FlpRequestDto4_1 flpRequestDto)
        {

            FlpProcessResponseDto flpProcessResponseDto = new FlpProcessResponseDto();
            //List<FLPConfigurationModel> flpConfigurationList = new List<FLPConfigurationModel>();
            FLPConfigurationModel flpConfigurationModel = new FLPConfigurationModel();
            DestinationStorageAccountDtoV4_1 destinationStorage = null;
            SharedLocationDestinationServerDtoV4_1 sharedDestLocationStorage = null;
            string flpConfigurationId = flpRequestDto?.FlpConfigurationId??string.Empty;
            bool isValidFile = false;
            if (string.IsNullOrWhiteSpace(flpConfigurationId))
            {
                _logger.LogError($"Multisheet Error:flpConfigurationId is NULL");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { $"flpConfigurationId is NULL" },
                    Result = null
                });
            }
            string uploadedFileId = FlpConfigurationHelperV4_1.GetUploadedFileId(flpRequestDto);
            if(string.IsNullOrWhiteSpace(uploadedFileId))
            {
                _logger.LogError($"Multisheet Error:Uploaded File Id is null for {flpRequestDto?.FlpConfigurationId ?? ""}");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { $"flpConfigurationId is NULL" },
                    Result = null
                });
            }
            
            //if (string.IsNullOrWhiteSpace(uploadedFileId))
            //{
            //    _logger.LogError($"Uploaded File Id is null for {flpRequestDto?.FlpConfigurationId ?? ""}");
            //}
            //return null;
            var result = await _processConfigurationService.GetMultisheetConfigurationById(flpRequestDto?.FlpConfigurationId??"",uploadedFileId);
            if (result.ResultStatus.Code == APIResultStatus.Completed.Code)
            {
                // flpConfigurationList = result.ResponseDetails
                flpConfigurationModel = result.Result;
                var processingSetting = flpConfigurationModel.processSettings;
                FlpConfigurationResponseDtoV4_1 flpConfigurationRequestDto = new FlpConfigurationResponseDtoV4_1();
                
                if (processingSetting.locationTypeId == (int)SourceLocationTypeEnum.Azure)
                {

                   
                    if (flpRequestDto?.BlobClients == null)
                    {
                        _logger.LogError($"Multisheet Error:Not found BlobClients  for flpConfigurationId : {flpConfigurationId} and uploadedFileId {uploadedFileId}");
                        await _fileLoadingProcessConfigurationService.UpdateFlpProcessStatus(uploadedFileId, APIResultStatus.Error);
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { "Not found blob client details" },
                            Result = null
                        });
                    }
                    flpConfigurationRequestDto.BlobClients = flpRequestDto.BlobClients;
                    flpConfigurationRequestDto.UploadedFileId = flpRequestDto.BlobClients.UploadedId;
                    var blobClients = flpConfigurationRequestDto.BlobClients;
                    isValidFile = FileValidator.IsValidExcelFile(blobClients.Name);

                    
                }
                if (processingSetting.locationTypeId == (int)SourceLocationTypeEnum.OnPrem)
                {
                   
                    if (string.IsNullOrWhiteSpace(flpRequestDto?.OnPremFileLocation?.FileUrl))
                    {
                        _logger.LogError($"Multisheet Error:Not found onPremFileLocation file URL for flpConfigurationId : {flpConfigurationId} and uploadedFileId {uploadedFileId}");
                        await _fileLoadingProcessConfigurationService.UpdateFlpProcessStatus(uploadedFileId, APIResultStatus.Error);
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { $"Not found onPremFileLocation file URL for flpConfigurationId : {flpConfigurationId} and uploadedFileId {uploadedFileId}" },
                            Result = null
                        });
                    }
                    flpConfigurationRequestDto.SourcePath = flpRequestDto.OnPremFileLocation.FileUrl;
                    flpConfigurationRequestDto.UploadedFileId = flpRequestDto.OnPremFileLocation.UploadedId;
                    isValidFile = FileValidator.IsValidExcelFile(flpRequestDto.OnPremFileLocation.FileUrl);

                   
                }


                if (!isValidFile)
                {
                    await _fileLoadingProcessConfigurationService.UpdateFlpProcessStatus(uploadedFileId, APIResultStatus.Error);
                    _logger.LogError($"Multisheet Error:Invalid File extention for flpConfigurationId : {flpConfigurationId} and uploadedFileId {uploadedFileId}");
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid File extention" },
                        Result = null
                    });
                }

                var configResponse = await _fileLoadingProcessConfigurationService.GetMultisheetConfiguration(flpConfigurationId, uploadedFileId, "");
                if (configResponse.ResponseCode == APIResultStatus.Completed.Code)
                {

                    flpConfigurationRequestDto = configResponse.Result;                   
                    //Source storage account 
                    if (Convert.ToInt32(processingSetting.destinationLocationTypeId) == (int)DestinationLocationTypeEnum.Azure)
                    {
                        flpConfigurationRequestDto.BlobClients = flpRequestDto?.BlobClients;
                        flpConfigurationRequestDto.UploadedFileId = uploadedFileId;
                        destinationStorage = await _fileLoadingProcessConfigurationService.DestinationstorageAccountInfo(flpConfigurationId);
                        if(destinationStorage == null)
                        {
                            return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                            {
                                ResultStatus = APIResultStatus.InvalidParameters,
                                ResponseMessage = new List<string> { "Invalid File extention" },
                                Result = null
                            });
                        }
                    }


                    //if (Convert.ToInt32(processingSetting.destinationLocationTypeId) == (int)DestinationLocationTypeEnum.OnPrem)
                    //{
                    //    sharedDestLocationStorage = await _fileLoadingProcessConfigurationServiceV4_1.SharedLocationDestinationServerDetails(flpConfigurationId);
                    //}
                    if(flpConfigurationModel.fileSettings == null && !flpConfigurationModel.fileSettings.Any())
                    {
                        _logger.LogError($"Multisheet Error:Not found fileSetting details for flpConfigurationId {flpConfigurationId} and uploadedFileId {uploadedFileId}");
                        await _fileLoadingProcessConfigurationService.UpdateFlpProcessStatus(uploadedFileId, APIResultStatus.Error);
                        
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.InvalidParameters,
                            ResponseMessage = new List<string> { $"Not found fileSetting details for flpConfigurationId {flpConfigurationId} and uploadedFileId {uploadedFileId}" },
                            Result = null
                        });
                      
                    }
                    List<FileSettings> fileSettings = flpConfigurationModel.fileSettings.ToList();

                   
                    
                    (FlpProcessResponseDto processResponseDto, FlpProcessTempFileModel tempFileModel) = await MovedFileInTempLocation(fileSettings,flpConfigurationRequestDto, destinationStorage, sharedDestLocationStorage);
                    if(processResponseDto == null && tempFileModel == null)
                    {
                        _logger.LogError($"Multisheet Error:File not moved found, processResponseDto &  tempFileModel for flpConfigurationId {flpConfigurationId} and uploadedFileId {uploadedFileId}");
                        await _fileLoadingProcessConfigurationService.UpdateFlpProcessStatus(uploadedFileId, APIResultStatus.Error);

                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { $"File not moved found, processResponseDto &  tempFileModel for flpConfigurationId  {flpConfigurationId} and uploadedFileId {uploadedFileId}" },
                            Result = null
                        });
                    }
                    if (processingSetting.dataSource == (int)FileProcessingServerType.SQLServer)
                    {
                        var res = await RunExcelProcessesInParallelAsync(flpConfigurationId, uploadedFileId, flpConfigurationModel,destinationStorage,sharedDestLocationStorage, tempFileModel, flpConfigurationRequestDto);
                      
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = res.ResultStatus,
                            ResponseMessage = res.ResponseMessage,
                            Result = res.Result
                        });
                    }
                    else
                    {

                        var res = await RunDatabricksExcelProcessesInParallelAsync(flpConfigurationId, uploadedFileId, flpConfigurationModel,
                                                       destinationStorage, sharedDestLocationStorage, tempFileModel,flpConfigurationRequestDto);

                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = res.ResultStatus,
                            ResponseMessage = res.ResponseMessage,
                            Result = res.Result
                        });
                    }                  

                   

                }               
               
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { $"Not found configuration details for {flpConfigurationId}" },
                    Result = flpProcessResponseDto
                });
            }
            else
            {
                _logger.LogError($"Multisheet Error:Not found configuration details for {flpConfigurationId} and uploadedFileId {uploadedFileId}");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { $"Not found configuration details for {flpConfigurationId}" },
                    Result = null
                });
            }
               
          
        }


        public async Task<APIResponse<FlpProcessResponseDto>> ProcessLandingLayerFile(FlpRequestDto4_1 flpRequestDto)
        {
            string flpConfigurationId = flpRequestDto?.FlpConfigurationId ?? string.Empty;

            if (string.IsNullOrWhiteSpace(flpConfigurationId))
            {
                _logger.LogError("ProcessLandingLayerFile Error: flpConfigurationId is NULL");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "flpConfigurationId is NULL" },
                    Result = null
                });
            }
            string uploadedFileId = flpRequestDto?.LandingLayerUploadedId??"";
            if (string.IsNullOrWhiteSpace(uploadedFileId))
            {
                _logger.LogError($"ProcessLandingLayerFile Error: Uploaded File Id is null for {flpConfigurationId}");
                await _fileLoadingProcessConfigurationService.UpdateFlpProcessStatus(uploadedFileId, APIResultStatus.Error);
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Uploaded File Id is NULL" },
                    Result = null
                });
            }

            try
            {
                // Check the source location type and call appropriate method
                //Not in use flpRequestDto.SourceLocationTypeId == (int)SourceLocationTypeEnum.SFTP
                if (flpRequestDto.SourceLocationTypeId == (int)SourceLocationTypeEnum.OnPrem)
                {
                    // Call remote to landing layer processing
                    _logger.LogInformation($"Processing file from remote to landing layer for flpConfigurationId: {flpConfigurationId}");
                    var result = await _landingLayerService.ProcessFileFromRemoteToLandingLayer(flpRequestDto);
                    if (result != null && result.ResultStatus.Code == (int)APIResultStatus.Completed.Code)
                    {
                        await _fileLoadingProcessConfigurationService.UpdateFlpProcessStatus(uploadedFileId,APIResultStatus.Completed);
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.Completed,
                            ResponseMessage = new List<string> { "Process completed successfully" },
                            Result = null
                        });
                    }
                    else
                    {
                        await _fileLoadingProcessConfigurationService.UpdateFlpProcessStatus(uploadedFileId, APIResultStatus.Error);
                        _logger.LogError($"ProcessLandingLayerFile Error: Failed to process file from blob to landing layer for flpConfigurationId: {flpConfigurationId}");
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { "Failed to process file from blob to landing layer" },
                            Result = null
                        });
                    }

                }
                else if (flpRequestDto.SourceLocationTypeId == (int)SourceLocationTypeEnum.Azure)
                {
                    // Call blob to landing layer processing
                    _logger.LogInformation($"Processing file from blob to landing layer for flpConfigurationId: {flpConfigurationId}");
                    var result = await _landingLayerService.ProcessFileFromBlobToLandingLayer(flpRequestDto);
                    if(result !=null && result.ResultStatus.Code == (int)APIResultStatus.Completed.Code)
                    {
                        
                        await _fileLoadingProcessConfigurationService.UpdateFlpProcessStatus(uploadedFileId, APIResultStatus.Completed);
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.Completed,
                            ResponseMessage = new List<string> { "Process completed successfully" },
                            Result = null
                        });
                    }
                    else
                    {
                        await _fileLoadingProcessConfigurationService.UpdateFlpProcessStatus(uploadedFileId, APIResultStatus.Error);
                        _logger.LogError($"ProcessLandingLayerFile Error: Failed to process file from blob to landing layer for flpConfigurationId: {flpConfigurationId}");
                        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { "Failed to process file from blob to landing layer" },
                            Result = null
                        });
                    }
                }
                else
                {
                    // Unsupported source location type
                    string errorMessage = $"Unsupported source location type: {flpRequestDto.SourceLocationTypeId} for flpConfigurationId: {flpConfigurationId}";
                    _logger.LogError($"ProcessLandingLayerFile Error: {errorMessage}");
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { errorMessage },
                        Result = null
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ProcessLandingLayerFile Error: Exception occurred for flpConfigurationId: {flpConfigurationId}");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "An error occurred while processing the landing layer file" },
                    Result = null
                });
            }
        }

        public async Task<APIResponse<FlpProcessResponseDto>> MoveUploadFilesToLayerFile(List<IFormFile> files, string processName, string flpConfigurationId, string uploadFileId, string loginId)
        {
           

            if (string.IsNullOrWhiteSpace(flpConfigurationId))
            {
                _logger.LogError("ProcessLandingLayerFile Error: flpConfigurationId is NULL");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "flpConfigurationId is NULL" },
                    Result = null
                });
            }
            
            if (string.IsNullOrWhiteSpace(uploadFileId))
            {
                _logger.LogError($"ProcessLandingLayerFile Error: Uploaded File Id is null for {flpConfigurationId}");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Uploaded File Id is NULL" },
                    Result = null
                });
            }

            try
            {
                (bool ret , string  message)= await _landingLayerService.MoveFileInLandingLayerFolder(files,processName,flpConfigurationId,uploadFileId,loginId);
                if (ret)
                {
                    await _fileLoadingProcessConfigurationService.UpdateFlpProcessStatus(uploadFileId, APIResultStatus.Completed);
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Completed,
                        ResponseMessage = new List<string> { "Process completed successfully" },
                        Result = null
                    });
                }
                else
                {
                    await _fileLoadingProcessConfigurationService.UpdateFlpProcessStatus(uploadFileId, APIResultStatus.Error);
                    _logger.LogError($"ProcessLandingLayerFile Error: Failed to process file from blob to landing layer for flpConfigurationId: {flpConfigurationId}");
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { "Failed to process file from blob to landing layer" },
                        Result = null
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ProcessLandingLayerFile Error: Exception occurred for flpConfigurationId: {flpConfigurationId}");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "An error occurred while processing the landing layer file" },
                    Result = null
                });
            }
        }

        /// <summary>
        /// To moving file into temporary location for destination source sql server & databricks 
        /// </summary>
        /// <param name="fileSettings"></param>
        /// <param name="flpConfig"></param>
        /// <param name="destStorage"></param>
        /// <param name="sharedDestServer"></param>
        /// <returns></returns>
        private async Task<(FlpProcessResponseDto, FlpProcessTempFileModel)> MovedFileInTempLocation(List<FileSettings> fileSettings, FlpConfigurationResponseDtoV4_1 flpConfig,
                                                                                                       DestinationStorageAccountDtoV4_1 destStorage,
                                                                                                       SharedLocationDestinationServerDtoV4_1? sharedDestServer)
        {
            var responseDto = new FlpProcessResponseDto();
            const string fileType = "excel";
            long processId = long.Parse(DateTime.UtcNow.ToString("yyyyMMddHHmmss"));




            try
            {
                string fileLocation = FlpConfigurationHelperV4_1.GetFileLocation(flpConfig);
                string fileUploadedId;
                string backupFileName;

                if (flpConfig.LocationTypeId == (int)SourceLocationTypeEnum.Azure)
                {
                    fileUploadedId = flpConfig.BlobClients?.UploadedId ?? string.Empty;
                    backupFileName = FlpConfigurationHelper.GetBackUpFileName(flpConfig.BlobClients?.Name, processId.ToString());
                }
                else
                {
                    fileUploadedId = flpConfig.UploadedFileId ?? string.Empty;
                    backupFileName = FlpConfigurationHelper.GetBackUpFileName(flpConfig.SourcePath, processId.ToString());
                }
                responseDto.BackUpFileName = backupFileName;

                var fileSettingList = fileSettings.ToList();
                var (tempFileModel, movedToTemp) = await _flpProcessingService.MoveSourceExcelFileToTemporaryDestinationAndDelete(processId, fileType, fileLocation, fileUploadedId, backupFileName,
                                                                                                                    flpConfig, destStorage, sharedDestServer, fileSettingList);

                if (!movedToTemp)
                {
                    return (null, null);
                }


                return (responseDto, tempFileModel);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Multisheet Error: processing flpConfigurationId {flpConfig.FlpConfigurationId}: {ex.Message}");
                return (null, null);
            }
        }


        public async Task<APIResponse<FlpProcessResponseDto>> RunExcelProcessesInParallelAsync(  string flpConfigurationId, string uploadedFileId, FLPConfigurationModel fLPConfigurationModel,
                                                        DestinationStorageAccountDtoV4_1 destStorage, SharedLocationDestinationServerDtoV4_1? sharedDestServer, FlpProcessTempFileModel flpProcessTempFileModel, FlpConfigurationResponseDtoV4_1 flpConfigurationResponseDto)
        {
            FlpProcessResponseDto flpConvertToParquetResponseDto = new FlpProcessResponseDto();
            List<FlpProcessTabResponseDto> tabResList = new List<FlpProcessTabResponseDto>();
            string fileType = "excel";

            try
            {
                List<FileSettings> fileSettingsList = fLPConfigurationModel.fileSettings.ToList();
                if (!fileSettingsList.Any())
                {
                    _logger.LogError($"Multisheet Error:Not found fileSetting details for flpConfigurationId {flpConfigurationId} and uploadedFileId {uploadedFileId}");
                    return new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { "Something went wrong during parallel processing." },
                        Result = flpConvertToParquetResponseDto
                    };
                }
                foreach (var fileSetting in fileSettingsList)
                {
                    //if (!fileSetting.ignoreSheet)
                    {
                        FlpProcessTabResponseDto flpProcessTabResponseDto = new FlpProcessTabResponseDto();
                        var res = await ExcelProcessForSqlServer(flpConfigurationId, uploadedFileId, fLPConfigurationModel, destStorage, sharedDestServer, flpProcessTempFileModel, flpConfigurationResponseDto, fileSetting);
                        flpProcessTabResponseDto = res.Result;
                        tabResList.Add(flpProcessTabResponseDto);
                        if(flpProcessTabResponseDto !=null && flpProcessTabResponseDto.FileProcessCompleted)
                        {
                            await LogFileStatus(flpProcessTabResponseDto, fileType, flpConfigurationResponseDto.ProcessName, flpConfigurationId, uploadedFileId);
                            await LogFileStatus(flpProcessTabResponseDto, fileType, flpConfigurationResponseDto.ProcessName, flpConfigurationId, uploadedFileId, FileStatusActivityEnum.ProcessCompleted, "Temp file is deleted successfully");

                        }

                    }
                   
                }

                //var tasks = fileSettingsList.Select(async fileSetting =>
                //{
                //    try
                //    {
                //        var res = await ExcelProcess(flpConfigurationId, uploadedFileId, fLPConfigurationModel, destStorage, sharedDestServer, flpProcessTempFileModel, flpConfigurationResponseDto, fileSetting);
                //        return res?.Result ?? new FlpProcessTabResponseDto(); // Handle null or failed response gracefully
                //    }
                //    catch
                //    {
                //        return new FlpProcessTabResponseDto();
                //    }
                //}).ToList();

                //var results = await Task.WhenAll(tasks);
                //tabResList.AddRange(results);

                // Check file processing status
                bool allProcessed = tabResList.All(x => x.FileProcessCompleted);
                bool anyErrorOccurred = tabResList.Any(x => !x.FileProcessCompleted);
                bool anyProcessed = tabResList.Any(x => x.FileProcessCompleted);

                if (allProcessed)
                {
                    // Attempt to delete temp file
                    (bool tempFileDeleted, string tempFileResponse) = await _flpProcessingService.DeleteTempFileFromTempLocation(
                        flpConfigurationResponseDto.DestinationLocationTypeId ?? 0,fileType, flpConfigurationResponseDto.ProcessName,flpConfigurationId,
                        uploadedFileId, flpProcessTempFileModel,tabResList);

                    if (!tempFileDeleted)
                    {
                        return new APIResponse<FlpProcessResponseDto>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { tempFileResponse },
                            Result = null
                        };
                    }
                    // Update status based on deletion result
                   // var status = tempFileDeleted ? FlpProcessStatusEnum.Processed : FlpProcessStatusEnum.Error;
                 
                    var status = FlpProcessStatusEnum.Processed;
                    await _fileLoadingProcessConfigurationService.UpdateProcessStatus(uploadedFileId, (int)status);
                }                
                else
                {

                    //foreach (var tab in tabResList)
                    //{
                    //    if (!tab.FileProcessCompleted)
                    //    {
                    //        continue; // Skip if the file process was not completed for this tab
                    //    }
                    //    await LogFileStatus(tab, fileType, flpConfigurationResponseDto.ProcessName, flpConfigurationId, uploadedFileId);

                    //    //var status = tab.FileProcessCompleted
                    //    //    ? FileStatusActivityEnum.ProcessCompleted
                    //    //    : FileStatusActivityEnum.Error;                      

                    //    await LogFileStatus(tab, fileType, flpConfigurationResponseDto.ProcessName, flpConfigurationId, uploadedFileId, FileStatusActivityEnum.ProcessCompleted, "Temp file is deleted successfully");
                    //}

                    var finalStatus = (anyProcessed && anyErrorOccurred) ? FlpProcessStatusEnum.PartiallyCompleted: FlpProcessStatusEnum.Error;

                    await _fileLoadingProcessConfigurationService.UpdateProcessStatus(uploadedFileId, (int)finalStatus);

                    //foreach (var tab in tabResList.ToList())
                    //{
                    //    await _flpProcessingService.AddFileProcessLosStatus(
                    //                  tabName: tab.TabName,
                    //                  fileType: fileType,
                    //                  loginId: "",
                    //                  message: $"Temp file is deleting in progress",
                    //                  messageType: "info",
                    //                  processId: tab.ProcessId,
                    //                  processName: flpConfigurationResponseDto.ProcessName,
                    //                  tableName: tab.TableName,
                    //                  totalRows: tab.TotalRows,
                    //                  flpConfigurationId: flpConfigurationId,
                    //                  fileUploadedId: uploadedFileId,
                    //                  FileStatusActivityEnum.Processing,
                    //                  FlpActivityLogStatusEnum.FileDeletedFromTemp, null
                    //     );
                    //    if (tab.FileProcessCompleted)
                    //    {
                    //        await _flpProcessingService.AddFileProcessLosStatus(
                    //                      tabName: tab.TabName,
                    //                      fileType: fileType,
                    //                      loginId: "",
                    //                      message: $"Temp file is deleted sucessfully for this stage",
                    //                      messageType: "info",
                    //                      processId: tab.ProcessId,
                    //                      processName: flpConfigurationResponseDto.ProcessName,
                    //                      tableName: tab.TableName,
                    //                      totalRows: tab.TotalRows,
                    //                      flpConfigurationId: flpConfigurationId,
                    //                      fileUploadedId: uploadedFileId,
                    //                      FileStatusActivityEnum.ProcessCompleted,
                    //                      FlpActivityLogStatusEnum.FileDeletedFromTemp, null
                    //         );
                    //    }
                    //    else
                    //    {
                    //        await _flpProcessingService.AddFileProcessLosStatus(
                    //                     tabName: tab.TabName,
                    //                     fileType: fileType,
                    //                     loginId: "",
                    //                     message: $"Temp file is not  deleted sucessfully for this stage",
                    //                     messageType: "info",
                    //                     processId: tab.ProcessId,
                    //                     processName: flpConfigurationResponseDto.ProcessName,
                    //                     tableName: tab.TableName,
                    //                     totalRows: tab.TotalRows,
                    //                     flpConfigurationId: flpConfigurationId,
                    //                     fileUploadedId: uploadedFileId,
                    //                     FileStatusActivityEnum.Error,
                    //                     FlpActivityLogStatusEnum.FileDeletedFromTemp, null
                    //        );
                    //    }



                    //}
                    //await _fileLoadingProcessConfigurationService.UpdateProcessStatus(uploadedFileId, (int)FlpProcessStatusEnum.Error);
                }


                    flpConvertToParquetResponseDto.FlpProcessTabResponseList = tabResList;
                flpConvertToParquetResponseDto.Message = "All processes completed (some may have errors).";

                return new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { flpConvertToParquetResponseDto.Message },
                    Result = flpConvertToParquetResponseDto
                };
            }
            catch (Exception ex)
            {
                return new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Something went wrong during parallel processing." },
                    Result = flpConvertToParquetResponseDto
                };
            }
        }


       /// <summary>
       /// Excel Process to sql server
       /// </summary>
       /// <param name="flpConfigurationId"></param>
       /// <param name="uploadedFileId"></param>
       /// <param name="fLPConfigurationModel"></param>
       /// <param name="destStorage"></param>
       /// <param name="sharedDestServer"></param>
       /// <param name="flpProcessTempFileModel"></param>
       /// <param name="flpConfigurationResponseDto"></param>
       /// <param name="fileSetting"></param>
       /// <returns></returns>
        public async Task<APIResponse<FlpProcessTabResponseDto>> ExcelProcessForSqlServer(string flpConfigurationId, string uploadedFileId, FLPConfigurationModel fLPConfigurationModel,
                                                                                                     DestinationStorageAccountDtoV4_1 destStorage,
                                                                                                     SharedLocationDestinationServerDtoV4_1? sharedDestServer,
                                                                                                     FlpProcessTempFileModel flpProcessTempFileModel,
                                                                                                     FlpConfigurationResponseDtoV4_1 flpConfigurationResponseDto,
                                                                                                     FileSettings fileSetting)
        {
           // FlpProcessResponseDto flpConvertToParquetResponseDto = new FlpProcessResponseDto();
            //List<FlpProcessTabResponseDto> tabResList = new List<FlpProcessTabResponseDto>();
            string fileType = "excel";
            FlpProcessTabResponseDto flpProcessTabResponseDto = new FlpProcessTabResponseDto();
            FlpActivityLogStatusEnum currentStatus = FlpActivityLogStatusEnum.FileSchemaValidated;
            long processId = long.Parse(DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            try
            {

               // List<FileSettings> fileSettingsList = fLPConfigurationModel.fileSettings;
               // foreach (var fileSetting in fileSettingsList)
                if(fileSetting !=null)
                {
                    FlpConfigurationResponseDtoV4_1 flpConfig =null;
                    flpProcessTabResponseDto.FileProcessCompleted = false;
                    flpProcessTabResponseDto.TabName = fileSetting?.tabName??"";
                   
                    var processSettings = fLPConfigurationModel.processSettings;
                    var configResponse = await _fileLoadingProcessConfigurationService.GetMultisheetConfiguration(flpConfigurationId, uploadedFileId, fileSetting.tabName);
                    if (configResponse.ResponseCode == APIResultStatus.Completed.Code)
                    {
                        flpConfig = configResponse.Result;
                        flpConfig.BlobClients = flpConfigurationResponseDto.BlobClients;
                        if (!FlpConfigurationHelper.ValidString(flpConfig.DatabaseConnectionSecret))
                        {
                            //status maintain and skip the loop for next tab/sheet
                            _logger.LogError($"Multisheet Error:Not found DatabaseConnectionSecret for configurationId {flpConfigurationId}");
                            return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                            {
                                ResultStatus = APIResultStatus.Error,
                                ResponseMessage = new List<string> { $"Not found DatabaseConnectionSecret for {flpConfigurationId}" },
                                Result = flpProcessTabResponseDto
                            });
                        }


                    }
                    else
                    {
                        _logger.LogError($"Multisheet Error:Not found data for configurationId {flpConfigurationId} and uploadedFileId {uploadedFileId}");
                        return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { $"Not found DatabaseConnectionSecret for {flpConfigurationId}" },
                            Result = flpProcessTabResponseDto
                        });
                    }



                    ConfigurationTableMappingDto configurationTableMappingDto = new ConfigurationTableMappingDto
                    {
                        FlpConfigurationId = flpConfigurationId,
                        TableName = fileSetting.databaseSettings.table_name,
                        TabName = fileSetting.tabName,
                        ColumnNameList = fileSetting.additionalSettings.column_name_list,
                        ConvertDataTypeColumnNameList = fileSetting.additionalSettings.convert_datatypes_column_list,
                        Delimiter = fileSetting.additionalSettings.delimiter,
                        DoNotArchiveFile = fileSetting.additionalSettings.do_not_archive_file,
                        DropHistoryTable = fileSetting.databaseSettings.drop_history_table,
                        DropMainTable = fileSetting.databaseSettings.drop_main_table,
                        FileNameString = "",
                        FlpTabName = "",
                        IgnoreDuplicateRows = fileSetting.additionalSettings.ignore_duplicate_rows,
                        IsHeaderProvided = fileSetting.additionalSettings.is_header_provided,
                        KeepFirstRow = fileSetting.additionalSettings.keep_first_row,
                        KeyColumnList = fileSetting.additionalSettings.key_column_list,
                        OrderByColumnListForDedup = fileSetting.additionalSettings.order_by_column_list_for_dedup,
                        QuoteCharacter = fileSetting.additionalSettings.quote_character,
                        SkipFooterRows = fileSetting.additionalSettings.skip_footer_rows,
                        SkipRows = fileSetting.additionalSettings.skip_rows,
                        ValidateFileSchema = fileSetting.databaseSettings.validate_fileschema,
                        SpanishToEnglish = fileSetting.additionalSettings.spanish_to_english,
                        OrdinalToRoman = fileSetting.additionalSettings.roman_numerals_only,
                        SkipEmptyLines = fileSetting.additionalSettings.skip_empty_lines,
                        mergeData = fileSetting.databaseSettings.mergeData,
                        createHistoryTable = fileSetting.databaseSettings.createHistoryTable,
                        DatabaseConnectionSecret = flpConfig?.DatabaseConnectionSecret ?? "",
                        historyTableName = flpConfig?.HistoryTableName ?? "",
                        ParquetCompression = flpConfig?.ParquetCompression ?? "",//"gzip"
                        UnityCatalog = flpConfig?.UnityCatalog ?? "",
                       // campaignId = configResponse.Result?.campaignId ?? "",

                    };


                    //flpConfigurationRequestDto.TableName = tableName;
                    string connectionString = !string.IsNullOrWhiteSpace(configurationTableMappingDto.DatabaseConnectionSecret)?
                        KeyVault.GetKeyVaultValue(configurationTableMappingDto.DatabaseConnectionSecret)?.Result??"":string.Empty;
                 
                    string fileLocation = FlpConfigurationHelperV4_1.GetFileLocation(flpConfig);
                    string backupFileName = string.Empty;
                    flpConfig.FlpConfigurationId = flpConfigurationId;
                    flpConfig.UploadedFileId = uploadedFileId;
                    if (flpConfig.LocationTypeId == (int)SourceLocationTypeEnum.Azure)
                    {

                        backupFileName = FlpConfigurationHelper.GetBackUpFileName(flpConfig.BlobClients?.Name, processId.ToString());
                    }
                    else
                    {

                        backupFileName = FlpConfigurationHelper.GetBackUpFileName(flpConfig.SourcePath, processId.ToString());
                    }
                    flpProcessTabResponseDto.BackUpFileName = backupFileName;

                     
                    flpProcessTabResponseDto.BlobName = flpProcessTempFileModel?.Name ?? "";


                    await _flpProcessingService.AddFileProcessLosStatus(
                          tabName: configurationTableMappingDto.TabName,
                          fileType: fileType,
                          loginId: "",
                          message: $"File schema validation in progress",
                          messageType: "info",
                          processId: processId,
                          processName: flpConfig.ProcessName,
                          tableName: configurationTableMappingDto.TableName,
                          totalRows: 0,
                          flpConfigurationId: flpConfig.FlpConfigurationId,
                          fileUploadedId: uploadedFileId,
                          FileStatusActivityEnum.Processing,
                          FlpActivityLogStatusEnum.FileSchemaValidated, null
                      );


                    ParquetFileResponseDtoV4_1 resultResponse = null;
                    flpConfig.ProcessId = processId;
                    flpConfig.FileType = fileType;
                    bool addExcelRowNo = false;

                    if (flpConfig.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                    {
                        _logger.LogInformation($"Updating RowNo for  {flpConfig.FlpConfigurationId} and uploadFileId {flpConfig.UploadedFileId}, tabName {configurationTableMappingDto.TabName}");
                        if (flpConfig.UIValidation || flpConfig.BEValidation)
                        {
                            //Call the API to update row no
                            var token =  _flpProcessingService.GetBearerToken();
                            var skipRows = configurationTableMappingDto.SkipRows ==0 ? 1 : configurationTableMappingDto.SkipRows;
                            var validationRuleServiceHelper = new ValidationRuleServiceHelper(_logger);
                           var result =  await validationRuleServiceHelper.AddExcelRowNo(flpConfigurationId,uploadedFileId,configurationTableMappingDto.TabName,
                                token,flpConfig.DestinationPath, destStorage.StorageContainerName, flpProcessTempFileModel?.Uri??"", destStorage.FlpStorageAccount,destStorage.SasKey, skipRows,skipRows+1,false,configurationTableMappingDto.IsHeaderProvided,configurationTableMappingDto.SkipEmptyLines);
                            if(result.ResultStatus.Code != APIResultStatus.Completed.Code)
                            {
                                addExcelRowNo = true;
                                flpProcessTabResponseDto.FileProcessCompleted = false;
                                //Add error log
                                await _flpProcessingService.AddFileProcessLosStatus(
                                     tabName: configurationTableMappingDto.TabName,
                                      fileType: fileType,
                                      loginId: "",
                                      message: $"Error:{result.ResponseMessage.FirstOrDefault()}",
                                      messageType: "error",
                                      processId: processId,
                                      processName: flpConfig.ProcessName,
                                      tableName: configurationTableMappingDto.TableName,// flpConfigurationRequestDto.TableName,
                                      totalRows: 0,
                                      flpConfigurationId: flpConfig.FlpConfigurationId,
                                      fileUploadedId: flpConfig.UploadedFileId,
                                      FileStatusActivityEnum.Error,
                                      FlpActivityLogStatusEnum.FileSchemaValidated, null
                                 );

                            }
                            else
                            {
                                //Need to be added tab name in log table inside this ConvertDataToParquet
                                resultResponse = await _excelToParquetService.ConvertDataToParquet(configurationTableMappingDto, flpConfig, flpProcessTempFileModel);
                            }
                        }
                        else
                        {
                            //Need to be added tab name in log table inside this ConvertDataToParquet
                            resultResponse = await _excelToParquetService.ConvertDataToParquet(configurationTableMappingDto, flpConfig, flpProcessTempFileModel);
                        }
                           
                    }
                    if (resultResponse != null && resultResponse.ParquetFileCreated)
                    {
                        currentStatus = FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation;
                        var response = await _flpProcessingService.ParquetFileProcessToBronzeTable(processId, fileType, fileLocation, connectionString, currentStatus, flpProcessTempFileModel, resultResponse, configurationTableMappingDto, flpConfig, destStorage, sharedDestServer);
                        // return response;
                        if (response.ResponseCode == APIResultStatus.Completed.Code)
                        {
                            flpProcessTabResponseDto = response.Result;
                            
                            flpProcessTabResponseDto.FileProcessCompleted = true;
                        }
                        else
                        {
                            flpProcessTabResponseDto = response.Result;
                        }
                       

                    }
                    else
                    {
                        string errorMessage = resultResponse?.ErrorMessage ?? "Something went wrong";
                        _logger.LogError($"Multisheet Error: {errorMessage} for tableName {configurationTableMappingDto.TableName} flpConfigurationId {flpConfig.FlpConfigurationId} tabName {configurationTableMappingDto.TabName}");
                        flpProcessTabResponseDto.FileProcessCompleted = false;
                        if (addExcelRowNo == false && resultResponse?.flpActivityLogStatusEnum != FlpActivityLogStatusEnum.FileSchemaValidated)
                        {
                            //Add error log
                            await _flpProcessingService.AddFileProcessLosStatus(
                                 tabName: configurationTableMappingDto.TabName,
                                  fileType: fileType,
                                  loginId: "",
                                  message: $"Error:{errorMessage}",
                                  messageType: "error",
                                  processId: processId,
                                  processName: flpConfig.ProcessName,
                                  tableName: configurationTableMappingDto.TableName,// flpConfigurationRequestDto.TableName,
                                  totalRows: 0,
                                  flpConfigurationId: flpConfig.FlpConfigurationId,
                                  fileUploadedId: flpConfig.UploadedFileId,
                                  FileStatusActivityEnum.Error,
                                  FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation,null
                             );
                        }

                    }
                    int totalRows = flpProcessTabResponseDto.TotalRows;
                    int insertedRows = flpProcessTabResponseDto.InsertedRows;
                    int duplicateRows = flpProcessTabResponseDto.DuplicateRows;
                    string blobName = flpProcessTabResponseDto.BlobName;
                    string backUpFileName = flpProcessTabResponseDto.BackUpFileName;
                    flpProcessTabResponseDto.TabName = fileSetting.tabName;
                    await _fileLoadingProcessConfigurationService.AddBackUpFileDetails(uploadedFileId, flpConfigurationId, backUpFileName,
                        configurationTableMappingDto.TabName, totalRows, insertedRows, duplicateRows, blobName);
                   // tabResList.Add(flpProcessTabResponseDto);
                }

               // flpConvertToParquetResponseDto.FlpProcessTabResponseList = tabResList;
              //  flpConvertToParquetResponseDto.Message = "Process is completed successfully.";
                return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Process is completed successfully." },
                    Result = flpProcessTabResponseDto
                });
            }
            catch (Exception ex)
            {
                flpProcessTabResponseDto.FileProcessCompleted = false;

                if(currentStatus == FlpActivityLogStatusEnum.FileSchemaValidated)
                {
                    _logger.LogError($"Error occurred {ex.Message.ToString()}, flpConfigrationId {flpConfigurationId}, uploadedFileId {uploadedFileId}, tabName {fileSetting?.tabName??""}");
                    await _flpProcessingService.AddFileProcessLosStatus(
                          tabName: fileSetting?.tabName,
                          fileType: fileType,
                          loginId: "",
                          message: "Error: Internal Server Error.",
                          messageType: "error",
                          processId: processId,
                          processName: flpConfigurationResponseDto.ProcessName,
                          tableName: fileSetting?.databaseSettings?.table_name,
                          totalRows: 0,
                          flpConfigurationId: flpConfigurationId,
                          fileUploadedId: uploadedFileId,
                          FileStatusActivityEnum.Error,
                          FlpActivityLogStatusEnum.FileSchemaValidated, null
                      );
                }
                //_logger.LogError($"Error processing flpConfigurationId {flpConfig.FlpConfigurationId}: {ex.Message}");
                return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "something went wrong" },
                    Result = flpProcessTabResponseDto
                });
            }
        }



    

        /// <summary>
        /// Run Databricks ExcelProcesses Parallel
        /// </summary>
        /// <param name="flpConfigurationId"></param>
        /// <param name="uploadedFileId"></param>
        /// <param name="fLPConfigurationModel"></param>
        /// <param name="destStorage"></param>
        /// <param name="sharedDestServer"></param>
        /// <param name="flpProcessTempFileModel"></param>
        /// <param name="flpConfigurationResponseDto"></param>
        /// <param name="databricksStorageAccountDto"></param>
        /// <returns></returns>


        public async Task<APIResponse<FlpProcessResponseDto>> RunDatabricksExcelProcessesInParallelAsync(string flpConfigurationId, string uploadedFileId, FLPConfigurationModel fLPConfigurationModel,
                                                       DestinationStorageAccountDtoV4_1 destStorage, SharedLocationDestinationServerDtoV4_1? sharedDestServer, FlpProcessTempFileModel flpProcessTempFileModel,
                                                       FlpConfigurationResponseDtoV4_1 flpConfigurationResponseDto)
        {
            FlpProcessResponseDto flpConvertToParquetResponseDto = new FlpProcessResponseDto();
            List<FlpProcessTabResponseDto> tabResList = new List<FlpProcessTabResponseDto>();
            string fileType = "excel";

            try
            {
                List<FileSettings> fileSettingsList = fLPConfigurationModel.fileSettings.ToList();
                if (!fileSettingsList.Any())
                {
                    _logger.LogError($"Multisheet Error:Not found fileSetting details for flpConfigurationId {flpConfigurationId} and uploadedFileId {uploadedFileId}");
                    return new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { "Something went wrong during parallel processing." },
                        Result = flpConvertToParquetResponseDto
                    };
                }
                foreach (var fileSetting in fileSettingsList)
                {
                    //if (!fileSetting.ignoreSheet)
                    {
                        FlpProcessTabResponseDto flpProcessTabResponseDto = new FlpProcessTabResponseDto();
                        var res = await ExcelProcessForDatabricks(flpConfigurationId, uploadedFileId, fLPConfigurationModel, destStorage, sharedDestServer, flpProcessTempFileModel, flpConfigurationResponseDto, fileSetting);

                        flpProcessTabResponseDto = res.Result;
                        tabResList.Add(flpProcessTabResponseDto);
                    }
                   
                }

                //var tasks = fileSettingsList.Select(async fileSetting =>
                //{
                //    try
                //    {
                //        var res = await ExcelProcess(flpConfigurationId, uploadedFileId, fLPConfigurationModel, destStorage, sharedDestServer, flpProcessTempFileModel, flpConfigurationResponseDto, fileSetting);
                //        return res?.Result ?? new FlpProcessTabResponseDto(); // Handle null or failed response gracefully
                //    }
                //    catch
                //    {
                //        return new FlpProcessTabResponseDto();
                //    }
                //}).ToList();

                //var results = await Task.WhenAll(tasks);
                //tabResList.AddRange(results);

                // Check file processing status
                bool allProcessed = tabResList.All(x => x.FileProcessCompleted);
                bool anyErrorOccurred = tabResList.Any(x => !x.FileProcessCompleted);
                bool anyProcessed = tabResList.Any(x => x.FileProcessCompleted);
                //if the temp file deleted true then update the status Processed otherwise Error

                var isAllStatusCompleted = tabResList.All(x => x.DatabricksProcessStatusId == (int)DatabricksProcessStatusEnum.Completed);
                var isAllStatusFailed = tabResList.All(x => x.DatabricksProcessStatusId == (int)DatabricksProcessStatusEnum.Failed);
                //  var allProcerssCompleted = tabResList.All(x=>x.FileProcessCompleted);

                //if (allProcessed)
                //{
                //    (bool tempFileDeleted, string tempFileResponse) = await _flpProcessingService.DeleteTempFileFromTempLocation(flpConfigurationResponseDto.DestinationLocationTypeId ?? 0, fileType, flpConfigurationResponseDto.ProcessName, flpConfigurationId, uploadedFileId, flpProcessTempFileModel, tabResList);
                //    if (!tempFileDeleted)
                //    {
                //        return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                //        {
                //            ResultStatus = APIResultStatus.Error,
                //            ResponseMessage = new List<string> { tempFileResponse },
                //            Result = flpConvertToParquetResponseDto
                //        });
                //    }
                //    //Once we delete the temp file then update the status Processed

                //}

                // Update status based on deletion result
                // var status = isAllStatusCompleted ? FlpProcessStatusEnum.Processed : isAllStatusFailed ? FlpProcessStatusEnum.Error: FlpProcessStatusEnum.Processing;
                if (isAllStatusFailed)
                {
                    await _fileLoadingProcessConfigurationService.UpdateProcessStatus(uploadedFileId,(int)FlpProcessStatusEnum.Error);
                }
                


                flpConvertToParquetResponseDto.FlpProcessTabResponseList = tabResList;
                flpConvertToParquetResponseDto.Message = "Processes completed.";

                return new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { flpConvertToParquetResponseDto.Message },
                    Result = flpConvertToParquetResponseDto
                };
            }
            catch (Exception ex)
            {
                return new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Something went wrong during parallel processing." },
                    Result = flpConvertToParquetResponseDto
                };
            }
        }



        public async Task<APIResponse<FlpProcessTabResponseDto>> ExcelProcessForDatabricks(string flpConfigurationId, string uploadedFileId, FLPConfigurationModel fLPConfigurationModel,
                                                                                                     DestinationStorageAccountDtoV4_1 destStorage,
                                                                                                     SharedLocationDestinationServerDtoV4_1? sharedDestServer,
                                                                                                     FlpProcessTempFileModel flpProcessTempFileModel,
                                                                                                     FlpConfigurationResponseDtoV4_1 flpConfigurationResponseDto,
                                                                                                     FileSettings fileSetting)
        {
            // FlpProcessResponseDto flpConvertToParquetResponseDto = new FlpProcessResponseDto();
            //List<FlpProcessTabResponseDto> tabResList = new List<FlpProcessTabResponseDto>();
            string fileType = "excel";
            FlpProcessTabResponseDto flpProcessTabResponseDto = new FlpProcessTabResponseDto();
            flpProcessTabResponseDto.FileProcessCompleted = false;
            flpProcessTabResponseDto.DatabricksProcessStatusId = (int)DatabricksProcessStatusEnum.Failed;
            try
            {
                if (fileSetting != null)
                {
                    FlpConfigurationResponseDtoV4_1 flpConfig = null;
                    DatabricksStorageAccountDto4_1 databricksStorageAccountDto = await _fileLoadingProcessConfigurationService.DatabricksStorageAccountInfo(flpConfigurationId, fileSetting.tabName);
                    if (databricksStorageAccountDto == null)
                    {
                        _logger.LogError($"Multisheet Error:Not found databricksStorageAccountDto for configurationId {flpConfigurationId},uploadedId {uploadedFileId},tabName {fileSetting.tabName}");
                        return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { $"Not found databricksStorageAccountDto for {flpConfigurationId}" },
                            Result = flpProcessTabResponseDto
                        });
                    }
                    var processSettings = fLPConfigurationModel.processSettings;
                    var configResponse = await _fileLoadingProcessConfigurationService.GetMultisheetConfiguration(flpConfigurationId, uploadedFileId, fileSetting.tabName);
                    if (configResponse.ResponseCode == APIResultStatus.Completed.Code)
                    {
                        flpConfig = configResponse.Result;
                        flpConfig.BlobClients = flpConfigurationResponseDto.BlobClients;
                        flpConfig.FlpConfigurationId = flpConfigurationId;
                        flpConfig.UploadedFileId = uploadedFileId;



                    }
                    else
                    {
                        _logger.LogError($"Multisheet Error:Not found data for configurationId {flpConfigurationId} and uploadedFileId {uploadedFileId}");
                        return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { $"Not found DatabaseConnectionSecret for {flpConfigurationId}" },
                            Result = flpProcessTabResponseDto
                        });
                    }



                    ConfigurationTableMappingDto configurationTableMappingDto = new ConfigurationTableMappingDto
                    {
                        FlpConfigurationId = flpConfigurationId,
                        TableName = fileSetting.databaseSettings.table_name,
                        TabName = fileSetting.tabName,
                        ColumnNameList = fileSetting.additionalSettings.column_name_list,
                        ConvertDataTypeColumnNameList = fileSetting.additionalSettings.convert_datatypes_column_list,
                        Delimiter = fileSetting.additionalSettings.delimiter,
                        DoNotArchiveFile = fileSetting.additionalSettings.do_not_archive_file,
                        DropHistoryTable = fileSetting.databaseSettings.drop_history_table,
                        DropMainTable = fileSetting.databaseSettings.drop_main_table,
                        FileNameString = "",
                        FlpTabName = "",
                        IgnoreDuplicateRows = fileSetting.additionalSettings.ignore_duplicate_rows,
                        IsHeaderProvided = fileSetting.additionalSettings.is_header_provided,
                        KeepFirstRow = fileSetting.additionalSettings.keep_first_row,
                        KeyColumnList = fileSetting.additionalSettings.key_column_list,
                        OrderByColumnListForDedup = fileSetting.additionalSettings.order_by_column_list_for_dedup,
                        QuoteCharacter = fileSetting.additionalSettings.quote_character,
                        SkipFooterRows = fileSetting.additionalSettings.skip_footer_rows,
                        SkipRows = fileSetting.additionalSettings.skip_rows,
                        ValidateFileSchema = fileSetting.databaseSettings.validate_fileschema,
                        SpanishToEnglish = fileSetting.additionalSettings.spanish_to_english,
                        OrdinalToRoman = fileSetting.additionalSettings.roman_numerals_only,
                        SkipEmptyLines = fileSetting.additionalSettings.skip_empty_lines,
                        mergeData = fileSetting.databaseSettings.mergeData,
                        createHistoryTable = fileSetting.databaseSettings.createHistoryTable,
                        DatabaseConnectionSecret = flpConfig?.DatabaseConnectionSecret ?? "",
                        historyTableName = flpConfig?.HistoryTableName ?? "",
                        ParquetCompression = flpConfig?.ParquetCompression ?? "",//"gzip"
                        UnityCatalog = flpConfig?.UnityCatalog ?? "",
                        campaignId = configResponse.Result?.campaignId ?? "",

                    };


                    //flpConfigurationRequestDto.TableName = tableName;
                    string connectionString = "";// KeyVault.GetKeyVaultValue(configurationTableMappingDto.DatabaseConnectionSecret).Result;
                    long processId = long.Parse(DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                    string fileLocation = FlpConfigurationHelperV4_1.GetFileLocation(flpConfig);
                    string backupFileName = string.Empty;
                    bool addExcelRowNo = false;
                    if (flpConfig.LocationTypeId == (int)SourceLocationTypeEnum.Azure)
                    {

                        backupFileName = FlpConfigurationHelper.GetBackUpFileName(flpConfig.BlobClients?.Name, processId.ToString());
                    }
                    else
                    {

                        backupFileName = FlpConfigurationHelper.GetBackUpFileName(flpConfig.SourcePath, processId.ToString());
                    }
                    flpProcessTabResponseDto.BackUpFileName = backupFileName;
                    flpProcessTabResponseDto.ProcessId = processId;

                    FlpActivityLogStatusEnum currentStatus = FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation;
                    flpProcessTabResponseDto.BlobName = flpProcessTempFileModel?.Name ?? "";


                    await _flpProcessingService.AddFileProcessLosStatus(
                          tabName: configurationTableMappingDto.TabName,
                          fileType: fileType,
                          loginId: "",
                          message: $"File schema validation in progress",
                          messageType: "info",
                          processId: processId,
                          processName: flpConfig.ProcessName,
                          tableName: configurationTableMappingDto.TableName,
                          totalRows: 0,
                          flpConfigurationId: flpConfig.FlpConfigurationId,
                          fileUploadedId: uploadedFileId,
                          FileStatusActivityEnum.Processing,
                          FlpActivityLogStatusEnum.FileSchemaValidated, null
                      );


                    ParquetFileResponseDtoV4_1 resultResponse = null;
                    flpConfig.ProcessId = processId;
                    flpConfig.FileType = fileType;
                    FlpDatabricksProcessResponseDtoV4_1 res = null;

                    if (flpConfig.DestinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                    {
                        //Need to be added tab name in log table inside this ConvertDataToParquet
                        
                        if (flpConfig.UIValidation || flpConfig.BEValidation)
                        {
                            _logger.LogInformation($"Updating RowNo for  {flpConfig.FlpConfigurationId} and uploadFileId {flpConfig.UploadedFileId}, tabName {configurationTableMappingDto.TabName}");
                            //Call the API to update row no
                            var token = _flpProcessingService.GetBearerToken();
                            var skipRows = configurationTableMappingDto.SkipRows == 0 ? 1 : configurationTableMappingDto.SkipRows;
                            var validationRuleServiceHelper = new ValidationRuleServiceHelper(_logger);
                            var result = await validationRuleServiceHelper.AddExcelRowNo(flpConfigurationId, uploadedFileId, configurationTableMappingDto.TabName,
                                 token, flpConfig.DestinationPath, destStorage.StorageContainerName, flpProcessTempFileModel?.Uri ?? "", destStorage.FlpStorageAccount, destStorage.SasKey, skipRows, skipRows + 1, false,configurationTableMappingDto.IsHeaderProvided,configurationTableMappingDto.SkipEmptyLines);
                            if (result.ResultStatus.Code != APIResultStatus.Completed.Code)
                            {
                                addExcelRowNo = true;
                                flpProcessTabResponseDto.FileProcessCompleted = false;
                                //Add error log
                                await _flpProcessingService.AddFileProcessLosStatus(
                                     tabName: configurationTableMappingDto.TabName,
                                      fileType: fileType,
                                      loginId: "",
                                      message: $"Error:{result.ResponseMessage.FirstOrDefault()}",
                                      messageType: "error",
                                      processId: processId,
                                      processName: flpConfig.ProcessName,
                                      tableName: configurationTableMappingDto.TableName,// flpConfigurationRequestDto.TableName,
                                      totalRows: 0,
                                      flpConfigurationId: flpConfig.FlpConfigurationId,
                                      fileUploadedId: flpConfig.UploadedFileId,
                                      FileStatusActivityEnum.Error,
                                      FlpActivityLogStatusEnum.FileSchemaValidated, null
                                 );

                            }
                            else
                            {
                                //Need to be added tab name in log table inside this ConvertDataToParquet
                                resultResponse = await _excelToParquetService.ConvertDataToParquet(configurationTableMappingDto, flpConfig, flpProcessTempFileModel);
                            }

                        }
                        else
                        {
                            resultResponse = await _excelToParquetService.ConvertDataToParquet(configurationTableMappingDto, flpConfig, flpProcessTempFileModel);
                        }
                            
                    }
                    if (resultResponse != null && resultResponse.ParquetFileCreated)
                    {
                        var response = await _flpProcessingService.ParquetFileProcessToDataLake(processId, fileType, fileLocation, connectionString, currentStatus, flpProcessTempFileModel, resultResponse, configurationTableMappingDto, flpConfig, destStorage, sharedDestServer, databricksStorageAccountDto);
                        // return response;
                        
                        if (response.ResponseCode == APIResultStatus.Completed.Code)
                        {
                            flpProcessTabResponseDto.FileProcessCompleted = true;
                            res = response.Result;
                            //RunId will be null if job not started yet & got some error from databricks while starting the job
                            if (res != null & res?.RunId !=null && res?.RunId >0)
                            {

                                flpProcessTabResponseDto.BackUpFileName = resultResponse.ParquetFilePath;
                                flpProcessTabResponseDto.TabName = fileSetting.tabName;
                                flpProcessTabResponseDto.TotalRows = res.TotalRows;
                                flpProcessTabResponseDto.InsertedRows = resultResponse.InsertedRows;
                                flpProcessTabResponseDto.DuplicateRows = resultResponse.DuplicateRows;
                                flpProcessTabResponseDto.BlobName = res.BlobName;

                                if (response.Result.LifeCycleStateId == (int)LifeCycleStateEnum.TERMINATED && response.Result.ResultStateId == (int)ResultStateEnum.SUCCESS)
                                {
                                    //Set the process status completed
                                    flpProcessTabResponseDto.DatabricksProcessStatusId = (int)DatabricksProcessStatusEnum.Completed;
                                    //Update runid with success status
                                    FlpProcessLogHistoryStatusDtov4_1 processLogHistoryStatus1 = new FlpProcessLogHistoryStatusDtov4_1()
                                    {
                                        FlpProcessStatusId = (int)FlpProcessStatusEnum.Processed,
                                        FileUploadedId = uploadedFileId,
                                        FlpConfigurationId = flpConfigurationId,
                                        DatabricksStageId = res.JobStatusId,
                                        RunId = res.RunId,
                                        TerminationDetailsId = res.TerminationStatusId,
                                        LifeCycleStateId = res.LifeCycleStateId,
                                        ResultStateId = res.ResultStateId,
                                        SkipUpdateHistoryStatus = true,
                                        tabName = configurationTableMappingDto.TabName

                                    };
                                    await _databricksDbRepository.UpdateLogHistoryStatus(processLogHistoryStatus1);
                                }
                                else
                                {
                                    //IN progress status
                                    flpProcessTabResponseDto.DatabricksProcessStatusId = (int)DatabricksProcessStatusEnum.InProgress;
                                    FlpProcessLogHistoryStatusDtov4_1 processLogHistoryStatus2 = new FlpProcessLogHistoryStatusDtov4_1()
                                    {
                                        FlpProcessStatusId = (int)FlpProcessStatusEnum.NotProcessed,
                                        FileUploadedId = uploadedFileId,
                                        FlpConfigurationId = flpConfigurationId,
                                        DatabricksStageId = res.JobStatusId,
                                        RunId = res.RunId,
                                        TerminationDetailsId = res.TerminationStatusId,
                                        LifeCycleStateId = res.LifeCycleStateId,
                                        ResultStateId = res.ResultStateId,
                                        SkipUpdateHistoryStatus = true,
                                        tabName = configurationTableMappingDto.TabName

                                    };
                                    await _databricksDbRepository.UpdateLogHistoryStatus(processLogHistoryStatus2);
                                }
                            }
                            else
                            {
                                //Set the process status failed
                                flpProcessTabResponseDto.DatabricksProcessStatusId = (int)DatabricksProcessStatusEnum.Failed;
                                flpProcessTabResponseDto.BackUpFileName = resultResponse.ParquetFilePath;
                                flpProcessTabResponseDto.TabName = fileSetting.tabName;
                                flpProcessTabResponseDto.TotalRows = resultResponse.TotalRows;
                                flpProcessTabResponseDto.InsertedRows = 0;
                                flpProcessTabResponseDto.DuplicateRows = 0;
                                flpProcessTabResponseDto.BlobName = res.BlobName;


                            }
                        }
                        else
                        {
                            flpProcessTabResponseDto.DatabricksProcessStatusId = (int)DatabricksProcessStatusEnum.Failed;//Error
                            flpProcessTabResponseDto.FileProcessCompleted = false;
                            flpProcessTabResponseDto.TabName = fileSetting.tabName;
                            flpProcessTabResponseDto.TotalRows = resultResponse.TotalRows;
                            flpProcessTabResponseDto.InsertedRows = 0;
                            flpProcessTabResponseDto.DuplicateRows = 0;
                        }
                    }
                    else
                    {
                        flpProcessTabResponseDto.DatabricksProcessStatusId = (int)DatabricksProcessStatusEnum.Failed;//Error
                        flpProcessTabResponseDto.FileProcessCompleted = false;
                        flpProcessTabResponseDto.TabName = fileSetting.tabName;
                        flpProcessTabResponseDto.TotalRows =0;
                        flpProcessTabResponseDto.InsertedRows = 0;
                        flpProcessTabResponseDto.DuplicateRows = 0;
                        string errorMessage = resultResponse?.ErrorMessage ?? "Something went wrong";
                        _logger.LogError($"Multisheet Error: {errorMessage} for  flpConfigurationId {flpConfig.FlpConfigurationId}, uploadedFileId {uploadedFileId} tabName {fileSetting?.tabName}");

                        if (!addExcelRowNo &&  resultResponse?.flpActivityLogStatusEnum != FlpActivityLogStatusEnum.FileSchemaValidated)
                        {
                            //Add error log
                            await _flpProcessingService.AddFileProcessLosStatus(
                                 tabName: configurationTableMappingDto.TabName,
                                  fileType: fileType,
                                  loginId: "",
                                  message: $"Error:{errorMessage}",
                                  messageType: "error",
                                  processId: processId,
                                  processName: flpConfig.ProcessName,
                                  tableName: configurationTableMappingDto.TableName,//flpConfigurationRequestDto.TableName,
                                  totalRows: 0,
                                  flpConfigurationId: flpConfig.FlpConfigurationId,
                                  fileUploadedId: flpConfig.UploadedFileId,
                                  FileStatusActivityEnum.Error,
                                  FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation, null
                             );
                        }
                    } 
                    int totalRows = flpProcessTabResponseDto.TotalRows;
                    int insertedRows = flpProcessTabResponseDto.InsertedRows;
                    int duplicateRows = flpProcessTabResponseDto.DuplicateRows;
                    string blobName = flpProcessTabResponseDto.BlobName;
                    string backUpFileName = flpProcessTabResponseDto.BackUpFileName;
                    flpProcessTabResponseDto.TabName = fileSetting.tabName;
                    await _fileLoadingProcessConfigurationService.AddBackUpFileDetails(uploadedFileId, flpConfigurationId, backUpFileName,
                        configurationTableMappingDto.TabName, totalRows, insertedRows, duplicateRows, blobName);


                    if (flpProcessTabResponseDto.DatabricksProcessStatusId != (int)DatabricksProcessStatusEnum.Failed)
                    {
                        flpProcessTabResponseDto.FileProcessCompleted = true;
                        return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                        {
                            ResultStatus = APIResultStatus.Completed,
                            ResponseMessage = new List<string> { "Process is completed successfully." },
                            Result = flpProcessTabResponseDto
                        });
                    }

                    FlpProcessLogHistoryStatusDtov4_1 processLogHistoryStatus = new FlpProcessLogHistoryStatusDtov4_1()
                    {
                        FlpProcessStatusId = (int)FlpProcessStatusEnum.Error,
                        FileUploadedId = uploadedFileId,
                        FlpConfigurationId = flpConfigurationId,
                        DatabricksStageId = res?.JobStatusId,
                        RunId = res?.RunId,
                        TerminationDetailsId = res?.TerminationStatusId,
                        LifeCycleStateId = res?.LifeCycleStateId,
                        ResultStateId = res?.ResultStateId,
                        SkipUpdateHistoryStatus = true,
                        tabName = configurationTableMappingDto.TabName

                    };
                    await _databricksDbRepository.UpdateLogHistoryStatus(processLogHistoryStatus);

                }

                flpProcessTabResponseDto.FileProcessCompleted = false;
                return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "something went wrong" },
                    Result = flpProcessTabResponseDto
                });


            }
            catch (Exception ex)
            {
                flpProcessTabResponseDto.FileProcessCompleted = false;
                _logger.LogError($"Multisheet Error: {ex.Message} for flpConfigurationId:{flpConfigurationId}, uploadedId:{uploadedFileId}, tabName:{fileSetting?.tabName}:");
                return await Task.FromResult(new APIResponse<FlpProcessTabResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "something went wrong" },
                    Result = flpProcessTabResponseDto
                });
            }
        }

        public async Task LogFileStatus(FlpProcessTabResponseDto tab, string fileType,string processName,string flpConfigurationId,string uploadedFileId,FileStatusActivityEnum statusEnum = FileStatusActivityEnum.Processing,
                                       string message = "Temp file is deleting in progress")
        {
            await _flpProcessingService.AddFileProcessLosStatus(
                tabName: tab.TabName,
                fileType: fileType,
                loginId: "",
                message: message,
                messageType: "info",
                processId: tab.ProcessId,
                processName: processName,
                tableName: tab.TableName,
                totalRows: tab.TotalRows,
                flpConfigurationId: flpConfigurationId,
                fileUploadedId: uploadedFileId,
                statusEnum,
                FlpActivityLogStatusEnum.FileDeletedFromTemp,
                null);
        }

    }
}
