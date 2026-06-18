using Asp.Versioning;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService;

namespace Teleperformance.DataIngestion.API.Controllers.v4._1
{

    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("4.1")]
    [Authorize]
    public class BlobController : ControllerBase
    {
        ILogger<BlobController> _logger;
        IProcessConfigurationServiceV4_1 _processConfigurationServiceV4_1;
        public BlobController(ILogger<BlobController> logger, IProcessConfigurationServiceV4_1 processConfigurationServiceV4_1)
        {
            _logger = logger;
            _processConfigurationServiceV4_1 = processConfigurationServiceV4_1;
        }

        [HttpGet("DownloadFile2")]
        public async Task<IActionResult> DownloadFile([FromQuery] string sasUrl)
        {
            try
            {

                var blobClient = new BlobClient(new Uri(sasUrl));
                var download = await blobClient.DownloadContentAsync();
                var fileBytes = download.Value.Content.ToArray();
                var contentType = download.Value.Details.ContentType ?? "application/octet-stream";
                var fileName = blobClient.Name;
                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"DownloadFile Error occurred: {ex.Message.ToString()}");
                return BadRequest(new { message = $"Download failed" });

            }
        }



        [HttpDelete("DeleteJsonDatabricksColumnFiles")]
        public async Task<IActionResult> DeleteJsonDatabricksColumnFiles()
        {
            try
            {
                _logger.LogError($"Failed to delete blob:");

                var result = await _processConfigurationServiceV4_1.DeleteJsonDatabricksColumnFiles();
               return StatusCode(result.ResponseCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"DownloadFile Error occurred: {ex.Message.ToString()}");
                return BadRequest(new { message = $"Download failed" });

            }
        }



    }
}
