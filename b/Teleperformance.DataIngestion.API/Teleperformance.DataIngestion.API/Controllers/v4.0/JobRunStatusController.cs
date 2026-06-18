using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._0.DataBricksAPIJobStatus;

namespace Teleperformance.DataIngestion.API.Controllers.v4._0
{

    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("4.0")]
    [Authorize]
    public class JobRunStatusController : ControllerBase
    {
        public readonly ILogger<JobRunStatusController> _logger;
        private readonly IDataBricksJobStatusService _iDataBricksJobStatusService;
        private readonly ICache _cache;

        public JobRunStatusController(ILogger<JobRunStatusController> logger, IDataBricksJobStatusService iDataBricksJobStatusService, ICache cache)
        {
            _logger = logger;
            _iDataBricksJobStatusService = iDataBricksJobStatusService;
            _cache = cache;
        }

        [Route("GetJobRunId")]
        [HttpGet]
        public async Task<IActionResult> GetJobRunId()
        {
            var result = await _iDataBricksJobStatusService.GetRunIdDetails();
            return StatusCode(result.ResponseCode, result);
        }


        [Route("GetRunIdsForUpdateStatus")]
        [HttpGet]
        public async Task<IActionResult> GetRunIdsForUpdateStatus()
        {
            var result = await _iDataBricksJobStatusService.GetRunIdDetailsForUpdateStatus();
            return StatusCode(result.ResponseCode, result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="jobRunIdDetailsDto"></param>
        /// <returns></returns>

        [Route("UpdateJobStatus")]
        [HttpPost]
        public async Task<IActionResult> UpdateJobStatus(JobRunIdDetailsDto jobRunIdDetailsDto)
        {
            
            var result1 = await _iDataBricksJobStatusService.UpdateLogHistoryStatus(jobRunIdDetailsDto);
            return StatusCode(result1.ResponseCode, result1);
        }



        /// <summary>
        /// if the file is not deleted from the temp location after the processing then we can call this api to delete the file from the temp location
        /// </summary>
        /// <param name="jobRunIdDetailsDto"></param>
        /// <returns></returns>
        [Route("DeleteTempFile")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        public async Task<IActionResult> DeleteTempFile(JobRunIdDetailsDtoV2 jobRunIdDetailsDto)
        {
            var result = await _iDataBricksJobStatusService.DeleteTempFileFromLocation(jobRunIdDetailsDto);
            return StatusCode(result.ResponseCode, result);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="jobRunIdDetailsDto"></param>
        /// <returns></returns>

        [Route("UpdateProcessStatus")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        public async Task<IActionResult> UpdateProcessStatus(JobRunIdDetailsDtoV2 jobRunIdDetailsDto)
        {           
            var result = await _iDataBricksJobStatusService.UpdateProcessStatus(jobRunIdDetailsDto);
            return StatusCode(result.ResponseCode, result);
        }

    }
}
