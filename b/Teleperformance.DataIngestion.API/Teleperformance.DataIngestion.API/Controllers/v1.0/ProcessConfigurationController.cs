using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using Teleperformance.DataIngestion.DataAccess.Interfaces;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0.Authentication;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Enums.v3._0;

namespace Teleperformance.DataIngestion.API.Controllers.v1._0
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("1.0")]
    [Authorize]
    public class ProcessConfigurationController : ControllerBase
    {
        private readonly IProcessConfigurationService _processConfigurationService;
        private readonly ILogger<ProcessConfigurationController> _logger;

        public ProcessConfigurationController(
            IProcessConfigurationService processConfigurationService,
            ILogger<ProcessConfigurationController> logger)
        {
            _processConfigurationService = processConfigurationService;
            _logger = logger;
        }

        [Route("GetAllProcessNamesByLoginId")]
        [HttpGet]
        public async Task<IActionResult> GetAllProcessNamesByLoginId(string loginid)
        {
            var result = await _processConfigurationService.GetAllProcessNamesByLoginId(loginid);
            return StatusCode(result.ResponseCode, result);

        }

        [Route("ProcessNameExists")]
        [HttpGet]
        public async Task<IActionResult> CheckProcessNameExists([FromQuery] string processName, string? configId)
        {
            var result = await _processConfigurationService.CheckProcessNameExists(processName, configId);
            return StatusCode(result.ResponseCode, result);

        }

        [Route("GetAllDataTypeNames")]
        [HttpGet]
        public async Task<IActionResult> GetAllDataTypeNames()
        {
            var result = await _processConfigurationService.GetAllDataTypeNames();
            return StatusCode(result.ResponseCode, result);

        }

        [Route("GetAllDataTimeFormats")]
        [HttpGet]
        public async Task<IActionResult> GetAllDateTimeFormats(bool displayOnLandingLayer)
        {
            var result = await _processConfigurationService.GetDateTimeFormats(displayOnLandingLayer);
            return StatusCode(result.ResponseCode, result);

        }

        [Route("GetAllDIRegions")]
        [HttpGet]
        public async Task<IActionResult> GetAllDIRegions()
        {
            var result = await _processConfigurationService.GetAllDIRegions();
            return StatusCode(result.ResponseCode, result);

        }

        [Route("GetAllDISubRegions")]
        [HttpGet]
        public async Task<IActionResult> GetAllDISubRegions()
        {
            var result = await _processConfigurationService.GetAllDISubRegions();
            return StatusCode(result.ResponseCode, result);

        }

        [Route("GetAllDIClientnames")]
        [HttpGet]
        public async Task<IActionResult> GetAllDIClientnames()
        {
            var result = await _processConfigurationService.GetAllDIClientnames();
            return StatusCode(result.ResponseCode, result);

        }

        [Route("GetDatabaseNames")]
        [HttpGet]
        public async Task<IActionResult> GetDatabaseNames(int regionId, string subRegionId, int clientNameId, int fileProcessingServerTypeId = 1)
        {
            var result = await _processConfigurationService.GetDatabaseNames(regionId, subRegionId, clientNameId, fileProcessingServerTypeId);
            return StatusCode(result.ResponseCode, result);

        }

        [Route("GetConfigurationById")]
        [HttpGet]
        public async Task<IActionResult> GetConfigurationById(string id)
        {
            var result = await _processConfigurationService.GetConfigurationById(id);
            return StatusCode(result.ResponseCode, result);

        }

        [HttpGet("GetProcessType")]
        public async Task<IActionResult> GetProcessType()
        {
            var result = await _processConfigurationService.GetProcessType();
            return StatusCode(result.ResponseCode, result);
        }

        [Route("InsertConfiguration")]
        [HttpPost, RequestSizeLimit(350_000_000)]
        public async Task<IActionResult> InsertConfiguration()
        {
            _logger.LogInformation("InsertConfiguration API Called!");
            try
            {
                var formCollection = await Request.ReadFormAsync();
                if (formCollection.Files.Count() != 2)
                {
                    return BadRequest();
                }
                _logger.Log(LogLevel.Information, $"formCollection read success {formCollection["UserName"].ToString()}");
                var file = formCollection.Files.First();
                var stream = file.OpenReadStream();
                var loggedInUser = formCollection["LoggedInUser"].ToString();
                var userName = formCollection["UserName"].ToString();
                string json = string.Empty;
                using (var sr = new StreamReader(formCollection.Files[1].OpenReadStream()))
                {
                    json = sr.ReadToEnd();
                }
                _logger.Log(LogLevel.Information, $"File read success {formCollection["UserName"].ToString()}");
                var result = await _processConfigurationService.InsertConfiguration(json, file, stream, loggedInUser, userName);
                _logger.Log(LogLevel.Information, $"All process executed success {formCollection["UserName"].ToString()}");
                return StatusCode(result.ResponseCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in InsertConfiguration " + ex.Message, ex.InnerException);
                throw;
            }
        }

        [HttpPost("GetFileProcessConfigurationList")]
        public async Task<IActionResult> FileProcessConfigurationList(FlpProcessConfigurationListRequest request)
        {
            var result = await _processConfigurationService.GetFlpProcessConfigurationList(request);
            return StatusCode(result.ResponseCode, result);
        }

        [HttpGet("GetfileServerDetails")]
        public async Task<IActionResult> GetfileServerDetails()
        {
            var result = await _processConfigurationService.GetfileServerDetails();
            return StatusCode(result.ResponseCode, result);
        }

        [HttpGet("GetstorageAccountDetails")]
        public async Task<IActionResult> GetstorageAccountDetails(int fileProcessingServerTypeId)
        {
            var result = await _processConfigurationService.GetstorageAccountDetails(fileProcessingServerTypeId);
            return StatusCode(result.ResponseCode, result);
        }

        [HttpPost("InsertFlpConfigurationDetails")]
        public async Task<IActionResult> InsertFlpConfigurationDetails(InsertFlpConfigurationRequest insertFlpConfigurationRequest)
        {
            var result = await _processConfigurationService.InsertFlpConfigurationDetails(insertFlpConfigurationRequest);
            return StatusCode(result.ResponseCode, result);
        }
        [Route("GetConfigurationDetailsById")]
        [HttpGet]
        public async Task<IActionResult> GetConfigurationDetailsById(string configurationId, string? tabName)
        {
            var result = await _processConfigurationService.GetConfigurationDetailsById(configurationId, tabName);
            return StatusCode(result.ResponseCode, result);

        }

        [HttpGet("GetSchedulerType")]
        public async Task<IActionResult> GetSchedulerType()
        {
            var result = await _processConfigurationService.GetSchedulerType();
            return StatusCode(result.ResponseCode, result);
        }
        [HttpGet("GetWeekDayName")]
        public async Task<IActionResult> GetWeekDayName()
        {
            var result = await _processConfigurationService.GetWeekDayName();
            return StatusCode(result.ResponseCode, result);
        }
        [HttpGet("GetFrequencyHour")]
        public async Task<IActionResult> GetFrequencyHour()
        {
            var result = await _processConfigurationService.GetFrequencyHour();
            return StatusCode(result.ResponseCode, result);
        }

        [HttpGet("GetSasKey")]
        public async Task<IActionResult> GetSasKey()
        {
            var result = await _processConfigurationService.GenerateSasKey();
            return StatusCode(result.ResponseCode, result);
        }

        [Route("UploadToBlobTemp")]
        [HttpPost, RequestSizeLimit(350_000_000)]
        public async Task<IActionResult> UploadToBlobTemp()
        {
            var formCollection = await Request.ReadFormAsync();

            var file = formCollection.Files.First();
            var stream = file.OpenReadStream();
            var fileName = formCollection["FileName"].ToString();


            var result = await _processConfigurationService.UploadToBlobTemp(stream, fileName);

            return StatusCode(result.ResponseCode, result);
        }

        [Route("getConvertedXLSX")]
        [HttpGet]
        public async Task<IActionResult> getConvertedXLSX()
        {
            var result = await _processConfigurationService.GetConvertedXLSX();

            return StatusCode(result.ResponseCode, result);
        }

        [Route("ConvertToXLSX")]
        [HttpPost, RequestSizeLimit(350_000_000)]
        public async Task<IActionResult> ConvertToXLSX()
        {
            try
            {
                var formCollection = await Request.ReadFormAsync();
                if (formCollection.Files.Count() >= 2)
                {
                    return BadRequest();
                }

                //logger.Log(LogLevel.Information, $"formCollection read success {formCollection["UserName"].ToString()}");

                var file = formCollection.Files.First();
                var stream = file.OpenReadStream();
                var loggedInUser = formCollection["LoggedInUser"].ToString();
                var userName = formCollection["UserName"].ToString();
                var processName = formCollection["ProcessName"].ToString();
                string json = string.Empty;

                using var zipStream = new MemoryStream();
                await file.CopyToAsync(zipStream);
                zipStream.Position = 0;

                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                var entry = archive.GetEntry(file.FileName);

                if (entry == null)
                    return BadRequest("no file found in zip");

                using var entryStream = entry.Open();
                using var memoryStream = new MemoryStream();
                await entryStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                var result = await _processConfigurationService.ConvertToXLSX(file, entryStream, processName);

                //return StatusCode(result.ResponseCode, result);


                return Ok(
                    new
                    {
                        //FileData = File(result.Result.fileData,
                        //"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        //"converted.xlsx"),
                        FileData = result.Result.fileData,
                        RowCount = result.Result.rowCount
                    });

            }
            catch (Exception ex)
            {
                _logger.LogError("Error in InsertConfiguration " + ex.Message, ex.InnerException);
                throw ex;
            }
        }

        

    }
}

