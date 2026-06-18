using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus;

namespace Teleperformance.DataIngestion.API.Controllers.v1._0
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("1.0")]
    [Authorize]
public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
        _dashboardService = dashboardService;
        }

        [HttpPost("GetProcessList")]
        public async Task<IActionResult> GetProcessList(GetProcessListRequest getProcessListRequest)
        {
            var result = await _dashboardService.GetProcessList(getProcessListRequest);
            return StatusCode(result.ResponseCode, result);
        }

        [HttpPost("GetFileList")]
        public async Task<IActionResult> GetFileList(GetFileListRequest getFileListRequest)
        {
            var result = await _dashboardService.GetFileList(getFileListRequest);
            return StatusCode(result.ResponseCode, result);
        }

        [HttpPost("GetClientList")]
        public async Task<IActionResult> GetClientList(GetFileListRequest getClientListRequest)
        {
            var result = await _dashboardService.GetClientList(getClientListRequest);
            return StatusCode(result.ResponseCode, result);
        }

        [HttpPost("DashboardRealTimeProcessingStatusList")]
        public async Task<IActionResult> DashboardRealTimeProcessingStatusList(GetFileListRequest getDashboardStatusList)
        {
            var result = await _dashboardService.DashboardRealTimeProcessingStatusList(getDashboardStatusList);
            return StatusCode(result.ResponseCode, result);
        }

        [HttpPost("CountFileUploadsByProcessType")]
        public async Task<IActionResult> CountFileUploadsByProcessType(GetFileListRequest countFileUploadsByProcessTypeRequest)
        {
            var result = await _dashboardService.CountFileUploadsByProcessType(countFileUploadsByProcessTypeRequest);
            return StatusCode(result.ResponseCode, result);
        }

        [HttpGet("DIFrameworkUtilization")]
        public async Task<IActionResult> DIFrameworkUtilization()
        {
            var result = await _dashboardService.DIFrameworkUtilization();
            return StatusCode(result.ResponseCode, result);
        }

        [HttpGet("UtilizationByRegions")]
        public async Task<IActionResult> UtilizationByRegions()
        {
            var result = await _dashboardService.GetUtilizationByRegionList();
            return StatusCode(result.ResponseCode, result);
        }
}
    
}
