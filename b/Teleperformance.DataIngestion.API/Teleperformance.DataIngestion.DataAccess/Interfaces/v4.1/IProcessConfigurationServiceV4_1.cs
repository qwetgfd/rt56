using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.DataProfilling;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.LandingLayer;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v4._1;
using Teleperformance.DataIngestion.Models.Entities.v4._1.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Models.v4._1;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1
{
    public interface IProcessConfigurationServiceV4_1
    {
        Task<APIResponse<ConfigurationResponseDto>> InsertConfiguration(string json, IFormFile file, Stream stream, string loggedInUser, string userName);
        Task<APIResponse<FLPConfigurationModel>> GetConfigurationById(string id);
        Task<APIResponse<FLPConfigurationModel>> GetMultisheetConfigurationById(string flpConfigurationId, string uploadFileId);
        //Task<APIResponse<FLPConfigurationModel>> GetFlpRuleSet(string flpConfigurationId, string uploadFileId);
        Task<APIResponse<bool>> InsertRuleSets(InsertRuleSetsRequest ruleSets, string flpConfigurationId, string tabName);
        Task<APIResponse<List<RuleSetNamesDto>>> GetDIGenericRulesNames(bool isActive);

        Task<APIResponse<List<RuleTypesDto>>> GetRuleTypes();
        Task<APIResponse<List<SubRulesDto>>> GetSubRules(int ruleTypeId);
        Task<APIResponse<List<PatternsDto>>> GetPatterns(int subRuleId);

        Task<APIResponse<List<ConditionalOperatorsDto>>> GetDIConditionalOperators();
        Task<APIResponse<List<RuleSetNamesDto>>> GetDIRuleSetNamesBySecGrpId(string securityGroupId);
        Task<APIResponse<List<RuleSetDto>>> GetDIRuleSetByRuleSetNameId(string securityGroupId);
        Task<APIResponse<List<RuleSetNamesDto>>> GetDIRuleSetByRuleSetName(string ruleSetName, string securityGroupId);

        Task<APIResponse<List<RuleSetDto>>> GetDIGenericRules();
        Task<APIResponse<bool>> CheckDIRuleSetNameExists(string ruleSetName);
        Task<APIResponse<RuleSetNameResponse>> GetRuleSetNameList(RuleSetNameListRequest ruleSetName);
        Task<APIResponse<bool>> GetGlobalRuleCreationAccess();
        Task<APIResponse<List<ValidationSPNamesDto>>> GetValidationSPNames();
        Task<APIResponse<bool>> LogUploadedFile(LogUploadedFileRequest request);
        Task<APIResponse<bool>> DeleteJsonDatabricksColumnFiles();
        Task<UpdateLoginResponseDto> UpdateLogin(string loginId, ClaimsPrincipal user, CancellationToken ct);
        

        Task<APIResponse<List<CampaignUserAccessDto>>> GetCampaignUserAccessInfo(string UPN);

        Task<APIResponse<string>> AddCampaignConfiguration(AddCampaignConfigurationRequestDto request);
        Task<APIResponse<string>> UpdateCampaignConfiguration(AddCampaignConfigurationRequestDto request);
        Task<APIResponse<CampaignProcessedRecordsCountResponseV2?>> GetCampaignProcessedRecordsCountAsyncV2(CampaignProcessedRecordsCountRequest request);

        #region LandingLayer

        Task<APIResponse<List<FileExtensionDto>>> GetValidFileExtensions();
        Task<APIResponse<List<PrefixesDto>>> GetPrefixes();
        Task<APIResponse<ConfigurationResponseDto>> LandingLayerInsertConfiguration(string json, List<IFormFile> file, string loggedInUser, string userName);
        Task<APIResponse<string>> LandingLayerOfflineModuleInsertConfiguration(InsertFlpConfigurationRequest insertFlpConfigurationRequest);

        Task<APIResponse<LandingLayerUploadConfigurationDto>> GetLandingLayerUploadConfiguration();

        // Add this method to the existing interface
        Task<APIResponse<string>> AddCampaignUserByClientGeoMapping(AddCampaignUserByClientGeoMapping request);
        Task<APIResponse<string>> RemoveCampaignUserByClientGeoMapping(AddCampaignUserByClientGeoMapping request);

        Task<APIResponse<string>> AddCampaignSuperAdmin(AddCampaignSuperAdmin request);

        Task<APIResponse<string>> RemoveCampaignSuperAdmin(AddCampaignSuperAdmin request);

        // Add this method to the existing interface
        Task<APIResponse<CampaignProcessedRecordsCountResponse?>> GetCampaignProcessedRecordsCountAsync(CampaignProcessedRecordsCountRequest request);
        #endregion

    }
}
