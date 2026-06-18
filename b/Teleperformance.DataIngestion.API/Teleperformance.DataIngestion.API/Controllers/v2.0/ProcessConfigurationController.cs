using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.DataAccess.Services.v3._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v2._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.API.Controllers.v2._0
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("2.0")]
    [Authorize]

    public class ProcessConfigurationController : ControllerBase
    {
        private readonly IProcessConfigService _processConfigurationService;
        private readonly IDataSliceAPIService dataSliceAPIService;

        public ProcessConfigurationController(IProcessConfigService processConfigurationService, IDataSliceAPIService dataSliceAPIService)
        {
            _processConfigurationService = processConfigurationService;
            this.dataSliceAPIService = dataSliceAPIService;
        }

        [Route("SecurityGroups")]
        [HttpGet]
        public async Task<IActionResult> SecurityGroups(string loginId)
        {
            var result = await _processConfigurationService.GetSecurityGroups(loginId);
            return StatusCode(result.ResponseCode, result);
        }


        [Route("SaveSecurityGroup")]
        [HttpPost]
        public async Task<IActionResult> SaveSecurityGroup(UserSelectedSecurityGroupDto selectedSecurityGroupDto)
        {
            var result = await _processConfigurationService.SaveUserSecurityGroup(selectedSecurityGroupDto);
            //Cleared cache
            var result1 = await dataSliceAPIService.ClearCache();
            var result2 = await dataSliceAPIService.FillDataInCache();
            return StatusCode(result.ResponseCode, result);
        }


        [Route("GetAllProcessNamesByLoginId")]
        [HttpGet]
        public async Task<IActionResult> GetAllProcessNamesByLoginId(int fileProcessingServerTypeId = 1)
        {
            var result = await _processConfigurationService.GetAllProcessNamesByLoginId(fileProcessingServerTypeId);
            return StatusCode(result.ResponseCode, result);

        }

        [Route("GetAllProcessNamesByLoginIdByTerm")]
        [HttpGet]
        public async Task<IActionResult> GetAllProcessNamesByLoginIdByTerm(string queryTerm="",int fileProcessingServerTypeId = 1)
        {
            var result = await _processConfigurationService.GetAllProcessNamesByLoginIdByTerm(queryTerm,fileProcessingServerTypeId);
            return StatusCode(result.ResponseCode,result.Result);

        }

        [Route("GetDSConfiguration")]
        [HttpGet]
        public async Task<IActionResult> GetDSConfiguration(int id)
        {
            var result = await _processConfigurationService.GetDSConfiguration(id);
            return StatusCode(result.ResponseCode, result);

        }
        [Route("GetRegionBySecurityGroupId")]
        [HttpGet]
        public async Task<IActionResult> GetRegionBySecurityGroupId(string securityGroupId)
        {
            var result = await _processConfigurationService.GetRegionBySecurityGroupId(securityGroupId);
            return StatusCode(result.ResponseCode, result);

        }
    }
}
