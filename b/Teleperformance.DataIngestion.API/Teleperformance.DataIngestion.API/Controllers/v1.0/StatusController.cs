using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs;
using Teleperformance.DataIngestion.Models.DTOs.v1;
using Teleperformance.DataIngestion.Models.DTOs.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.API.Controllers.v1._0
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("1.0")]
    [Authorize]
    public class StatusController : ControllerBase
    {
        private readonly IStatusService _statusService;

        public StatusController(IStatusService statusService)
        {
            _statusService = statusService;
        }

        [HttpGet("FileUploadStatus")]
        public async Task<IActionResult> FileUploadStatus()
        {
            var result = await _statusService.FileUploadStatus();
            return StatusCode(result.ResponseCode, result);
        }

        [HttpPost("FileUploadStatusByProcessId")]
        public async Task<IActionResult> FileUploadStatusByProcessId(StatusRequest statusRequest)
        {
            var result = await _statusService.FileUploadStatus(statusRequest);
            return StatusCode(result.ResponseCode, result);
        }

        [HttpPost("FileUploadDetailedStatus")]
        public async Task<IActionResult> FileUploadDetailedStatus(FileUploadDetailedStatusRequest fileUploadDetailedStatusRequest)
        {
            var result = await _statusService.FileUploadDetailedStatus(fileUploadDetailedStatusRequest);
            return StatusCode(result.ResponseCode, result);
        }

        [HttpPost("GetProcessedFileList")]
        public async Task<IActionResult> ProcessedFileList(ProcessedFileListRequestDto request)
        {
            var result = await _statusService.GetProcessedFileList(request);
            return StatusCode(result.ResponseCode, result);
        }


    }

   
}
