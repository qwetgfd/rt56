using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.DataAccess.Services.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.DataProfilling;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.LandingLayer;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v4._1;
using Teleperformance.DataIngestion.Models.Entities.v4._1.ProcessConfiguration;
using FileConfigurationDto = Teleperformance.DataIngestion.Models.DTOs.v4._1.FileConfigurationDto;

namespace Teleperformance.DataIngestion.API.Controllers.v4._1
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("CorsPolicy")]
    [ApiVersion("4.1")]
    [Authorize]
    public class ProcessConfigurationController : ControllerBase
    {
        private readonly IProcessConfigurationServiceV4_1 _processConfigurationService;
        private readonly ILogger<ProcessConfigurationController> _logger;
        private readonly ILandingLayerService _landingLayerService;
        public ProcessConfigurationController(
            IProcessConfigurationServiceV4_1 processConfigurationService, ILandingLayerService landingLayerService,
            ILogger<ProcessConfigurationController> logger)
        {
            _processConfigurationService = processConfigurationService;
            _logger = logger;
            _landingLayerService = landingLayerService;
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

        /// <summary>
        /// Modified for Landing Layer Create Configuration in Online Module
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        [Route("LandingLayerInsertConfiguration")]
        [HttpPost, RequestSizeLimit(350_000_000)]
        public async Task<IActionResult> LandingLayerInsertConfiguration([FromForm] LandingLayerInsertConfigurationRequest req)
        {
            _logger.LogInformation("InsertConfiguration API Called!");
            try
            {
                //basic validation
                if(req.Files == null || req.Files.Count == 0)
                {
                    return BadRequest("At least one file is required under the field name 'files'.");
                }

                if (string.IsNullOrWhiteSpace(req.MyJson))
                {
                    return BadRequest("Missing 'MyJson' form field or file-part");
                }
                var formCollection = await Request.ReadFormAsync();

                _logger.Log(LogLevel.Information, $"File read success {formCollection["UserName"].ToString()}");
                var result = await _processConfigurationService.LandingLayerInsertConfiguration(req.MyJson, req.Files,req.loggedInUser, req.userName);
                _logger.Log(LogLevel.Information, $"All process executed success {formCollection["UserName"].ToString()}");
                return StatusCode(result.ResponseCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in InsertConfiguration " + ex.Message, ex.InnerException);
                throw;
            }
        }


        [Route("MovedFileToLandingLayer")]
        [HttpPost, RequestSizeLimit(350_000_000)]
        public async Task<IActionResult> MovedFileToLandingLayer([FromForm] LandingLayerInsertConfigurationRequest req)
        {
            
            try
            {
                //basic validation
                if (req.Files == null || req.Files.Count == 0)
                {
                    return BadRequest("At least one file is required under the field name 'files'.");
                }

                if (string.IsNullOrWhiteSpace(req.flpConfigurationId))
                {
                    return BadRequest("Missing 'LoggedInUser' form field or file-part");
                }

                _logger.LogInformation($"MovedFileToLandingLayer API Called for {req.flpConfigurationId}");

                if (string.IsNullOrWhiteSpace(req.loggedInUser))
                {
                    return BadRequest("Missing 'LoggedInUser' form field or file-part");
                }

                if (string.IsNullOrWhiteSpace(req.userName))
                {
                    return BadRequest("Missing 'UserName' form field or file-part");
                }              

                if (string.IsNullOrEmpty(req.uploadFileId))
                {
                    return BadRequest("Missing 'Upload File Id' form field or file-part");
                }
                //var formCollection = await Request.ReadFormAsync();

                _logger.Log(LogLevel.Information, $"File read success {req.userName}");
              //  var result = await _processConfigurationService.LandingLayerInsertConfiguration(req.MyJson, req.Files, req.LoggedInUser, req.UserName);
               
                // Parse MyJson to get the required parameters
                //var jsonData = JsonSerializer.Deserialize<FileConfigurationDto>(req.MyJson);

               
                var result = await _landingLayerService.MoveUploadFilesToLayerFile(req.Files, req.processName, req.flpConfigurationId, req.uploadFileId, req.userName);
                //
                _logger.Log(LogLevel.Information, $"All process executed success {req.userName}");
                
                return StatusCode(result.ResponseCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in MovedFileToLandingLayer " + ex.Message, ex.InnerException);
                throw;
            }
        }

       
        [HttpGet("GetLandingLayerUploadConfiguration")]
        public async Task<IActionResult> GetLandingLayerUploadConfiguration()
        {
            var result = await _processConfigurationService.GetLandingLayerUploadConfiguration();
            return StatusCode(result.ResponseCode, result);
        }


        [HttpPost("LandingLayerOfflineModuleInsertConfiguration")]
        public async Task<IActionResult> LandingLayerOfflineModuleInsertConfiguration(InsertFlpConfigurationRequest insertFlpConfigurationRequest)
        {
            _logger.LogInformation($"LandingLayerOfflineModuleInsertConfiguration API Called for {insertFlpConfigurationRequest.ProcessName}");
            var result = await _processConfigurationService.LandingLayerOfflineModuleInsertConfiguration(insertFlpConfigurationRequest);
            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetConfigurationById")]
        [HttpGet]
        public async Task<IActionResult> GetConfigurationById(string id)
        {
            var result = await _processConfigurationService.GetConfigurationById(id);
            return StatusCode(result.ResponseCode, result);

        }

        [Route("GetDIRuleTypes")]
        [HttpGet]
        public async Task<IActionResult> GetDIRuleTypes()
        {
            var result = await _processConfigurationService.GetRuleTypes();

            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetDISubRules")]
        [HttpGet]
        public async Task<IActionResult> GetDISubRules(int ruleTypeId)
        {
            var result = await _processConfigurationService.GetSubRules(ruleTypeId);

            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetDIPatterns")]
        [HttpGet]
        public async Task<IActionResult> GetDIPatterns(int subRuleId)
        {
            var result = await _processConfigurationService.GetPatterns(subRuleId);

            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetDIConditionalOperators")]
        [HttpGet]
        public async Task<IActionResult> GetDIConditionalOperators()
        {
            var result = await _processConfigurationService.GetDIConditionalOperators();

            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetDIRuleSetNamesBySecGrpId")]
        [HttpGet]
        public async Task<IActionResult> GetDIRuleSetNamesBySecGrpId(string securityGroupId)
        {
            var result = await _processConfigurationService.GetDIRuleSetNamesBySecGrpId(securityGroupId);

            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetDIRuleSetByRuleSetNameId")]
        [HttpGet]
        public async Task<IActionResult> GetDIRuleSetByRuleSetNameId(string ruleSetNameId)
        {
            var result = await _processConfigurationService.GetDIRuleSetByRuleSetNameId(ruleSetNameId);

            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetDIRuleSetByRuleSetName")]
        [HttpGet]
        public async Task<IActionResult> GetDIRuleSetByRuleSetName(string ruleSetName, string securityGroupId)
        {
            var result = await _processConfigurationService.GetDIRuleSetByRuleSetName(ruleSetName, securityGroupId);

            return StatusCode(result.ResponseCode, result);
        }



        [Route("GetDIGenericRules")]
        [HttpGet]
        public async Task<IActionResult> GetDIGenericRules()
        {
            var result = await _processConfigurationService.GetDIGenericRules();

            return StatusCode(result.ResponseCode, result);
        }


        [Route("CheckDIRuleSetNameExists")]
        [HttpGet]
        public async Task<IActionResult> CheckDIRuleSetNameExists(string ruleSetName)
        {
            var result = await _processConfigurationService.CheckDIRuleSetNameExists(ruleSetName);

            return StatusCode(result.ResponseCode, result);
        }

        [Route("InsertRuleSets")]
        [HttpPost]
        public async Task<IActionResult> InsertRuleSets(InsertRuleSetsRequest request, string flpConfigurationId = "", string tabName = "")
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState); // This will help you debug
            }

            var result = await _processConfigurationService.InsertRuleSets(request, flpConfigurationId, tabName);

            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetDIGenericRulesNames")]
        [HttpGet]
        public async Task<IActionResult> GetDIGenericRules(bool isActive)
        {
            var result = await _processConfigurationService.GetDIGenericRulesNames(isActive);

            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetRuleSetNameList")]
        [HttpPost]
        public async Task<IActionResult> GetRuleSetNameList(RuleSetNameListRequest request)
        {
            var result = await _processConfigurationService.GetRuleSetNameList(request);
            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetGlobalRuleCreationAccess")]
        [HttpGet]
        public async Task<IActionResult> GetGlobalRuleCreationAccess()
        {
            var result = await _processConfigurationService.GetGlobalRuleCreationAccess();
            return StatusCode(result.ResponseCode, result);
        }

        [Route("GetValidationSPNames")]
        [HttpGet]
        public async Task<IActionResult> GetValidationSPNames()
        {
            var result = await _processConfigurationService.GetValidationSPNames();
            return StatusCode(result.ResponseCode, result);
        }

        [Route("LogUploadedFile")]
        [HttpPost]
        public async Task<IActionResult> LogUploadedFile(LogUploadedFileRequest request)
        {
            var result = await _processConfigurationService.LogUploadedFile(request);
            return StatusCode(result.ResponseCode, result);
        }

        [Route("AddCampaignConfiguration")]
        [HttpPost]
        public async Task<IActionResult> AddCampaignConfiguration(AddCampaignConfigurationRequestDto request)
        {
            _logger.LogInformation("AddCampaignConfiguration API Called!");
            try
            {
                var result = await _processConfigurationService.AddCampaignConfiguration(request);
                return StatusCode(result.ResponseCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in AddCampaignConfiguration " + ex.Message, ex.InnerException);
                throw;
            }
        }

        [Route("UpdateCampaignConfiguration")]
        [HttpPut]
        public async Task<IActionResult> UpdateCampaignConfiguration(AddCampaignConfigurationRequestDto request)
        {
            _logger.LogInformation("UpdateCampaignConfiguration API Called!");
            try
            {
                var result = await _processConfigurationService.UpdateCampaignConfiguration(request);
                return StatusCode(result.ResponseCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in UpdateCampaignConfiguration " + ex.Message, ex.InnerException);
                throw;
            }
        }

        // Add this method to the existing ProcessConfigurationController class

        [Route("AddCampaignUserByClientGeoMapping")]
        [HttpPost]
        public async Task<IActionResult> AddCampaignUserByClientGeoMapping(AddCampaignUserByClientGeoMapping request)
        {
            _logger.LogInformation("AddCampaignUserByClientGeoMapping API Called!");
            try
            {
                var result = await _processConfigurationService.AddCampaignUserByClientGeoMapping(request);
                return StatusCode(result.ResponseCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in AddCampaignUserByClientGeoMapping " + ex.Message, ex.InnerException);
                throw;
            }
        }


        [Route("RemoveCampaignUserByClientGeoMapping")]
        [HttpDelete]
        public async Task<IActionResult> DeleteCampaignUserByClientGeoMapping(AddCampaignUserByClientGeoMapping request)
        {
            _logger.LogInformation("DeleteCampaignUserByClientGeoMapping API Called!");
            try
            {
                var result = await _processConfigurationService.RemoveCampaignUserByClientGeoMapping(request);
                return StatusCode(result.ResponseCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in DeleteCampaignUserByClientGeoMapping " + ex.Message, ex.InnerException);
                throw;
            }
        }

        [Route("AddCampaignSuperAdmin")]
        [HttpPost]
        public async Task<IActionResult> AddCampaignSuperAdmin(AddCampaignSuperAdmin request)
        {
            _logger.LogInformation("AddCampaignSuperAdmin API Called!");
            try
            {
                var result = await _processConfigurationService.AddCampaignSuperAdmin(request);
                return StatusCode(result.ResponseCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in AddCampaignUserByClientGeoMapping " + ex.Message, ex.InnerException);
                throw;
            }
        }



        [Route("RemoveCampaignSuperAdmin")]
        [HttpDelete]
        public async Task<IActionResult> RemoveCampaignSuperAdmin(AddCampaignSuperAdmin request)
        {
            _logger.LogInformation("RemoveCampaignSuperAdmin API Called!");
            try
            {
                var result = await _processConfigurationService.RemoveCampaignSuperAdmin(request);
                return StatusCode(result.ResponseCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in RemoveCampaignSuperAdmin " + ex.Message, ex.InnerException);
                throw;
            }
        }


        // Add this method to the existing StatusController class
        // Replace the existing GET method with this POST method

        [HttpPost("CampaignProcessedRecordsCount")]
        public async Task<IActionResult> CampaignProcessedRecordsCount(CampaignProcessedRecordsCountRequest request)
        {
            try
            {
                _logger.LogInformation("CampaignProcessedRecordsCount API Called for CampaignId: {CampaignId}", request?.CampaignId);

                // Model validation will be handled by ModelState
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Validate the required parameter
                if (request == null || string.IsNullOrWhiteSpace(request.CampaignId))
                {
                    var errorResponse = new APIResponse<CampaignProcessedRecordsCountResponseV2>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "CampaignId is required" },
                        Result = null
                    };
                    return StatusCode(errorResponse.ResponseCode, errorResponse);
                }

                var result = await _processConfigurationService.GetCampaignProcessedRecordsCountAsync(request);
                return StatusCode(result.ResponseCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CampaignProcessedRecordsCount for campaignId: {CampaignId}, FromDate: {FromDate}, ToDate: {ToDate}",
                    request?.CampaignId, request?.FromDate, request?.ToDate);

                return StatusCode(500, new APIResponse<CampaignProcessedRecordsCountResponseV2>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Internal server error" },
                    Result = null
                });
            }
        }


        [HttpPost("CampaignProcessedRecordsCountV2")]
        public async Task<IActionResult> CampaignProcessedRecordsCountV2(CampaignProcessedRecordsCountRequest request)
        {
            try
            {
                _logger.LogInformation("CampaignProcessedRecordsCount API Called for CampaignId: {CampaignId}", request?.CampaignId);

                // Model validation will be handled by ModelState
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Validate the required parameter
                if (request == null || string.IsNullOrWhiteSpace(request.CampaignId))
                {
                    var errorResponse = new APIResponse<CampaignProcessedRecordsCountResponse>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "CampaignId is required" },
                        Result = null
                    };
                    return StatusCode(errorResponse.ResponseCode, errorResponse);
                }

                var result = await _processConfigurationService.GetCampaignProcessedRecordsCountAsyncV2(request);
                return StatusCode(result.ResponseCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CampaignProcessedRecordsCount for campaignId: {CampaignId}, FromDate: {FromDate}, ToDate: {ToDate}",
                    request?.CampaignId, request?.FromDate, request?.ToDate);

                return StatusCode(500, new APIResponse<CampaignProcessedRecordsCountResponse>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Internal server error" },
                    Result = null
                });
            }
        }




        [Route("GetCampaignUserAccessInfo")]
        [HttpGet]
        public async Task<IActionResult> GetCampaignUserAccessInfo(string upn)
        {
            var result = await _processConfigurationService.GetCampaignUserAccessInfo(upn);
            return StatusCode(result.ResponseCode, result);
        }

        
        [HttpGet("GetValidFileExtensions")]
        public async Task<IActionResult> GetValidFileExtensions()
        {
            var result = await _processConfigurationService.GetValidFileExtensions();
            return StatusCode(result.ResponseCode, result);
        }

        [HttpGet("GetPrefixes")]
        public async Task<IActionResult> GetPrefixes()
        {
            var result = await _processConfigurationService.GetPrefixes();
            return StatusCode(result.ResponseCode, result);
        }
    }
}
