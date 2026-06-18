using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.ServiceModel.Channels;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.DatabricksAPI.Model;
using Teleperformance.DataIngestion.DatabricksAPI.Services;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService;
using Teleperformance.DataIngestion.Models.DTOs.v4._0;
using Teleperformance.DataIngestion.Models.DTOs.v4._0.DataBricksAPIJobStatus;
using Teleperformance.DataIngestion.Models.DTOs.v4._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v4._0.DataBricksAPIJobStatus;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Enums.v4._0;
using Teleperformance.DataIngestion.Models.Helpers;

namespace Teleperformance.DataIngestion.DataAccess.Services.v4._0
{
    public class DataBricksJobStatusService : IDataBricksJobStatusService
    {
        private readonly ILogger<DataBricksJobStatusService> _logger;
        private readonly IDatabricksAPIDbRepository _databricksAPIDbREpository;
        private readonly IFileLoadingProcessConfiguration _fileLoadingProcessConfiguration;
        private readonly ICache _cache;
        private readonly IFlpProcessingServiceV4_1 _flpProcessingServiceV4_1;
        private readonly IBlobStorageService _iBlobStorageService;

        public DataBricksJobStatusService(ILogger<DataBricksJobStatusService> logger, IDatabricksAPIDbRepository databricksAPIDbREpository, ICache cache,
            IFileLoadingProcessConfiguration fileLoadingProcessConfiguration, IFlpProcessingServiceV4_1 flpProcessingServiceV4_1, IBlobStorageService iBlobStorageService)
        {
            _logger = logger;
            _databricksAPIDbREpository = databricksAPIDbREpository;
            _cache = cache;
            _fileLoadingProcessConfiguration = fileLoadingProcessConfiguration;
            _flpProcessingServiceV4_1 = flpProcessingServiceV4_1;
            _iBlobStorageService = iBlobStorageService;
        }
        public async Task<APIResponse<IEnumerable<JobRunIdDetailsDto>>> GetRunIdDetails()
        {
            try
            {
                var dbResult = await _databricksAPIDbREpository.GetRunIdDetails();
                var result = dbResult.Select(x => new JobRunIdDetailsDto
                {
                    RunId = x.runId,
                    LogHistoryId = x.logHistoryId,
                    UploadFileId = x.uploadFileId,
                    ActivityProcessStatusId = x.activityProcessStatusId,
                    FlpFileLogStatusId = x.flpFileLogStatusId,
                    FlpConfigurationId = x.flpConfigurationId,
                    FlpProcessStatusId = x.flpProcessStatusId,
                    DatabricksStageId = x.databricksStageId,
                    TerminaionDetailsId = x.terminaionDetailsId,
                    ResultStateId = x.resultStateId,
                    LifeCycleStateId = x.lifeCycleStateId,
                    Message = x.message,
                    TabName = x.tabName
                });
                return await Task.FromResult(new APIResponse<IEnumerable<JobRunIdDetailsDto>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Successfully fetched records" },
                    Result = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString(), "Error occurred: GetRunIdDetails()");
                return await Task.FromResult(new APIResponse<IEnumerable<JobRunIdDetailsDto>>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Something went wrong" },
                    Result = null
                });
            }
        }


