using Azure;
using Microsoft.Extensions.Logging;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Services.v1._0
{
    public class DashboardService : IDashboardService
    {
        private readonly IDashboardRepository _dashboardRepository;
        private readonly ILogger<DashboardService> _logger;
        private readonly IHeaderService _headerService;
        public DashboardService(IDashboardRepository dashboardRepository, ILogger<DashboardService> logger,IHeaderService headerService)
        {
            _dashboardRepository = dashboardRepository;
            _logger = logger;
            _headerService = headerService;
        }
        
        public async Task<APIResponse<GetProcessListResponse?>> GetProcessList(GetProcessListRequest getProcessListRequest)
        {
            // Ensure that the request object is not null and that the integer properties are properly set
            if (getProcessListRequest == null && getProcessListRequest.RegionId <= 0 && getProcessListRequest.SubRegionId == null && getProcessListRequest.SubRegionId =="")
            {
                _logger.LogError("Invalid input for RegionId or SubRegionId. RegionId: " + getProcessListRequest.RegionId + ", SubRegionId: " + getProcessListRequest.SubRegionId);

                return new APIResponse<GetProcessListResponse?>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Invalid input for RegionId or SubRegionId" },
                    Result = null
                };
            }
            getProcessListRequest.securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
            
            // Fetch the row count from the repository (assumed to return an integer)
            var processRowCount = await _dashboardRepository.GetProcessList(getProcessListRequest);

            // Check if the result is valid (shouldn't be less than 0)
            if (processRowCount.ProcessRowCount < 0)
            {
                _logger.LogError("Error: Negative row count received for RegionId: " + getProcessListRequest.RegionId + ", SubRegionId: " + getProcessListRequest.SubRegionId);

                return new APIResponse<GetProcessListResponse?>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "No valid data found for RegionId: " + getProcessListRequest.RegionId + ", SubRegionId: " + getProcessListRequest.SubRegionId },
                    Result = null  // Return 0 as row count if no valid data is found
                };
            }

            // Log the successful execution
            _logger.LogInformation($"Process row count: {processRowCount} for RegionId: {getProcessListRequest.RegionId}, SubRegionId: {getProcessListRequest.SubRegionId}");

            // Return success response with the row count as the result
            return new APIResponse<GetProcessListResponse?>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = processRowCount // Return the row count
            };
        }


        public async Task<APIResponse<GetFileListResponse?>> GetFileList(GetFileListRequest getFileListRequest)
        {
            // Ensure that the request object is not null and that the integer properties are properly set
            if (getFileListRequest == null && getFileListRequest.RegionId <= 0 && getFileListRequest.SubRegionId == null && getFileListRequest.SubRegionId == "")
            {
                _logger.LogError("Invalid input for RegionId or SubRegionId. RegionId: " + getFileListRequest.RegionId + ", SubRegionId: " + getFileListRequest.SubRegionId);

                return new APIResponse<GetFileListResponse?>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Invalid input for RegionId or SubRegionId" },
                    Result = null
                };
            }
            getFileListRequest.securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
            // Fetch the row count from the repository (assumed to return an integer)
            var processRowCount = await _dashboardRepository.GetFileList(getFileListRequest);

            // Check if the result is valid (shouldn't be less than 0)
            if (processRowCount?.totalUploadedFiles < 0)
            {
                _logger.LogError("Error: Negative row count received for RegionId: " + getFileListRequest.RegionId + ", SubRegionId: " + getFileListRequest.SubRegionId);

                return new APIResponse<GetFileListResponse?>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "No valid data found for RegionId: " + getFileListRequest.RegionId + ", SubRegionId: " + getFileListRequest.SubRegionId },
                    Result = null  // Return 0 as row count if no valid data is found
                };
            }

            // Log the successful execution
            _logger.LogInformation($"Process row count: {processRowCount} for RegionId: {getFileListRequest.RegionId}, SubRegionId: {getFileListRequest.SubRegionId}");

            // Return success response with the row count as the result
            return new APIResponse<GetFileListResponse?>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = processRowCount // Return the row count
            };

        }

        public async Task<APIResponse<GetClientListResponse?>> GetClientList(GetFileListRequest getClientListRequest)
         {
            // Ensure that the request object is not null and that the integer properties are properly set
            if (getClientListRequest == null && getClientListRequest.RegionId <= 0 && getClientListRequest.SubRegionId == null && getClientListRequest.SubRegionId == "")
            {
                _logger.LogError("Invalid input for RegionId or SubRegionId. RegionId: " + getClientListRequest.RegionId + ", SubRegionId: " + getClientListRequest.SubRegionId);

                return new APIResponse<GetClientListResponse?>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Invalid input for RegionId or SubRegionId" },
                    Result = null
                };
            }

            getClientListRequest.securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
            // Fetch the row count from the repository (assumed to return an integer)
            var totalClientRowCount = await _dashboardRepository.GetClientList(getClientListRequest);

            // Check if the result is valid (shouldn't be less than 0)
            if (totalClientRowCount.totalClients < 0)
            {
                _logger.LogError("Error: Negative row count received for RegionId: " + getClientListRequest.RegionId + ", SubRegionId: " + getClientListRequest.SubRegionId);

                return new APIResponse<GetClientListResponse?>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "No valid data found for RegionId: " + getClientListRequest.RegionId + ", SubRegionId: " + getClientListRequest.SubRegionId },
                    Result = null  // Return 0 as row count if no valid data is found
                };
            }

            // Log the successful execution
            _logger.LogInformation($"Process row count: {totalClientRowCount} for RegionId: {getClientListRequest.RegionId}, SubRegionId: {getClientListRequest.SubRegionId}");

            // Return success response with the row count as the result
            return new APIResponse<GetClientListResponse?>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = totalClientRowCount // Return the row count
            };

        }

        public async Task<APIResponse<IEnumerable<DashboardRealTimeProcessingResponse?>>> DashboardRealTimeProcessingStatusList(GetFileListRequest getDashboardStatusList)
        {
            // Ensure that the request object is not null and that the integer properties are properly set
            if (getDashboardStatusList == null && getDashboardStatusList.RegionId <= 0 && getDashboardStatusList.SubRegionId == null && getDashboardStatusList.SubRegionId == "")
            {
                _logger.LogError("Invalid input for RegionId or SubRegionId. RegionId: " + getDashboardStatusList.RegionId + ", SubRegionId: " + getDashboardStatusList.SubRegionId);

                return new APIResponse<IEnumerable<DashboardRealTimeProcessingResponse?>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Invalid input for RegionId or SubRegionId" },
                    Result = null
                };
            }
            getDashboardStatusList.securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
            // Fetch the row count from the repository (assumed to return an integer)
            var realTimeProcessingResponse = await _dashboardRepository.DashboardRealTimeProcessingStatusList(getDashboardStatusList);

            // Check if the result is valid (shouldn't be less than 0)
            if (realTimeProcessingResponse is null)
            {
                _logger.LogError("Error: Invalid input received for RegionId: " + getDashboardStatusList.RegionId + ", SubRegionId: " + getDashboardStatusList.SubRegionId);

                return new APIResponse<IEnumerable<DashboardRealTimeProcessingResponse?>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "No valid data found for RegionId: " + getDashboardStatusList.RegionId + ", SubRegionId: " + getDashboardStatusList.SubRegionId },
                    Result = null  // Return 0 as row count if no valid data is found
                };
            }

            // Log the successful execution
            _logger.LogInformation($"Process row count: {realTimeProcessingResponse} for RegionId: {getDashboardStatusList.RegionId}, SubRegionId: {getDashboardStatusList.SubRegionId}");

            // Return success response with the row count as the result
            return new APIResponse<IEnumerable<DashboardRealTimeProcessingResponse?>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = realTimeProcessingResponse // Return the row count
            };

        }


        public async Task<APIResponse<IEnumerable<CountFileUploadsByProcessTypeResponse?>>> CountFileUploadsByProcessType(GetFileListRequest countFileUploadsByProcessTypeRequest)
        {
            // Ensure that the request object is not null and that the integer properties are properly set
            if (countFileUploadsByProcessTypeRequest == null && countFileUploadsByProcessTypeRequest.RegionId <= 0 && countFileUploadsByProcessTypeRequest.SubRegionId == null && countFileUploadsByProcessTypeRequest.SubRegionId == "")
            {
                _logger.LogError("Invalid input for RegionId or SubRegionId. RegionId: " + countFileUploadsByProcessTypeRequest.RegionId + ", SubRegionId: " + countFileUploadsByProcessTypeRequest.SubRegionId);

                return new APIResponse<IEnumerable<CountFileUploadsByProcessTypeResponse?>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Invalid input for RegionId or SubRegionId" },
                    Result = null
                };
            }
            countFileUploadsByProcessTypeRequest.securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
            // Fetch the row count from the repository (assumed to return an integer)
            var realTimeProcessingResponse = await _dashboardRepository.CountFileUploadsByProcessType(countFileUploadsByProcessTypeRequest);

            // Check if the result is valid (shouldn't be less than 0)
            if (realTimeProcessingResponse is null)
            {
                _logger.LogError("Error: Invalid input received for RegionId: " + countFileUploadsByProcessTypeRequest.RegionId + ", SubRegionId: " + countFileUploadsByProcessTypeRequest.SubRegionId);

                return new APIResponse<IEnumerable<CountFileUploadsByProcessTypeResponse?>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "No valid data found for RegionId: " + countFileUploadsByProcessTypeRequest.RegionId + ", SubRegionId: " + countFileUploadsByProcessTypeRequest.SubRegionId },
                    Result = null  // Return 0 as row count if no valid data is found
                };
            }

            // Log the successful execution
            _logger.LogInformation($"Process row count: {realTimeProcessingResponse} for RegionId: {countFileUploadsByProcessTypeRequest.RegionId}, SubRegionId: {countFileUploadsByProcessTypeRequest.SubRegionId}");

            // Return success response with the row count as the result
            return new APIResponse<IEnumerable<CountFileUploadsByProcessTypeResponse?>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = realTimeProcessingResponse // Return the row count
            };

        }


        public async Task<APIResponse<IEnumerable<DIFrameworkUtilizationResponse?>>> DIFrameworkUtilization()
        {

            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
            // Fetch the row count from the repository (assumed to return an integer)
            var DIFrameworkUtilizationResponse = await _dashboardRepository.DIFrameworkUtilization(securityGroupId);

            // Check if the result is valid (shouldn't be less than 0)
            if (DIFrameworkUtilizationResponse is null)
            {
                _logger.LogError("Error: DIFrameworkUtilization Response is null ");

                return new APIResponse<IEnumerable<DIFrameworkUtilizationResponse?>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "DIFrameworkUtilization Response is null" },
                    Result = null  // Return 0 as row count if no valid data is found
                };
            }

            // Log the successful execution
            _logger.LogInformation($"DIFrameworkUtilization Response is null");

            // Return success response with the row count as the result
            return new APIResponse<IEnumerable<DIFrameworkUtilizationResponse?>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = DIFrameworkUtilizationResponse // Return the row count
            };

        }


        public async Task<APIResponse<IEnumerable<UtilizationByRegionResponseDto?>>> GetUtilizationByRegionList()
        {

            IEnumerable<UtilizationByRegionResponseDto> result;
            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
            // Fetch the row count from the repository (assumed to return an integer)
            var dbResponse = await _dashboardRepository.GetUtilizationByRegionList(securityGroupId);
            // Check if the result is valid (shouldn't be less than 0)
            if (dbResponse is null)
            {
                _logger.LogError("Error: DIFrameworkUtilization Response is null ");

                return new APIResponse<IEnumerable<UtilizationByRegionResponseDto?>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "DIFrameworkUtilization Response is null" },
                    Result = null  // Return 0 as row count if no valid data is found
                };
            }

            result = dbResponse.Select(dr=> new UtilizationByRegionResponseDto
            {
                RegionId = dr.regionId,
                RegionName = dr.regionName,
                TotalFileCount = dr.totalFileCount
            });

            // Return success response with the row count as the result
            return new APIResponse<IEnumerable<UtilizationByRegionResponseDto?>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = result // Return the row count
            };

        }


    }
}
