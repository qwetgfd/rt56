using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService;

namespace Teleperformance.DataIngestion.API.Controllers.v3._0
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("3.0")]
    [Authorize]
    public class DataSliceAPIController : ControllerBase
    {
        public readonly ILogger<ImportController> _logger;
        private readonly IFileProcessService _fileLoadingProcess;
        private readonly ICache _cache;
        private readonly IChangeProcessStatusService _changeProcessStatusService;
        private readonly IDataSliceAPIService _iDataSliceApiService;

        public DataSliceAPIController(ILogger<ImportController> logger, IFileProcessService fileLoadingProcess, ICache cache, IChangeProcessStatusService changeProcessStatusService, IDataSliceAPIService iDataSliceApiService)
        {
            _logger = logger;
            _fileLoadingProcess = fileLoadingProcess;
            _cache = cache;
            _changeProcessStatusService = changeProcessStatusService;
            _iDataSliceApiService = iDataSliceApiService;
        }


        [Route("FillCache")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        public async Task<IActionResult> FillCache()
        {
            //Cleared cache
            var resut = await _iDataSliceApiService.ClearCache();  
            //Filled cache
            var result = await _iDataSliceApiService.FillDataInCache();
            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetRegionsBySecurityGroup")]
        [HttpGet]
        public async Task<IActionResult> GetRegionBySecurityGroup()
        {
            var result = await _iDataSliceApiService.GetRegionBySecurityGroup();
            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetSubRegions")]
        [HttpGet]
        public async Task<IActionResult> GetSubRegion()
        {
            var result = await _iDataSliceApiService.GetSubRegion();
            return StatusCode(result.ResponseCode, result);
        }


        [Route("GetClients")]
        [HttpGet]
        public async Task<IActionResult> GetClients()
        {
            var result = await _iDataSliceApiService.GetClient();
            return StatusCode(result.ResponseCode, result);
        }


        [Route("ClearCache")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        public async Task<IActionResult> ClearCache()
        {
            var resut = await _iDataSliceApiService.ClearCache();
            return StatusCode(resut.ResponseCode, resut);
        }

    }
}
