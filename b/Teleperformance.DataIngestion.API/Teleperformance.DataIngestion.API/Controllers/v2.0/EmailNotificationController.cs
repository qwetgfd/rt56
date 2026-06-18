using Asp.Versioning;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Teleperformance.DataIngestion.API.Controllers.v1._0;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v2._0.EmailNotification;

namespace Teleperformance.DataIngestion.API.Controllers.v2._0
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("2.0")]
    [Authorize]
    public class EmailNotificationController : ControllerBase
    {
        
        public readonly ILogger<ImportController> _logger;
        private readonly IEmailNotificationService _iEmailNotificationService;
        private readonly IBlobStorageService _iBlobStorageService;

        public EmailNotificationController(ILogger<ImportController> logger, IEmailNotificationService iEmailNotificationService, IBlobStorageService iBlobStorageService)
        {           
            _logger = logger;
            _iEmailNotificationService = iEmailNotificationService;
            _iBlobStorageService = iBlobStorageService;

        }

        [Route("List")]
        [HttpGet]        
        public async Task<IActionResult> EmailNotificationList()
        {         
            var result = await _iEmailNotificationService.GetEmailNotificationList();
            return StatusCode(result.ResponseCode, result);
        }


        [Route("Send")]
        [HttpPost]
        public async Task<IActionResult> SendEmailNotification(EmailNotificationRequestDto emailNotificationDto)
        {           
            var result = await _iEmailNotificationService.SendEmailNotification(emailNotificationDto);
            return StatusCode(result.ResponseCode, result);
        }
    }
}