        public async Task<APIResponse<IEnumerable<JobRunIdDetailsDtoV2>>> GetRunIdDetailsForUpdateStatus()
        {
            try
            {
               
                var dbResult = await _databricksAPIDbREpository.GetJobRunDetailsForUpdateStatus();
                var result = dbResult.Select(x => new JobRunIdDetailsDtoV2
                {
                    RunId = x.runId,
                    UploadFileId = x.uploadFileId,
                    FlpConfigurationId = x.flpConfigurationId,
                    TabName = x.tabName
                });
                _logger.LogError($"Called GetRunIdDetailsForUpdateStatus() {result.Count()}", "Error occurred: GetRunIdDetailsForUpdateStatus()");
                return await Task.FromResult(new APIResponse<IEnumerable<JobRunIdDetailsDtoV2>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Successfully fetched records" },
                    Result = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString(), "Error occurred: GetRunIdDetailsForUpdateStatus()");
                return await Task.FromResult(new APIResponse<IEnumerable<JobRunIdDetailsDtoV2>>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Something went wrong" },
                    Result = null
                });
            }
        }

        public async Task<APIResponse<string>> UpdateLogHistoryStatus(JobRunIdDetailsDto jobRunIdDetailsDto)
        {
            FlpProcessLogHistoryStatusDto processLogHistoryStatus = null;
            try
            {
                int databricksStageId = 0;
                int terminationDetailsStatusId = 0;

                if (jobRunIdDetailsDto == null)
                {
                    _logger.LogError("jobRunIdDetailsDto is null");
                    return await Task.FromResult(new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "jobRunIdDetailsDto not found" },
                        Result = null
                    });
                }

                var databricksAPIServerDetails = await _databricksAPIDbREpository.GetDataBricksAPIServerDetails(jobRunIdDetailsDto.FlpConfigurationId);

                if (databricksAPIServerDetails == null)
                {
                    return await Task.FromResult(new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Databricks server details not found" },
                        Result = null
                    });
                }



                var jobRunner = new DatabricksJobRunner($"{databricksAPIServerDetails.databricksInstance}",
                    $"{databricksAPIServerDetails.databricksAPIVersion}", $"{databricksAPIServerDetails.databricksAPIToken}");
                //var currentStatus = await jobRunner.GetJobRunStatusAsync(jobRunIdDetailsDto.RunId??0);
                _logger.LogInformation($"Info:function app, API function  called UpdateLogHistoryStatus for flpConfig {jobRunIdDetailsDto.FlpConfigurationId} tabName {jobRunIdDetailsDto.TabName}, calling API");
                //Call job status api
                var jobRunStatusAPIResponse = await jobRunner.GetJobRunStatusAsync(jobRunIdDetailsDto.RunId ?? 0);
                if (jobRunStatusAPIResponse != null && (int)jobRunStatusAPIResponse.StatusCode == 200)
                {
                    if (jobRunStatusAPIResponse.JobSatus != null)
                    {

                        var jobStatusState = jobRunStatusAPIResponse.JobSatus.State;
                        _logger.LogInformation($"Info:function app, Writing Catalog- call status {jobStatusState.LifeCycleState} , {jobStatusState.ResultState} for flpconfigurationId {jobRunIdDetailsDto.FlpConfigurationId} tabName {jobRunIdDetailsDto.TabName}, calling API");
                        if (jobStatusState != null)
                        {
                            //Returning lifeCycleState and resultState from API response
                            var lifeCycleStateId = EnumHelper.GetLifeCycleStateEnumValueFromDescription(jobStatusState.LifeCycleState);
                            var resultStateId = EnumHelper.GetResultStateEnumEnumValueFromDescription(jobStatusState.ResultState);
                            var jobStatusMessage = $"life_cycle_state {jobStatusState.LifeCycleState} and result_state {jobStatusState.ResultState}";
                            _logger.LogInformation($"Info:function app, Writing Catalog- call status (before if) {lifeCycleStateId} and {resultStateId} line no 111, calling API");
                            if (lifeCycleStateId == (int)LifeCycleStateEnum.TERMINATED && resultStateId == (int)ResultStateEnum.SUCCESS)
                            {
                                _logger.LogInformation($"Info:function app Line no 117, Writing Catalog- call status {lifeCycleStateId} , {resultStateId} for flpconfigurationId {jobRunIdDetailsDto.FlpConfigurationId} tabName {jobRunIdDetailsDto.TabName}, calling API");
                                //Completed Process - Update  success status
                                processLogHistoryStatus = new FlpProcessLogHistoryStatusDto()
                                {
                                    LogHistoryId = jobRunIdDetailsDto.LogHistoryId,
                                    FlpConfigurationId = jobRunIdDetailsDto.FlpConfigurationId,
                                    FlpFileLogStatusId = jobRunIdDetailsDto.FlpFileLogStatusId,
                                    ActivityProcessStatusId = (int)FileStatusActivityEnum.ProcessCompleted,
                                    FileUploadedId = jobRunIdDetailsDto.UploadFileId,
                                    Message = $"Process completed successfully. {jobStatusMessage}",
                                    FlpProcessStatusId = (int)FlpProcessStatusEnum.Processed,
                                    DatabricksStageId = databricksStageId,
                                    RunId = jobRunIdDetailsDto.RunId,
                                    TerminationDetailsId = terminationDetailsStatusId,
                                    SkipUpdateHistoryStatus = false,
                                    DatabricksAPIResponse = jobRunStatusAPIResponse.ResponseContent,
                                    LifeCycleStateId = lifeCycleStateId,
                                    ResultStateId = resultStateId,
                                    tabName = jobRunIdDetailsDto.TabName
                                };
                                var dbResult1 = await _databricksAPIDbREpository.UpdateLogHistoryStatus(processLogHistoryStatus);
                                //Update file loading process status
                                //  await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(jobRunIdDetailsDto.UploadFileId, APIResultStatus.Completed);

                                _logger.LogError($"Updating FileDeletedFromTemp: Writing Databricks catalog failed for this runId {jobRunIdDetailsDto.RunId} with status {jobStatusMessage}");
                                //Update the status of each tab to file deleted from temp folder
                                FlpTabStatus flpTabStatus = new FlpTabStatus()
                                {
                                    // flpConfigurationId = jobRunIdDetailsDto.FlpConfigurationId,
                                    // UploadFileId = jobRunIdDetailsDto.UploadFileId,
                                    tabName = jobRunIdDetailsDto?.TabName,
                                    flpFileLogStatusId = (int)FlpActivityLogStatusEnum.FileDeletedFromTemp,
                                    activityProcessStatusId = (int)FileStatusActivityEnum.Error,
                                    // Message = $"Error in writing databricks catalog: {jobStatusMessage}"
                                };

                                JobRunIdDetailsDtoV2 jobRunIdDetailsDtoV2 = new JobRunIdDetailsDtoV2()
                                {
                                    FlpConfigurationId = jobRunIdDetailsDto.FlpConfigurationId,
                                    UploadFileId = jobRunIdDetailsDto.UploadFileId,
                                    RunId = jobRunIdDetailsDto.RunId,
                                    TabName = jobRunIdDetailsDto.TabName
                                };
                                await LogFileStatus(flpTabStatus, jobRunIdDetailsDtoV2, FileStatusActivityEnum.Processing, "File deletion from temp folder started");
                                await LogFileStatus(flpTabStatus, jobRunIdDetailsDtoV2, FileStatusActivityEnum.ProcessCompleted, "File deleted from temp folder");
                            }
                            else if ((lifeCycleStateId == (int)LifeCycleStateEnum.TERMINATED && resultStateId == (int)ResultStateEnum.FAILED)
                                || (lifeCycleStateId == (int)LifeCycleStateEnum.INTERNAL_ERROR && resultStateId == (int)ResultStateEnum.FAILED))
                            {
                                //Completed
                                //Update runid with success status
                                _logger.LogInformation($"Info:function app Line no 146, Writing Catalog- call status {lifeCycleStateId} , {resultStateId} for flpconfigurationId {jobRunIdDetailsDto.FlpConfigurationId} tabName {jobRunIdDetailsDto.TabName}, calling API");
                                processLogHistoryStatus = new FlpProcessLogHistoryStatusDto()
                                {
                                    LogHistoryId = jobRunIdDetailsDto.LogHistoryId,
                                    FlpConfigurationId = jobRunIdDetailsDto.FlpConfigurationId,
                                    FlpFileLogStatusId = jobRunIdDetailsDto.FlpFileLogStatusId,
                                    ActivityProcessStatusId = (int)FileStatusActivityEnum.Error,
                                    FileUploadedId = jobRunIdDetailsDto.UploadFileId,
                                    Message = $"Writing Databricks catalog: {jobStatusMessage}",
                                    FlpProcessStatusId = (int)FlpProcessStatusEnum.Error,
                                    DatabricksStageId = databricksStageId,
                                    RunId = jobRunIdDetailsDto.RunId,
                                    TerminationDetailsId = terminationDetailsStatusId,
                                    SkipUpdateHistoryStatus = false,
                                    DatabricksAPIResponse = jobRunStatusAPIResponse.ResponseContent,
                                    LifeCycleStateId = lifeCycleStateId,
                                    ResultStateId = resultStateId,
                                    tabName = jobRunIdDetailsDto.TabName
                                };
                                var dbResult1 = await _databricksAPIDbREpository.UpdateLogHistoryStatus(processLogHistoryStatus);
                                //Update file loading process status
                                // await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(jobRunIdDetailsDto.UploadFileId, APIResultStatus.Error);
                               
                            }
                            else
                            {
                                _logger.LogInformation($"Info:function app Line 170, Writing Catalog- no completed call status {lifeCycleStateId} and {resultStateId} for flpconfigurationId {jobRunIdDetailsDto.FlpConfigurationId} tabName {jobRunIdDetailsDto.TabName},  calling API");
                                //Not completed: update only status
                                //Update runid with success status
                                //Will check next time
                                processLogHistoryStatus = new FlpProcessLogHistoryStatusDto()
                                {
                                    LogHistoryId = jobRunIdDetailsDto.LogHistoryId,
                                    FlpConfigurationId = jobRunIdDetailsDto.FlpConfigurationId,
                                    FlpFileLogStatusId = jobRunIdDetailsDto.FlpFileLogStatusId,
                                    ActivityProcessStatusId = (int)FileStatusActivityEnum.Processing,
                                    FileUploadedId = jobRunIdDetailsDto.UploadFileId,
                                    Message = $"Writing Databricks catalog: {jobStatusMessage}",
                                    FlpProcessStatusId = (int)FlpProcessStatusEnum.NotProcessed,
                                    DatabricksStageId = jobRunIdDetailsDto.DatabricksStageId,
                                    RunId = jobRunIdDetailsDto.RunId,
                                    TerminationDetailsId = null,
                                    SkipUpdateHistoryStatus = false,
                                    DatabricksAPIResponse = jobRunStatusAPIResponse.ResponseContent,
                                    LifeCycleStateId = lifeCycleStateId,
                                    ResultStateId = resultStateId,
                                    tabName = jobRunIdDetailsDto.TabName
                                };
                                var dbResult1 = await _databricksAPIDbREpository.UpdateLogHistoryStatus(processLogHistoryStatus);
                            }

                        }



                    }
                    else
                    {

                        _logger.LogInformation($"Info:function app, Writing Catalog- Not found job status for upload file Id line no 196, calling API");
                        //Update runid with success status
                        processLogHistoryStatus = new FlpProcessLogHistoryStatusDto()
                        {
                            LogHistoryId = jobRunIdDetailsDto.LogHistoryId,
                            FlpConfigurationId = jobRunIdDetailsDto.FlpConfigurationId,
                            FlpFileLogStatusId = jobRunIdDetailsDto.FlpFileLogStatusId,
                            ActivityProcessStatusId = (int)FileStatusActivityEnum.Error,
                            FileUploadedId = jobRunIdDetailsDto.UploadFileId,
                            Message = $"Not found job status for upload file Id {jobRunIdDetailsDto.UploadFileId}",
                            FlpProcessStatusId = (int)FlpProcessStatusEnum.Error,
                            DatabricksStageId = null,
                            RunId = jobRunIdDetailsDto.RunId,
                            TerminationDetailsId = null,
                            SkipUpdateHistoryStatus = false,
                            DatabricksAPIResponse = jobRunStatusAPIResponse?.ResponseContent ?? "",
                            LifeCycleStateId = null,
                            ResultStateId = null
                        };
                        var dbResult1 = await _databricksAPIDbREpository.UpdateLogHistoryStatus(processLogHistoryStatus);
                        // await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(jobRunIdDetailsDto.UploadFileId, APIResultStatus.Error);
                    }

                }
                else
                {
                    _logger.LogInformation($"Info:function app, Writing Catalog- Not found details from Job/getrun API for this runId {jobRunIdDetailsDto.RunId} line no 222, calling API");
                    //Skip this condition - will check by function app next time
                    //Error Not fetched job status in history table: update only error status
                    //Update runid with success status
                    processLogHistoryStatus = new FlpProcessLogHistoryStatusDto()
                    {
                        LogHistoryId = jobRunIdDetailsDto.LogHistoryId,
                        FlpConfigurationId = jobRunIdDetailsDto.FlpConfigurationId,
                        FlpFileLogStatusId = jobRunIdDetailsDto.FlpFileLogStatusId,
                        ActivityProcessStatusId = (int)FileStatusActivityEnum.Error,
                        FileUploadedId = jobRunIdDetailsDto.UploadFileId,
                        Message = $"Not found details from Job/getrun API for this runId {jobRunIdDetailsDto.RunId}",
                        FlpProcessStatusId = (int)FlpProcessStatusEnum.Error,
                        DatabricksStageId = null,
                        RunId = jobRunIdDetailsDto.RunId,
                        TerminationDetailsId = null,
                        SkipUpdateHistoryStatus = false,
                        DatabricksAPIResponse = jobRunStatusAPIResponse?.ResponseContent ?? ""
                    };
                    var dbResult1 = await _databricksAPIDbREpository.UpdateLogHistoryStatus(processLogHistoryStatus);
                    // await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(jobRunIdDetailsDto.UploadFileId, APIResultStatus.Error);
                }


                return await Task.FromResult(new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Successfully updated status" },
                    Result = "Success"
                });
            }
            catch (Exception ex)
            {
                processLogHistoryStatus = new FlpProcessLogHistoryStatusDto()
                {
                    LogHistoryId = jobRunIdDetailsDto.LogHistoryId,
                    FlpConfigurationId = jobRunIdDetailsDto.FlpConfigurationId,
                    FlpFileLogStatusId = jobRunIdDetailsDto.FlpFileLogStatusId,
                    ActivityProcessStatusId = (int)FileStatusActivityEnum.Error,
                    FileUploadedId = jobRunIdDetailsDto.UploadFileId,
                    Message = $"Error occurred: Internal server error for this runId {jobRunIdDetailsDto.RunId}",
                    FlpProcessStatusId = (int)FlpProcessStatusEnum.Error,
                    DatabricksStageId = null,
                    RunId = jobRunIdDetailsDto.RunId,
                    TerminationDetailsId = null,
                    SkipUpdateHistoryStatus = false,
                    DatabricksAPIResponse = ""
                };
                var dbResult1 = await _databricksAPIDbREpository.UpdateLogHistoryStatus(processLogHistoryStatus);
                //await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(jobRunIdDetailsDto.UploadFileId, APIResultStatus.Error);
                _logger.LogError(ex.Message.ToString(), "Error occurred: UpdateLogHistoryStatus()");
                return await Task.FromResult(new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Something went wrong" },
                    Result = null
                });
            }
            //finally
            //{
            //    _logger.LogError($"Deleting temp file and updating status for {jobRunIdDetailsDto.FlpConfigurationId} and uploadedFileId{jobRunIdDetailsDto.UploadFileId}");
            //    await DeleteTempFileFromLocation(jobRunIdDetailsDto);
            //    await UpdateProcessStatus(jobRunIdDetailsDto);
            //}
        }

        public async Task<APIResponse<string>> DeleteTempFileFromLocation(JobRunIdDetailsDtoV2 jobRunIdDetailsDto)
        {
            try
            {
                bool isFileDeleted = false;                
                var dbResult = await _databricksAPIDbREpository.GetCurrentTabStatus(jobRunIdDetailsDto.FlpConfigurationId, jobRunIdDetailsDto.UploadFileId);

                if (dbResult == null || !dbResult.Any())
                {
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.Completed,
                        ResponseMessage = new List<string> { "No tab status found" },
                        Result = "Success"
                    };
                }
                //If we got any error in any tab - we will not delete the file & not updated stage to file deleted from temp folder
                var anyError = dbResult.Any(x => x.flpFileLogStatusId != (int)FlpActivityLogStatusEnum.WritingDataBricksCatalog && x.activityProcessStatusId == (int)FileStatusActivityEnum.Error);
                var isAnyFileProcess = dbResult.Any(x => (x.flpFileLogStatusId == (int)FlpActivityLogStatusEnum.WritingDataBricksCatalog &&
                x.activityProcessStatusId == (int)FileStatusActivityEnum.ProcessCompleted) || (x.flpFileLogStatusId == (int)FlpActivityLogStatusEnum.WritingDataBricksCatalog &&
                x.activityProcessStatusId == (int)FileStatusActivityEnum.Error));
                if (anyError && !isAnyFileProcess)
                {
                    _logger.LogWarning($"line no 363 No eligible files for deletion for FlpConfigurationId {jobRunIdDetailsDto.FlpConfigurationId} and UploadedFileId {jobRunIdDetailsDto.UploadFileId}");
                    // No need to delete if not all completed or if any error And update the status of each tab to error if any error found
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.Completed,
                        ResponseMessage = new List<string> { "No eligible files for deletion" },
                        Result = "Success"
                    };
                }
                var allProcessCompleted = dbResult.All(x => x.flpFileLogStatusId == (int)FlpActivityLogStatusEnum.FileDeletedFromTemp &&
                   x.activityProcessStatusId == (int)FileStatusActivityEnum.ProcessCompleted);

                //File should be delete only if all process completed and no error
                if (allProcessCompleted)
                {
                   
                    var tempFileConfig = await _databricksAPIDbREpository.GetTempFileDetails(jobRunIdDetailsDto.FlpConfigurationId, jobRunIdDetailsDto.UploadFileId);


                    if (tempFileConfig != null && !string.IsNullOrWhiteSpace(tempFileConfig.blobName))
                    {
                        var storageInfo = await _fileLoadingProcessConfiguration.DestinationstorageAccountInfo(jobRunIdDetailsDto.FlpConfigurationId);

                        if (storageInfo != null && tempFileConfig.destinationLocationTypeId == (int)DestinationLocationTypeEnum.Azure)
                        {
                            string connectionString = FlpConfigurationHelper.GetBlobConnectionString(storageInfo.FlpStorageAccount, storageInfo.StorageAccountKey);

                            isFileDeleted = await _iBlobStorageService.DeleteTempFileBlobAsync(tempFileConfig.blobName, connectionString, storageInfo.StorageContainerName);
                           
                            // Delete CSV temp file if main file deleted and CSV exists and is large enough
                            if (isFileDeleted && tempFileConfig.fileSize > 50 && !string.IsNullOrWhiteSpace(tempFileConfig.csvTempBlobName))
                            {
                                await _iBlobStorageService.DeleteTempFileBlobAsync(tempFileConfig.csvTempBlobName, connectionString, storageInfo.StorageContainerName);
                            }
                            if(!isFileDeleted)
                             _logger.LogWarning($"Delete Temp File Error occurred: FlpConfigurationId {jobRunIdDetailsDto.FlpConfigurationId} and UploadedFileId {jobRunIdDetailsDto.UploadFileId}");
                            //Update the status of each tab to file deleted from temp folder
                            // var tabs = dbResult.Where(x => x.flpFileLogStatusId == (int)FlpActivityLogStatusEnum.WritingDataBricksCatalog &&
                            //x.flpFileLogStatusId != (int)FlpActivityLogStatusEnum.FileDeletedFromTemp);
                            // foreach (var item in tabs)
                            // {

                            //    // await LogFileStatus(item, jobRunIdDetailsDto, FileStatusActivityEnum.Processing, "File deletion from temp folder started");
                            //     await LogFileStatus(item, jobRunIdDetailsDto,
                            //         (item.activityProcessStatusId == (int)FileStatusActivityEnum.ProcessCompleted & isFileDeleted) ? FileStatusActivityEnum.ProcessCompleted : FileStatusActivityEnum.Error,
                            //         (item.activityProcessStatusId == (int)FileStatusActivityEnum.ProcessCompleted & isFileDeleted) ? "File deleted from temp folder" : "Failed to delete temp file");
                            // }
                            return new APIResponse<string>
                            {
                                ResultStatus = APIResultStatus.Completed,
                                ResponseMessage = new List<string> { "Successfully updated status" },
                                Result = "Success"
                            };
                        }
                        else
                        {
                            _logger.LogWarning($"Delete Temp File Error occurred: info not found for FlpConfigurationId {jobRunIdDetailsDto.FlpConfigurationId} and UploadedFileId {jobRunIdDetailsDto.UploadFileId}");

                            return new APIResponse<string>
                            {
                                ResultStatus = APIResultStatus.Error,
                                ResponseMessage = new List<string> { "Something went wrong" },
                                Result = null
                            };
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"line no 405 Temp file config not found for FlpConfigurationId {jobRunIdDetailsDto.FlpConfigurationId} and UploadedFileId {jobRunIdDetailsDto.UploadFileId}");

                        return new APIResponse<string>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = new List<string> { "Something went wrong" },
                            Result = null
                        };
                    }


                }

                //var eligibleTabs = dbResult.Where(x => x.flpFileLogStatusId == (int)FlpActivityLogStatusEnum.WritingDataBricksCatalog && x.activityProcessStatusId == (int)FileStatusActivityEnum.ProcessCompleted &&
                //          x.flpFileLogStatusId != (int)FlpActivityLogStatusEnum.FileDeletedFromTemp);
                //foreach (var item in eligibleTabs)
                //{
                //    _logger.LogError($"Error occurred: starting condition line inside the loop  isFileDeleted {isFileDeleted} item {item.tabName} any Error - {anyError} allProcessCompleted - {allProcessCompleted} - {jobRunIdDetailsDto.FlpConfigurationId} {jobRunIdDetailsDto.UploadFileId}");
                //    await LogFileStatus(item, jobRunIdDetailsDto, FileStatusActivityEnum.Processing, "File deletion from temp folder started");
                //    await LogFileStatus(item, jobRunIdDetailsDto, FileStatusActivityEnum.ProcessCompleted, "File deleted from temp folder");
                //}
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Successfully updated status" },
                    Result = "Success"
                };


                // Log file status for each eligible tab

                //  _logger.LogError($"Error occurred: starting condition line before the loop  isFileDeleted {isFileDeleted} item count {eligibleTabs.Count()} any Error - {anyError} allProcessCompleted - {allProcessCompleted} - {jobRunIdDetailsDto.FlpConfigurationId} {jobRunIdDetailsDto.UploadFileId}");



            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred: DeleteTempFileFromLocation()");
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Something went wrong" },
                    Result = null
                };
            }
        }

     

        public async Task<APIResponse<string>> UpdateProcessStatus(JobRunIdDetailsDtoV2 jobRunIdDetailsDto)
        {
            try
            {
                var dbResult = await _databricksAPIDbREpository.GetCurrentTabStatus(jobRunIdDetailsDto.FlpConfigurationId, jobRunIdDetailsDto.UploadFileId);

                if (dbResult == null || !dbResult.Any())
                {
                    _logger.LogError($"No tab status found: UpdateProcessStatus() for {jobRunIdDetailsDto.FlpConfigurationId}");
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.Completed,
                        ResponseMessage = new List<string> { "No tab status found" },
                        Result = "Success"
                    };
                }


                var anyInProcess = dbResult.Any(x => x.activityProcessStatusId == (int)FileStatusActivityEnum.Processing);

                if (anyInProcess)
                {
                    _logger.LogInformation($"File is still processing: UpdateProcessStatus() for {jobRunIdDetailsDto.FlpConfigurationId}");
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.Completed,
                        ResponseMessage = new List<string> { "File is still processing" },
                        Result = "Success"
                    };
                }
                var allProcessed = dbResult.All(x => x.flpFileLogStatusId == (int)FlpActivityLogStatusEnum.FileDeletedFromTemp &&
                  x.activityProcessStatusId == (int)FileStatusActivityEnum.ProcessCompleted);
                var anyProcessed = dbResult.Any(x => x.flpFileLogStatusId == (int)FlpActivityLogStatusEnum.FileDeletedFromTemp &&
                    x.activityProcessStatusId == (int)FileStatusActivityEnum.ProcessCompleted);
                var anyError = dbResult.Any(x => x.activityProcessStatusId == (int)FileStatusActivityEnum.Error);

                if (allProcessed)
                {
                    await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(jobRunIdDetailsDto.UploadFileId, FlpProcessStatusEnum.Processed);
                }
                else if (anyProcessed && anyError)
                {
                    await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(jobRunIdDetailsDto.UploadFileId, FlpProcessStatusEnum.PartiallyCompleted);
                }
                else if (!anyProcessed && anyError)
                {
                    await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(jobRunIdDetailsDto.UploadFileId, FlpProcessStatusEnum.Error);
                }

                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Successfully updated status" },
                    Result = "Success"
                };
            }
            catch (Exception ex)
            {
                //await _fileLoadingProcessConfiguration.UpdateFlpProcessStatus(
                //    jobRunIdDetailsDto.UploadFileId, FlpProcessStatusEnum.Error);

                _logger.LogError(ex, "Error occurred: UpdateProcessStatus()");
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Something went wrong" },
                    Result = null
                };
            }
        }



        private async Task LogFileStatus(FlpTabStatus item, JobRunIdDetailsDtoV2 jobRunIdDetailsDto, FileStatusActivityEnum statusEnum, string message)
        {
            await _flpProcessingServiceV4_1.AddFileProcessLosStatus(
                tabName: item.tabName,
                fileType: "excel",
                loginId: "",
                message: message,
                messageType: "info",
                processId: item.processId,
                processName: item.processName,
                tableName: item.tableName,
                totalRows: item.totalRows,
                flpConfigurationId: jobRunIdDetailsDto.FlpConfigurationId,
                fileUploadedId: jobRunIdDetailsDto.UploadFileId,
                statusEnum,
                FlpActivityLogStatusEnum.FileDeletedFromTemp,
                null);
        }


    }
}
