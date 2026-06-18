using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;

namespace Teleperformance.DataIngestion.API.Controllers.v4._1
{

    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("4.1")]
    [Authorize]
    public class ImportController : ControllerBase
    {
        public readonly ILogger<ImportController> _logger;
        private readonly IFileProcessServiceV4_1 _fileLoadingProcess;
        public ImportController(ILogger<ImportController> logger, IFileProcessServiceV4_1 fileLoadingProcess)
        {
            _logger = logger;
            _fileLoadingProcess = fileLoadingProcess;
        }        

        [Route("ProcessExcelFile")]
        [HttpPost]
        public async Task<IActionResult> ProcessExcelFile(FlpRequestDto4_1 flpRequestDto)
        {
            var result = await _fileLoadingProcess.ProcessExcelFile(flpRequestDto);
            return StatusCode(result.ResponseCode, result);
        }


        [Route("FileMoveToLandingLayer")]
        [HttpPost]
        public async Task<IActionResult> FileMoveToLandingLayer(FlpRequestDto4_1 flpRequestDto)
        {
            var result = await _fileLoadingProcess.ProcessLandingLayerFile(flpRequestDto);
            return StatusCode(result.ResponseCode, result);
        }

    }
}
