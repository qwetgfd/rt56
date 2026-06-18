using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;

namespace Teleperformance.DataIngestion.API.Controllers.v1._0
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("1.0")]
    [Authorize]
    public class ImportController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        public readonly ILogger<ImportController> _logger;
        private readonly IFileLoadingProcessConfiguration _fileLoadingProcessConfiguration;
        private readonly IFileLoadingProcessService _fileLoadingProcess;
        private readonly ICache _cache;

        public ImportController(IConfiguration configuration, ILogger<ImportController> logger, IFileLoadingProcessConfiguration fileLoadingProcessConfiguration, IFileLoadingProcessService fileLoadingProcess, ICache cache)
        {
            _configuration = configuration;
            _logger = logger;
            _fileLoadingProcessConfiguration = fileLoadingProcessConfiguration;
            _fileLoadingProcess = fileLoadingProcess;
            _cache = cache;

        }
        [Route("GetProcessList")]
        [HttpPost]
        public async Task<IActionResult> GetProcessList(FlpProcessRequestDto flpProcessRequestDto)
        {
            var result = await _fileLoadingProcessConfiguration.GetProcessList(flpProcessRequestDto.ProcessTypeId);
            return StatusCode(result.ResponseCode, result);
        }


        //[Route("GetProcessListToLandingLayer")]
        //[HttpPost]
        //public async Task<IActionResult> GetProcessListToLandingLayer(FlpProcessRequestDto flpProcessRequestDto)
        //{
        //    var result = await _fileLoadingProcessConfiguration.GetProcessListToLandingLayer(flpProcessRequestDto.ProcessTypeId);
        //    return StatusCode(result.ResponseCode, result);
        //}



        [Route("GetProcessListToLandingLayer")]
        [HttpGet]
        public async Task<IActionResult> GetProcessListToLandingLayer([FromQuery] int processTypeId)
        {
            var result = await _fileLoadingProcessConfiguration.GetProcessListToLandingLayer(processTypeId);
            return StatusCode(result.ResponseCode, result);
        }


        [Route("ProcessCsvFile")]
        [HttpPost]
        public async Task<IActionResult> ProcessCsvFile(FlpRequestDto flpRequestDto)
        {
            var result = await _fileLoadingProcess.ProcessCsvFile(flpRequestDto);
            return StatusCode(result.ResponseCode, result);
        }



        [Route("ProcessTxtFile")]
        [HttpPost]
        public async Task<IActionResult> ProcessTxtFile(FlpRequestDto flpRequestDto)
        {
            var result = await _fileLoadingProcess.ProcessTxtFile(flpRequestDto);
            return StatusCode(result.ResponseCode, result);
        }


        [Route("ProcessExcelFile")]
        [HttpPost]
        public async Task<IActionResult> ProcessExcelFile(FlpRequestDto flpRequestDto)
        {
            var result = await _fileLoadingProcess.ProcessExcelFile(flpRequestDto);
            return StatusCode(result.ResponseCode, result);
        }


        [Route("UpdateProcessSchedulerLastDate")]
        [HttpPost]
        public async Task<IActionResult> UpdateProcessSchedulerLastDate(FlpProcessProcessSchedulerLastdate processProcessSchedulerLastdate)
        {
            var result = await _fileLoadingProcess.UpdateProcessSchedulerLastDate(processProcessSchedulerLastdate.FlpConfigurationId);
            return StatusCode(result.ResponseCode, result);
        }


       




    }
}
