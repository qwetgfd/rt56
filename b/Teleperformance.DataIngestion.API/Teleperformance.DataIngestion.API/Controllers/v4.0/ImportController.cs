using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;

namespace Teleperformance.DataIngestion.API.Controllers.v4._0
{

    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("4.0")]
    [Authorize]
    public class ImportController : ControllerBase
    {

        public readonly ILogger<ImportController> _logger;
        private readonly IFileProcessServiceV4 _fileLoadingProcess;
        private readonly ICache _cache;

        public ImportController(ILogger<ImportController> logger, IFileProcessServiceV4 fileLoadingProcess, ICache cache)
        {
            _logger = logger;
            _fileLoadingProcess = fileLoadingProcess;
            _cache = cache;
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


    }
}
