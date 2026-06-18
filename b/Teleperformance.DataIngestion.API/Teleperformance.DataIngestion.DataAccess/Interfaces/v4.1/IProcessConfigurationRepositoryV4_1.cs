using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.DataProfilling;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v4._1;
using Teleperformance.DataIngestion.Models.Entities.v4._1.DataProfilling;
using Teleperformance.DataIngestion.Models.Entities.v4._1.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v4._1.LandingLayer;
using Teleperformance.DataIngestion.Models.Models.v4._1;


namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1
{
    public interface IProcessConfigurationRepositoryV4_1
    {
        Task<int> InsertProcessConfiguration(ProcessSettingEntity processConfiguration);
        Task<int> InsertProcessFileConfiguration(FileSettingEntity fileConfiguration);
        Task<int> InsertProcessDatabaseConfiguration(DatabaseSettingEntity databaseConfiguration);
        /// <summary>
        /// Insert Process Rule Set
        /// </summary>
        /// <param name="ruleSet">List of rules </param>
        /// <param name="tabName">tabname for excel, pass null for csv and txt</param>
        /// <param name="securityGroupIds">List of security group Ids</param>
        /// <param name="created_by">nt id</param>
        /// <param name="username">full name</param>
        /// <param name="description">description</param>
        /// <returns></returns>
        Task<int> InsertProcessRuleSet(RuleSetEntity ruleSet, string tabName, string securityGroupIds, string created_by, string loginid, string description);
        Task<DatabaseResponse> UpdateColumnNameList(string flpConfigurationId, string tabName, string columnNameList);
        Task<DatabaseResponse> UpdateConvertDatatypesColumnList(string flpConfigurationId, string tabName, string convertDatatypesColumnList);
        Task<DatabaseResponse> UpdateFlpFileColumnMapping(string flpConfigurationId, string processName, string tabName, string tableName, string fileColumnMapping);
        Task<ProcessSettings?> GetFLPConfigurationByIdAsync(string id);
        Task<List<FileSettings>?> GetFileSettingsByConfigIdAsync(string id);
        Task<Models.Models.v4._1.AdditionalSettings?> GetAdditionalSettingsByConfigIdAsync(string id, string tabName);
        Task<DatabaseSettings?> GetDatabaseSettingsByConfigIdAsync(string id, string tabName);
        Task<List<FileColumnMapping>?> GetFileColumnMappingByConfigIdAsync(string id, string tabName);

        Task<DatabaseResponse> UpdateProcessTabName(string securityGroupId, string flpConfigurationId, string uploadFileId, string tabName);
        Task<IEnumerable<ProcessTabNameEntity?>> GetProcessTabNameAsync(string securityGroupId, string flpConfigurationId, string uploadFileId);

        Task<List<RuleSetNames>> GetDIGenericRulesNames(bool isActive, string securityGroupId);
        Task<List<RuleTypes>> GetDIRuleTypes();
        Task<List<SubRules>> GetDISubRules(int ruleTypeId);
        Task<List<Patterns>> GetDIPatterns(int subRuleId);
        Task<List<ConditionalOperators>> GetDIConditionalOperators();
        Task<List<RuleSetNames>> GetDIRuleSetNamesBySecGrpId(string securityGroupId);
        Task<List<RuleSetNames>> GetDIRuleSetNamesByRuleSetName(string ruleSetName , string securityGroupId);
        Task<List<RuleSet>> GetDIRuleSetByRuleSetNameId(string ruleSetNameId);

        Task<List<RuleSet>> GetDIGenericRules();

        Task<bool> CheckDIRuleSetNameExists(string ruleSetName);
        
        Task<RuleSetNameResponse> GetRuleSetNameList(RuleSetNameListRequest request);
        Task<List<RuleSet>> GetFlpRuleSetByConfigurationId(string id, string tabName);
        Task<DatabaseResponse> AddFileHeaders(string flpConfigurationId, string uploadFileId, string fileHeader, string tabName);

        Task<bool> GetGlobalRuleCreationAccess(string securityGroupId);
        Task<bool> LogUploadedFile(LogUploadedFileRequest request);        
        Task<List<ValidationSPNames>> GetValidationSPNames();
        Task<IEnumerable<DatabricksJsonFileColumnEntities>> GetDatabricksJsonFileURLs();

        Task<DatabaseResponse> DeletedDatabricksColumnJsonFile(string flpConfigurationId, string uploadFileId, string tabName);
        Task<DatabaseResponse> AddCampaignConfiguration(FlpCampaignConfiguration campaignConfiguration);
        Task<DatabaseResponse> AddCampaignUserAccess(FlpCampaignUserAccess campaignUserAccess);
        Task<DatabaseResponse> UpdateCampaignConfiguration(FlpCampaignConfiguration campaignConfiguration);
        Task<FlpCampaignConfiguration> GetCampaignConfigurationByCampaignId(string campaignId, string applicationId);
        Task<bool> CheckCampaignConfigurationExists(string campaignId, string campaignName, int? regionId, string subRegionId, int? clientId, string upn);

        Task<List<CampaignUserAccess>> GetCampaignUserAccessInfo(string upn);
        Task<DatabaseResponse> UpdateCampaignUserAccess(FlpCampaignUserAccess campaignUserAccess);
        // Add this method to the existing interface
        Task<List<FlpCampaignConfiguration>> GetCampaignByClientGeoMapping(int? regionId, string? subRegionId, int? clientId, string? applicationId);
        Task<DatabaseResponse> DeleteCampaignUserAccess(FlpCampaignUserAccess campaignUserAccess);
        //Task<List<FlpCampaignConfiguration>> GetCampaignDetails();

        Task<List<FlpCampaignConfiguration>> GetCampaignDetails(string upn, string applicationId);
        // Add this method to the existing interface
        Task<CampaignProcessedRecordsCountResponse?> GetCampaignProcessedRecordsCountAsync(string applicationId,CampaignProcessedRecordsCountRequest request);
        Task<CampaignProcessedRecordsCountResponseV2?> GetCampaignProcessedRecordsCountAsyncV2(string applicationId, CampaignProcessedRecordsCountRequest request);

        #region LandingLayer
        Task<List<FileExtensions>> GetValidFileExtensions();
        Task<List<Prefixes>> GetPrefixes();
        Task<LandingLayerUploadConfiguration> GetLandingLayerUploadConfiguration();
        Task<bool> InsertRegex(List<LandingLayerRegex> regex, string flpConfigurationId, string loginId);
        Task<bool> InsertLandingLayerFileExtension(List<int?> fileExtensions, string flpConfigurationId, string loginId);        
        Task<string> InsertFlpConfigurationDetails(InsertFlpConfigurationRequest insertFlpConfigurationRequest);
        Task<int> InsertFlpFileConfigurationDetails(FlpFileConfigurationRequest fileConfig, string flpConfigurationId, string createdBy);        
        Task<int> InsertConfigurationTableMapping(ConfigurationTableMappingRequest tableMapping, string flpConfigurationId, string createdBy, int dataSource);
        #endregion


    }
}
