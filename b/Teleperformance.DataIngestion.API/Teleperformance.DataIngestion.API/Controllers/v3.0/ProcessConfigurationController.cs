using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.API.Controllers.v3._0
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("3.0")]
    [Authorize]
    public class ProcessConfigurationController : ControllerBase
    {
        private readonly IProcessConfigServiceV3 processConfigurationService;

        public ProcessConfigurationController(IProcessConfigServiceV3 processConfigurationService)
        {
            this.processConfigurationService = processConfigurationService;
        }


        [Route("ConvertWordToEnglishCharactersOnly")]
        [HttpGet]
        public async Task<IActionResult> ConvertWordToEnglishCharactersOnly(string wordToConvert, string language)
        {
            var result = await processConfigurationService.ConvertEnglishCharactersOnly(wordToConvert, language);
            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetAllEnglishCharactersOnly")]
        [HttpGet]
        public async Task<IActionResult> GetAllEnglishCharactersOnly(string language)
        {
            var result = await processConfigurationService.GetAllEnglishCharactersOnly(language);
            return StatusCode(result.ResponseCode, result);
        }



        [Route("UpdateLogin")]
        [HttpPost]
        public async Task<IActionResult> UpdateLogin(UpdateLoginDto updateLoginDto, CancellationToken ct)
        {
            var result = await processConfigurationService.UpdateLogin(updateLoginDto, User, ct);
            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetDeltaDatabaseNames")]
        [HttpGet]
        public async Task<IActionResult> GetDeltaDatabaseNames(int regionId, string subRegionId, int clientNameId)
        {
            var result = await processConfigurationService.GetDeltaDatabaseNames(regionId, subRegionId, clientNameId);
            return StatusCode(result.ResponseCode, result);
        }

        [Route("UpdateActiveStatusByFlpConfigurationId")]
        [HttpPost]
        public async Task<IActionResult> EnableDisableProcessByFlpConfigurationId(EnableDisableProcessByFlpConfigurationIdRequestDto request )
        {
            var result = await processConfigurationService.EnableDisableProcessByConfigurationId(request.flpConfigurationIds, request.userName, request.created_by, request.activeStatus);
            return StatusCode(result.ResponseCode, result);
        }
    }
}
