using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Validations;
using Newtonsoft.Json;
using System.Data;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.DataAssists;
using NPOI.OpenXmlFormats.Wordprocessing;
using Newtonsoft.Json.Linq;
using System.Net;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1.DataAssists;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService;

namespace Teleperformance.DataIngestion.API.Controllers.v4._1
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("4.1")]
    [Authorize]
    public class DataAssistsController : ControllerBase
    {
        private readonly ILogger<DataAssistsController> _logger;
        private readonly IDataValidationServiceV4_1 service;

        public DataAssistsController(
            ILogger<DataAssistsController> logger,
            IDataValidationServiceV4_1 service
            )
        {
            this._logger = logger;
            this.service = service;
        }
        [HttpPost("GenerateResponse")]
        public async Task<IActionResult> GenerateResponse([FromForm] ProjectQueryFormRequest request)
        {
            await Task.Delay(3000);
            _logger.LogError($"GenerateResponse ApI called  at line no 40");
            var result = await service.GenerateResponse(request);
            _logger.LogError($"GenerateResponse ApI called response  at line no 42");
            //return StatusCode(result.ResponseCode, result);
            return Content(result, "application/json");
            //return result;
        }

        [HttpPost("GenerateResponse2")]
        public async Task<IActionResult> GenerateResponse2([FromForm] ProjectQueryFormRequest request)
        {
            var result = await service.GenerateResponse2(request);
            //return StatusCode(result.ResponseCode, result);
            //return Content(result, "application/json");
            return Content(System.Text.Json.JsonSerializer.Serialize(result), "application/json");
            //return result;
        }



    }
}
