using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;

namespace Teleperformance.DataIngestion.API.Controllers.v3._0
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("3.0")]
    [Authorize]
    public class ImportController : ControllerBase
    {
        
        public readonly ILogger<ImportController> _logger;        
        private readonly IFileProcessService _fileLoadingProcess;
        private readonly ICache _cache;
        private readonly IChangeProcessStatusService _changeProcessStatusService;

        public ImportController(ILogger<ImportController> logger, IFileProcessService fileLoadingProcess, ICache cache, IChangeProcessStatusService changeProcessStatusService)
        {            
            _logger = logger;
            _fileLoadingProcess = fileLoadingProcess;
            _cache = cache;
            _changeProcessStatusService = changeProcessStatusService;
        }




        [Route("ProcessTxtFile")]
        [HttpPost]
        public async Task<IActionResult> ProcessTxtFile(FlpRequestDto flpRequestDto)
        {
            var result = await _fileLoadingProcess.ProcessTxtFile(flpRequestDto);
            return StatusCode(result.ResponseCode, result);
        }

        [Route("ProcessCsvFile")]
        [HttpPost]
        public async Task<IActionResult> ProcessCsvFile(FlpRequestDto flpRequestDto)
        {
            var result = await _fileLoadingProcess.ProcessCsvFile(flpRequestDto);
            return StatusCode(result.ResponseCode, result);
        }


        [Route("ProcessExcelFile")]
        [HttpPost]
        public async Task<IActionResult> ProcessExcelFile(FlpRequestDto flpRequestDto)
        {
            var result = await _fileLoadingProcess.ProcessExcelFile(flpRequestDto);
            return StatusCode(result.ResponseCode, result);
        }


        [Route("UpdateProcessStatus")]
        [HttpPost]
        public async Task<IActionResult> UpdateProcessStatus()
        {
            var result = await _changeProcessStatusService.UpdateProcessStatus();
            return StatusCode(result.ResponseCode, result);
        }
    }
}
