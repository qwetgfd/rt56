using AutoMapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.Models.DTOs;
using Teleperformance.DataIngestion.Models.DTOs.v1;
using Teleperformance.DataIngestion.Models.DTOs.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Services.v1._0
{
    public class StatusService : IStatusService
    {
        private readonly IStatusRepository _statusRepository;
        private readonly ILogger<StatusService> _logger;
        private readonly IHeaderService _headerService;

        public StatusService(IStatusRepository statusRepository, ILogger<StatusService> logger, IHeaderService headerService)
        {
            _statusRepository = statusRepository;
            _logger = logger;  
            _headerService = headerService;
        }
              

        public async Task<APIResponse<IEnumerable<FlpUploadedFileStatusResponseDto?>>> FileUploadStatus()
        {
            var result = await _statusRepository.FlpFileStatus();

            if (result == null || !result.Any())
            {
                _logger.LogError("No data found.");
                return new APIResponse<IEnumerable<FlpUploadedFileStatusResponseDto?>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "No data found" },
                    Result = null
                };
            }
            var flpConfigurationResponseDto = result.GroupBy(y => y.ClientId)
                .Select(f => new FlpUploadedFileStatusResponseDto
                {
                    ClientId = f.Key,
                    ClientName = f.Select(x => x.ClientName).FirstOrDefault(),
                    FileConfigurationStatusList = f.GroupBy(x => x.ConfigurationId)
                    .Select(g => new FileConfigurationStatusDto
                    {
                        FlpConfigurationID = g.Key,
                        FlpConfigurationName = g.Select(x => x.ConfigurationName).FirstOrDefault(),  // Assuming this is included in the result

                        // Map all uploaded files to the UploadedFiles list
                        UploadedFiles = g.Select(r => new UploadedFileStatus
                        {
                            uploadFileId = r.uploadFileId,
                            UploadFileName = r.UploadFileName,
                            DatabaseName = r.DatabaseName,
                            TableName = r.TableName,
                            FileCreationDate = r.FileCreationDate,
                            FileProcessStatusId = r.FileProcessStatusId,
                            TotalRecords = r.TotalRecords,
                            ProcessedRecords = r.ProcessedRecords,
                            DuplicateRecords = r.DuplicateRecords,
                            FileProcessstatusName = r.statusName,
                            CompletionTime = r.CompletionTime,
                            DurationInSeconds = r.DurationInSeconds,
                            Description = r.Description,
                            TabName = r.TabName,
                            fileProcessingServerTypeId = r.fileProcessingServerTypeId
                        }).ToList()  // Convert the result into a list of UploadedFileStatus
                    }).ToList()
                }).ToList();


            //var flpConfigurationResponseDto = result
            //                        .GroupBy(y => y.ClientId)
            //                       .Select(f => new FlpUploadedFileStatusResponseDto
            //                    {
            //                        ClientId = f.Key,
            //                        ClientName = f.Select(x => x.ClientName).FirstOrDefault(),
            //                        FileConfigurationStatusList = f
            //                            .GroupBy(x => x.ConfigurationId)
            //                            .Select(g => new FileConfigurationStatusDto
            //                            {
            //                                FlpConfigurationID = g.Key,
            //                                FlpConfigurationName = g.Select(x => x.ConfigurationName).FirstOrDefault(),

            //                                UploadedFiles = g
            //                                        .GroupBy(r => r.TabName) // or use r.TabName if needed
            //                                        .Select(grp => grp.OrderByDescending(r => r.uploadFileId).FirstOrDefault()) // latest file
            //                                                                                                                        //.Where(r => r != null)
            //                                        .Select(r => new UploadedFileStatus
            //                                        {
            //                                            uploadFileId = r.uploadFileId,
            //                                            UploadFileName = r.UploadFileName,
            //                                            DatabaseName = r.DatabaseName,
            //                                            TableName = r.TableName,
            //                                            FileCreationDate = r.FileCreationDate,
            //                                            FileProcessStatusId = r.FileProcessStatusId,
            //                                            TotalRecords = r.TotalRecords,
            //                                            ProcessedRecords = r.ProcessedRecords,
            //                                            DuplicateRecords = r.DuplicateRecords,
            //                                            FileProcessstatusName = r.statusName,
            //                                            CompletionTime = r.CompletionTime,
            //                                            DurationInSeconds = r.DurationInSeconds,
            //                                            Description = r.Description,
            //                                            TabName = r.TabName // ?? "Unknown"
            //                                        })
            //                                        .ToList()

            //                            })
            //                            .ToList()
            //                    })
            //                    .ToList();


            return new APIResponse<IEnumerable<FlpUploadedFileStatusResponseDto?>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = flpConfigurationResponseDto
            };
        }

        public async Task<APIResponse<FileConfigurationStatusDto?>> FileUploadStatus(StatusRequest statusRequest)
        {
            var result = await _statusRepository.StatusFlpconfigurationID(statusRequest);

            if (result == null || !result.Any())
            {
                _logger.LogError("No data found for ConfigurationId: " + statusRequest.flpConfigurationId);
                return new APIResponse<FileConfigurationStatusDto?>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "No data found for ConfigurationId: " + statusRequest.flpConfigurationId },
                    Result = null
                };
            }

            // Map result to the FlpConfiguration model
            var firstRecord = result.First();  // Assuming result contains multiple statuses for the same file

            var FlpConfigurationResponseDto = new FileConfigurationStatusDto
            {
                FlpConfigurationID = firstRecord.ConfigurationId,
                FlpConfigurationName = firstRecord.ConfigurationName,  // Assuming this is included in the result

                // Map all uploaded files to the UploadedFiles list
                UploadedFiles = result.Select(r => new UploadedFileStatus
                {
                    uploadFileId = r.uploadFileId,
                    UploadFileName = r.UploadFileName,
                    FileCreationDate = r.FileCreationDate,
                    FileProcessStatusId = r.FileProcessStatusId,
                    FileProcessstatusName = r.statusName
                }).ToList()  // Convert the result into a list of UploadedFileStatus
            };

            return new APIResponse<FileConfigurationStatusDto?>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = FlpConfigurationResponseDto
            };
        }

        public async Task<APIResponse<UploadFileStatusResponseDto?>> FileUploadDetailedStatus(FileUploadDetailedStatusRequest fileUploadDetailedStatusRequest)
        {
            var result = await _statusRepository.StatusUploadFileReport(fileUploadDetailedStatusRequest);

            if (result == null || !result.Any())
            {
                _logger.LogError("No data found for ConfigurationId: " + fileUploadDetailedStatusRequest.flpConfigurationId + ", " + "UploadFileId: " + fileUploadDetailedStatusRequest.uploadFileId);

                return new APIResponse<UploadFileStatusResponseDto?>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "No data found for ConfigurationId: " + fileUploadDetailedStatusRequest.flpConfigurationId + ", " + "UploadFileId: " + fileUploadDetailedStatusRequest.uploadFileId },
                    Result = null
                };
            }

            // Map result to the FlpConfiguration model
            var firstRecord = result.First();  // Assuming result contains multiple statuses for the same file

            var uploadFileStatusResponseDto = new UploadFileStatusResponseDto
            {
                FlpConfigurationID = firstRecord.ConfigurationId,
                UploadFileID = firstRecord.uploadFileId,
                UploadFile = firstRecord.UploadFileName,
                DatabaseName = firstRecord.DatabaseName,
                TableName = firstRecord.TableName,
                TotalRecords = firstRecord.TotalRecords,
                ProcessedRecords = firstRecord.ProcessedRecords,
                DuplicateRecords = firstRecord.DuplicateRecords,
                Description = firstRecord.Description,
                TabName = firstRecord.TabName,
                blobName = firstRecord.blobName,
                statusCode = firstRecord.statusCode,
                FileStatus = result.Select(r => new UIdFileStatus
                {
                    StatusName = r.LogStatusName ?? r.ProcessStatusName,
                    Status = r.ProcessStatusName,
                    StatusStartTime = r.StatusStartTime,
                    StatusCompletionTime = r.StatusCompletionTime,
                    DurationInSeconds = r.DurationInSeconds,
                    ProcessStatusId = r.ProcessStatusId,
                    ErrorMessage = r.ErrorMessage,
                    StatusMessage = r.statusMessage

                }).ToList()  // Map each row in the result set to the FileStatus list
            };           
            
            return new APIResponse<UploadFileStatusResponseDto?>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = uploadFileStatusResponseDto
            };
        }

        public async Task<APIResponse<ProcessedFileResponse>> GetProcessedFileList(ProcessedFileListRequestDto request)
        {
            try
            {
                ProcessedFileResponse databaseResponse = null;
                string internalDIFRequest = _headerService.GetHeaderValue("internalDIFRequest");

                if (string.IsNullOrWhiteSpace(internalDIFRequest))
                {
                    databaseResponse = await _statusRepository.GetFlpProcessedFilesListAsyncV2(request);
                }
                else
                {
                    databaseResponse = await _statusRepository.GetFlpProcessedFilesListAsync(request);
                }
                  

                if (databaseResponse != null)
                {
                    return await Task.FromResult(new APIResponse<ProcessedFileResponse>
                    {
                        ResultStatus = APIResultStatus.Completed,
                        ResponseMessage = new List<string> { "Success" },
                        Result = databaseResponse
                    });
                }
                else
                {
                    _logger.LogError("No Data Found");
                    return await Task.FromResult(new APIResponse<ProcessedFileResponse>
                    {
                        ResultStatus = APIResultStatus.NoContent,
                        ResponseMessage = new List<string> { "No Data Found" },
                        Result = null
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return await Task.FromResult(new APIResponse<ProcessedFileResponse>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Internal Server Error" },
                    Result = null
                });
            }
        }
    }
            
}
