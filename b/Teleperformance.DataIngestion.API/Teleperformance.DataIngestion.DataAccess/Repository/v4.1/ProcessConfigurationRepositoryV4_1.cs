using AngleSharp.Dom;
using Dapper;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.Extensions.Logging;
using NPOI.SS.Formula.Functions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.DataProfilling;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v4._1;
using Teleperformance.DataIngestion.Models.Entities.v4._1.DataProfilling;
using Teleperformance.DataIngestion.Models.Entities.v4._1.LandingLayer;
using Teleperformance.DataIngestion.Models.Entities.v4._1.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Models.v4._1;
using static Dapper.SqlMapper;
using FileColumnMapping = Teleperformance.DataIngestion.Models.Models.v4._1.FileColumnMapping;
using IDapperService = Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0.IDapperService;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v4._1
{
    public class ProcessConfigurationRepositoryV4_1 : IProcessConfigurationRepositoryV4_1
    {
        private readonly ILogger<ProcessConfigurationRepositoryV4_1> _logger;
        private readonly IDapperService _dapperService;

        public ProcessConfigurationRepositoryV4_1(ILogger<ProcessConfigurationRepositoryV4_1> logger, IDapperService dapperService)
        {
            this._logger = logger;
            this._dapperService = dapperService;
        }

        public async Task<int> InsertProcessConfiguration(ProcessSettingEntity processConfiguration)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", processConfiguration.flpConfigurationId);
                dynamicParameters.Add("@process_name", processConfiguration.process_name);
                dynamicParameters.Add("@is_active", processConfiguration.is_active);
                dynamicParameters.Add("@sender_communication_email", processConfiguration.sender_communication_email);
                dynamicParameters.Add("@support_communication_email", processConfiguration.support_communication_email);
                dynamicParameters.Add("@created_by", processConfiguration.created_by);
                dynamicParameters.Add("@userName", processConfiguration.userName);
                dynamicParameters.Add("@created_date", processConfiguration.created_date);
                dynamicParameters.Add("@loginid", processConfiguration.loginid);
                dynamicParameters.Add("@search_string_in_file_name", processConfiguration.search_string_in_file_name);
                dynamicParameters.Add("@process_group_name", processConfiguration.process_group_name);
                dynamicParameters.Add("@billing_client_name", processConfiguration.billing_client_name);
                dynamicParameters.Add("@Description", processConfiguration.description);
                dynamicParameters.Add("@RegionId", processConfiguration.regionId);
                dynamicParameters.Add("@SubRegionId", processConfiguration.subRegionId);
                dynamicParameters.Add("@ClientId", processConfiguration.clientId);
                dynamicParameters.Add("@securityGroupId", String.Join(",", processConfiguration.securityGroups.Select(c => c.securityGroupId)));
                dynamicParameters.Add("@region", processConfiguration.region);
                dynamicParameters.Add("@subRegion", processConfiguration.subRegion);
                dynamicParameters.Add("@clientName", processConfiguration.clientName);
                dynamicParameters.Add("@fileProcessingServerTypeId", processConfiguration.dataSource);
                dynamicParameters.Add("@sourcePath", processConfiguration.sourcePath);
                dynamicParameters.Add("@destinationPath", processConfiguration.destinationPath);
                dynamicParameters.Add("@multisheet", processConfiguration.multisheet);
                dynamicParameters.Add("@sheetReferenceByIndex", processConfiguration.sheetReferenceByIndex);
                dynamicParameters.Add("@internalCampaignId", processConfiguration.internalCampaignId);
                var dbResponse = await _dapperService.InsertDataAsync<ProcessSettingEntity>("[commit_flpConfiguration]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<int> InsertProcessFileConfiguration(FileSettingEntity fileConfiguration)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", fileConfiguration.flpConfigurationId);
                dynamicParameters.Add("@tabName", fileConfiguration.tabName);
                dynamicParameters.Add("@ignoreSheet", fileConfiguration.ignoreSheet);
                dynamicParameters.Add("@delimiter", fileConfiguration.delimiter);
                dynamicParameters.Add("@is_header_provided", fileConfiguration.flexCheckHasHeaders);
                dynamicParameters.Add("@skip_rows", fileConfiguration.skip_header_rows);
                dynamicParameters.Add("@skip_footer_rows", fileConfiguration.skip_footer_rows);
                dynamicParameters.Add("@quote_character", fileConfiguration.txtQuoteCharacter);
                dynamicParameters.Add("@key_column_list", fileConfiguration.key_column_list);
                dynamicParameters.Add("@column_name_list", fileConfiguration.column_name_list);
                dynamicParameters.Add("@convert_datatypes_column_list", fileConfiguration.convert_datatypes_column_list);
                dynamicParameters.Add("@loginid", fileConfiguration.loginid);
                dynamicParameters.Add("@order_by_column_list_for_dedup", fileConfiguration.order_by_column_list_for_dedup);
                dynamicParameters.Add("@ignore_duplicate_rows", fileConfiguration.ignore_duplicate_rows);
                dynamicParameters.Add("@do_not_archive_file", fileConfiguration.do_not_archive_file);
                dynamicParameters.Add("@keep_first_row", fileConfiguration.keep_first_row);
                dynamicParameters.Add("@spanishToEnglish", fileConfiguration.spanish_to_english);
                dynamicParameters.Add("@romanNumeralsOnly", fileConfiguration.roman_numerals_only);
                dynamicParameters.Add("@skipEmptyLines", fileConfiguration.flexCheckSkipEmptyLines);
                dynamicParameters.Add("@prefix", fileConfiguration.prefix);
                dynamicParameters.Add("@dateFormatId", fileConfiguration.dateFormatId);
                dynamicParameters.Add("@timeFormatId", fileConfiguration.timeFormatId);
                var dbResponse = await _dapperService.InsertDataAsync<ProcessSettingEntity>("[commit_flpFileConfiguration]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<int> InsertProcessDatabaseConfiguration(DatabaseSettingEntity databaseConfiguration)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", databaseConfiguration.flpConfigurationId);
                dynamicParameters.Add("@tabName", databaseConfiguration.tabName);
                dynamicParameters.Add("@ignoreSheet", databaseConfiguration.ignoreSheet);
                dynamicParameters.Add("@process_name", databaseConfiguration.process_name);
                dynamicParameters.Add("@db_file_column_name_list", databaseConfiguration.db_file_columnName_list);
                dynamicParameters.Add("@table_name", databaseConfiguration.table_name);
                dynamicParameters.Add("@loginid", databaseConfiguration.loginid);
                dynamicParameters.Add("@drop_main_table", databaseConfiguration.drop_main_table);
                dynamicParameters.Add("@drop_history_table", databaseConfiguration.drop_history_table);
                dynamicParameters.Add("@validate_fileschema", databaseConfiguration.validate_fileschema);
                dynamicParameters.Add("@databaseConfigurationId", databaseConfiguration.databaseConfigurationId);
                dynamicParameters.Add("@mergeData", databaseConfiguration.mergeData);
                dynamicParameters.Add("@createHistoryTable", databaseConfiguration.createHistoryTable);
                dynamicParameters.Add("@fileProcessingServerTypeId", databaseConfiguration.dataSource);
                dynamicParameters.Add("@storageAccountId", databaseConfiguration.deltaStorageAccountId);
                dynamicParameters.Add("@deltaContainerName", databaseConfiguration.deltaContainerName);
                dynamicParameters.Add("@deltaSource", databaseConfiguration.deltaSource);
                dynamicParameters.Add("@deltaJobId", databaseConfiguration.deltaJobId);
                dynamicParameters.Add("@landingLayerAcceptedPath", databaseConfiguration.landingLayerAcceptedPath);
                dynamicParameters.Add("@landingLayerRejectedPath", databaseConfiguration.landingLayerRejectedPath);
                var dbResponse = await _dapperService.InsertDataAsync<ProcessSettingEntity>("[commit_ConfigurationTableMapping]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<DatabaseResponse> UpdateColumnNameList(string flpConfigurationId, string tabName, string columnNameList)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@tabName", tabName);
                dynamicParameters.Add("@columnNameList", columnNameList);

                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_columnNameList]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<DatabaseResponse> UpdateConvertDatatypesColumnList(string flpConfigurationId, string tabName, string convertDatatypesColumnList)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@tabName", tabName);
                dynamicParameters.Add("@convertDatatypesColumnList", convertDatatypesColumnList);

                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_convertDatatypesColumnList]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<DatabaseResponse> UpdateFlpFileColumnMapping(string flpConfigurationId, string processName, string tabName, string tableName, string fileColumnMapping)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@process_name", processName);
                dynamicParameters.Add("@tabName", tabName);
                dynamicParameters.Add("@table_name", tableName);
                dynamicParameters.Add("@db_file_column_name_list", fileColumnMapping);

                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_flpFileColumnMapping]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<DatabaseResponse> UpdateProcessTabName(string securityGroupId, string flpConfigurationId, string uploadFileId, string tabName)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@securityGroupId", securityGroupId);
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@uploadFileId", uploadFileId);
                dynamicParameters.Add("@tabName", tabName);

                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_processTabNames]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<IEnumerable<ProcessTabNameEntity?>> GetProcessTabNameAsync(string securityGroupId, string flpConfigurationId, string uploadFileId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@securityGroupId", securityGroupId);
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@uploadFileId", uploadFileId);
                var dbResponse = await _dapperService.GetMultipleRowsAsync<ProcessTabNameEntity>("[sel_processTabNames]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<ProcessSettings?> GetFLPConfigurationByIdAsync(string id)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", id);

                using var con = _dapperService.CreateConnection();
                var dbResponse = await con.QueryMultipleAsync("[sel_flpConfigurationByConfigId]", dynamicParameters, commandType: CommandType.StoredProcedure);
                var fileConfig = dbResponse.Read<ProcessSettings>().FirstOrDefault();

                if (fileConfig != null)
                {
                    fileConfig.securityGroups = dbResponse.Read<SecurityGroup>().ToArray();
                }

                return fileConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<FileSettings>?> GetFileSettingsByConfigIdAsync(string id)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", id);

                using var con = _dapperService.CreateConnection();
                var dbResponse = await con.QueryMultipleAsync("dbo.sel_FileSettingsByConfigId", dynamicParameters, commandType: CommandType.StoredProcedure);
                var fileConfig = dbResponse.Read<FileSettings>().ToList();
                return fileConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<Models.Models.v4._1.AdditionalSettings?> GetAdditionalSettingsByConfigIdAsync(string id, string tabName)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", id);
                dynamicParameters.Add("@tabName", tabName);
                using var con = _dapperService.CreateConnection();
                var dbResponse = await con.QueryMultipleAsync("dbo.sel_flpFileConfigurationByConfigId", dynamicParameters, commandType: CommandType.StoredProcedure);
                var fileConfig = dbResponse.Read<Models.Models.v4._1.AdditionalSettings>().FirstOrDefault();
                //if(fileConfig)
                return fileConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<DatabaseSettings?> GetDatabaseSettingsByConfigIdAsync(string id, string tabName)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", id);
                if (!string.IsNullOrWhiteSpace(tabName))
                    dynamicParameters.Add("@tabName", tabName);
                using var con = _dapperService.CreateConnection();
                var dbResponse = await con.QueryMultipleAsync("dbo.sel_flpDatabaseConfigurationByConfigId", dynamicParameters, commandType: CommandType.StoredProcedure);
                var fileConfig = dbResponse.Read<DatabaseSettings>().FirstOrDefault();
                return fileConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<FileColumnMapping>?> GetFileColumnMappingByConfigIdAsync(string id, string tabName)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", id);
                if (!string.IsNullOrWhiteSpace(tabName))
                    dynamicParameters.Add("@tabName", tabName);
                using var con = _dapperService.CreateConnection();
                var dbResponse = await con.QueryMultipleAsync("dbo.sel_flpFileColumnMappingByConfigId", dynamicParameters, commandType: CommandType.StoredProcedure);
                var fileConfig = dbResponse.Read<FileColumnMapping>().ToList();
                return fileConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<int> InsertProcessRuleSet(RuleSetEntity ruleSet, string tabName, string securityGroupIds, string created_by, string username, string description)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", ruleSet.flpConfigurationId);
                dynamicParameters.Add("@ruleId", ruleSet.id);
                dynamicParameters.Add("@ruleSetNameId", ruleSet.ruleSetNameId);
                dynamicParameters.Add("@ruleSetName", ruleSet.ruleSetName);
                //dynamicParameters.Add("@addRuleSetName", ruleSet.ruleSetNameId.Trim().Length == 0 ? true : false );
                dynamicParameters.Add("@ruleTypeId", ruleSet.ruleTypeId);
                dynamicParameters.Add("@subRuleId", ruleSet.subRuleId);
                // dynamicParameters.Add("@ruleColumnName", string.Join(",", ruleSet.ruleColumnName));
                // dynamicParameters.Add("@ruleColumnName2", string.Join(",", ruleSet.ruleColumnName2));
                dynamicParameters.Add("@ruleColumnName", ruleSet.ruleColumnName != null ? string.Join(",", ruleSet.ruleColumnName) : null);
                dynamicParameters.Add("@ruleColumnName2", ruleSet.ruleColumnName2 != null ? string.Join(",", ruleSet.ruleColumnName2) : null);
                dynamicParameters.Add("@ruleDescription", ruleSet.ruleDescription);
                dynamicParameters.Add("@prompt", ruleSet.prompt);
                dynamicParameters.Add("@format", ruleSet.format);
                dynamicParameters.Add("@patternId", ruleSet.patternId);
                dynamicParameters.Add("@isCombinationRule", ruleSet.isCombinationRule);
                dynamicParameters.Add("@securityGroupIds", securityGroupIds);
                dynamicParameters.Add("@tabName", tabName);
                dynamicParameters.Add("@isActive", ruleSet.isActive);
                dynamicParameters.Add("@isGlobal", ruleSet.isGlobal);
                dynamicParameters.Add("@created_by", created_by);
                dynamicParameters.Add("@username", username);
                dynamicParameters.Add("@description", description);
                dynamicParameters.Add("@ruleSetType", ruleSet.ruleSetType);
                dynamicParameters.Add("@conditionId", ruleSet.conditionId);
                dynamicParameters.Add("@fromValue", ruleSet.fromValue);
                dynamicParameters.Add("@toValue", ruleSet.toValue);
                dynamicParameters.Add("@isAllowNullOrSpace", ruleSet.isAllowNullOrSpace);
                dynamicParameters.Add("@spNameId", ruleSet.spNameId);
                dynamicParameters.Add("@isUpdated", ruleSet.isUpdated);



                var dbResponse = await _dapperService.InsertDataAsync<RuleSetEntity>("[commit_ConfigurationRuleSet]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<RuleSet>?> GetFlpRuleSetByConfigurationId(string id, string tabName)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", id);
                dynamicParameters.Add("@tabName", tabName);

                using var con = _dapperService.CreateConnection();
                var dbResponse = await con.QueryMultipleAsync("dbo.sel_flpRuleSetsByConfigId", dynamicParameters, commandType: CommandType.StoredProcedure);
                var flpRuleSet = dbResponse.Read<RuleSet>().ToList();
                return flpRuleSet;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<RuleTypes>> GetDIRuleTypes()
        {
            try
            {
                var dbResponse = await _dapperService.GetMultipleRowsAsync<RuleTypes>("[sel_RuleTypes]", null, commandType: CommandType.StoredProcedure);
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<SubRules>> GetDISubRules(int ruleTypeId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@ruleTypeId", ruleTypeId);
                var dbResponse = await _dapperService.GetMultipleRowsAsync<SubRules>("[sel_SubRules]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }
        public async Task<List<Patterns>> GetDIPatterns(int subRuleTypeId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@subRuleTypeId", subRuleTypeId);
                var dbResponse = await _dapperService.GetMultipleRowsAsync<Patterns>("[sel_Patterns]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<ConditionalOperators>> GetDIConditionalOperators()
        {
            try
            {
                var dbResponse = await _dapperService.GetMultipleRowsAsync<ConditionalOperators>("[sel_conditionalOperators]", null, commandType: CommandType.StoredProcedure);
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<RuleSetNames>> GetDIRuleSetNamesBySecGrpId(string securityGroupId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@SecurityGroupId", securityGroupId);

                var dbResponse = await _dapperService.GetMultipleRowsAsync<RuleSetNames>("[sel_ConfigurationRuleSetNameBySecGrpId]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<RuleSetNames>> GetDIRuleSetNamesByRuleSetName(string ruleSetName, string securityGroupId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@ruleSetName", ruleSetName);
                dynamicParameters.Add("@securityGroupId", securityGroupId);


                var dbResponse = await _dapperService.GetMultipleRowsAsync<RuleSetNames>("[sel_ConfigurationRuleSetNameByRuleSetName]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }



        public async Task<List<RuleSet>> GetDIRuleSetByRuleSetNameId(string ruleSetNameId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@ruleSetNameId", ruleSetNameId);

                var dbResponse = await _dapperService.GetMultipleRowsAsync<RuleSet>("[sel_flpRuleSetsByRuleSetId]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<RuleSet>> GetDIGenericRules()
        {
            try
            {

                var dbResponse = await _dapperService.GetMultipleRowsAsync<RuleSet>("[sel_flpGenericRules]", null, commandType: CommandType.StoredProcedure);

                return dbResponse.ToList();
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<RuleSetNames>> GetDIGenericRulesNames(bool isActive, string securityGroupId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@securityGroupId", securityGroupId);
                dynamicParameters.Add("@isActive", isActive);

                var dbResponse = await _dapperService.GetMultipleRowsAsync<RuleSetNames>("[sel_flpGenericRulesNames]", dynamicParameters, commandType: CommandType.StoredProcedure);

                return dbResponse.ToList();
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<bool> CheckDIRuleSetNameExists(string ruleSetName)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@ruleSetName", ruleSetName);

                var dbResponse = await _dapperService.GetSingleRowAsync<bool>("[sel_DIRuleSetNameExists]", dynamicParameters, commandType: CommandType.StoredProcedure);

                return dbResponse;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<RuleSetNameResponse> GetRuleSetNameList(RuleSetNameListRequest request)
        {
            var dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@pageNumber", request.PageNumber);
            dynamicParameters.Add("@pageSize", request.PageSize);
            //dynamicParameters.Add("@processName",request.ProcessName);
            dynamicParameters.Add("@fromDate", request.FromDate);
            dynamicParameters.Add("@toDate", request.ToDate);
            dynamicParameters.Add("@createdBy", request.CreatedBy);
            dynamicParameters.Add("@searchColumnName", request.SearchOnColumn);
            dynamicParameters.Add("@searchColumnValue", request.SearchValue);
            dynamicParameters.Add("@securityGroupId", request.securityGroupId);
            dynamicParameters.Add("@isActive", request.isActive);
            dynamicParameters.Add("@totalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

            var result = await _dapperService.GetMultipleRowsAsync<RuleSetNameListResponse>("sel_ruleSetNameList", dynamicParameters);
            int totalCount = dynamicParameters.Get<int>("@totalCount");

            return new RuleSetNameResponse
            {
                Response = result.ToList(),
                TotalCount = totalCount
            };
        }

        public async Task<DatabaseResponse> AddFileHeaders(string flpConfigurationId, string uploadFileId, string fileHeader, string tabName)
        {
            var dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
            dynamicParameters.Add("@uploadFileId", uploadFileId);
            dynamicParameters.Add("@fileHeaders", fileHeader);
            dynamicParameters.Add("@tabName", tabName);
            var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_fileHeaders]", dynamicParameters, commandType: CommandType.StoredProcedure);
            return dbResponse;



        }

        public async Task<bool> GetGlobalRuleCreationAccess(string securityGroupId)
        {
            var dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@securityGroupId", securityGroupId);

            var dbResponse = await _dapperService.GetSingleRowAsync<bool>("[sel_GlobalRuleCreationAccess]", dynamicParameters, commandType: CommandType.StoredProcedure);

            return dbResponse;
        }

        public async Task<List<ValidationSPNames>> GetValidationSPNames()
        {

            var dbResponse = await _dapperService.GetMultipleRowsAsync<ValidationSPNames>("[sel_ValidationRuleSPNames]", null, commandType: CommandType.StoredProcedure);

            return dbResponse.ToList();
        }

        public async Task<bool> LogUploadedFile(LogUploadedFileRequest request)
        {
            var dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@fileName", request.fileName);
            dynamicParameters.Add("@fileSize", request.fileSize);
           // dynamicParameters.Add("@uploadedDateTime", request.uploadedDateTime);
            dynamicParameters.Add("@uploadedBy", request.uploadedBy);
            if (!string.IsNullOrEmpty(request.flpConfigurationId))
                dynamicParameters.Add("@flpConfigurationId", request.flpConfigurationId);
            if (!string.IsNullOrEmpty(request.uploadFileId))
                dynamicParameters.Add("@uploadFileId", request.uploadFileId);
            if (!string.IsNullOrEmpty(request.securityGroupId))
                dynamicParameters.Add("@securityGroupId", request.securityGroupId);

            var dbResponse = await _dapperService.InsertDataAsync<int>("[commit_logUploadedFile]", dynamicParameters, commandType: CommandType.StoredProcedure);
            return Convert.ToBoolean(dbResponse);
        }

        public async Task<IEnumerable<DatabricksJsonFileColumnEntities>> GetDatabricksJsonFileURLs()
        {
            var dbResponse = await _dapperService.GetMultipleRowsAsync<DatabricksJsonFileColumnEntities>("[sel_databricksJsonFileDetails]", null, commandType: CommandType.StoredProcedure);
            return dbResponse;
        }

        public async Task<DatabaseResponse> DeletedDatabricksColumnJsonFile(string flpConfigurationId, string uploadFileId, string tabName)
        {
            var dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
            dynamicParameters.Add("@uploadFileId", uploadFileId);
            dynamicParameters.Add("@tabName", tabName);
            var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_databricksJsonFileColumnURL]", dynamicParameters, commandType: CommandType.StoredProcedure);
            return dbResponse;
        }

        


        public async Task<DatabaseResponse> AddCampaignConfiguration(FlpCampaignConfiguration campaignConfiguration)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@internalCampaignId", campaignConfiguration.InternalCampaignId);
                dynamicParameters.Add("@campaignId", campaignConfiguration.CampaignId);
                dynamicParameters.Add("@campaignName", campaignConfiguration.CampaignName);
                dynamicParameters.Add("@regionId", campaignConfiguration.RegionId);
                dynamicParameters.Add("@subRegionId", campaignConfiguration.SubRegionId);
                dynamicParameters.Add("@clientId", campaignConfiguration.ClientId);
                dynamicParameters.Add("@addedBy", campaignConfiguration.AddedBy);
                dynamicParameters.Add("@applicationId", campaignConfiguration.ApplicationId);
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_campaignConfiguration]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        #region LandingLayer
        public async Task<bool> InsertRegex(List<LandingLayerRegex> regex, string flpConfigurationId, string loginId)
        {

            var cleaned = regex?
                    .Select(r => new LandingLayerRegex { regex = r.regex?.Trim(), description = r.description?.Trim() })
                    .Where(r => !string.IsNullOrWhiteSpace(r.regex) && r != null)
                    .Distinct()
                    .ToList() ?? new List<LandingLayerRegex>();

            var json = JsonSerializer.Serialize(cleaned);


            var dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@regex", json);
            dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
            dynamicParameters.Add("@insertedBy", loginId);


            var dbResponse = await _dapperService.InsertDataAsync<int>("[commit_flpFileConfigRegex]", dynamicParameters, commandType: CommandType.StoredProcedure);
            return Convert.ToBoolean(dbResponse);
        }
        public async Task<List<FileExtensions>> GetValidFileExtensions()
        {

            var dbResponse = await _dapperService.GetMultipleRowsAsync<FileExtensions>("[sel_FileExtensionNames]", null, commandType: CommandType.StoredProcedure);

            return dbResponse.ToList();
        }

        public async Task<List<Prefixes>> GetPrefixes()
        {

            var dbResponse = await _dapperService.GetMultipleRowsAsync<Prefixes>("[sel_LandinLayerPrefixes]", null, commandType: CommandType.StoredProcedure);

            return dbResponse.ToList();
        }

        public async Task<LandingLayerUploadConfiguration> GetLandingLayerUploadConfiguration()
        {
            var dynamicParameters = new DynamicParameters();
            

            var dbResponse = await _dapperService.GetSingleRowAsync<LandingLayerUploadConfiguration>("[sel_LandingLayerConfiguration]", null, commandType: CommandType.StoredProcedure);

            return dbResponse;
        }

        public async Task<bool> InsertLandingLayerFileExtension(List<int?> fileExtensions, string flpConfigurationId, string loginId)
        {

            // Normalize: remove <=0, distinct, keep order
            var cleaned = (fileExtensions ?? new List<int?>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();
                  

            var json = JsonSerializer.Serialize(cleaned);

            var dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@extensionIds", json);
            dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
            dynamicParameters.Add("@insertedBy", loginId);


            var dbResponse = await _dapperService.InsertDataAsync<int>("[commit_flpFileConfigFileExtension]", dynamicParameters, commandType: CommandType.StoredProcedure);
            return Convert.ToBoolean(dbResponse);
        }

        /// <summary>
        /// Insert FLPConfiguration for offline module
        /// </summary>
        /// <param name="insertFlpConfigurationRequest"></param>
        /// <returns></returns>
        public async Task<string> InsertFlpConfigurationDetails(InsertFlpConfigurationRequest insertFlpConfigurationRequest)
        {
            try
            {
                // Initialize dynamic parameters for the main FlpConfiguration insert
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@FlpConfigurationId", insertFlpConfigurationRequest.FlpConfigurationId, direction: ParameterDirection.Output); // Output to get the inserted ID
                dynamicParameters.Add("@process_name", insertFlpConfigurationRequest.ProcessName);
                dynamicParameters.Add("@locationTypeId", insertFlpConfigurationRequest.LocationTypeId);
                dynamicParameters.Add("@sender_communication_email", insertFlpConfigurationRequest.SenderCommunicationEmail);
                dynamicParameters.Add("@created_by", insertFlpConfigurationRequest.CreatedBy);
                dynamicParameters.Add("@userName", insertFlpConfigurationRequest.UserName);
                dynamicParameters.Add("@description", insertFlpConfigurationRequest.Description);
                dynamicParameters.Add("@processTypeId", insertFlpConfigurationRequest.ProcessTypeId);
                dynamicParameters.Add("@regionId", insertFlpConfigurationRequest.RegionId);
                dynamicParameters.Add("@subRegionId", insertFlpConfigurationRequest.SubRegionId);
                dynamicParameters.Add("@clientId", insertFlpConfigurationRequest.ClientId);
                dynamicParameters.Add("@search_string_in_file_name", insertFlpConfigurationRequest.SearchStringInFileName);
                dynamicParameters.Add("@serverLocationId", insertFlpConfigurationRequest.ServerLocationId);
                dynamicParameters.Add("@baseFolderName", insertFlpConfigurationRequest.BaseFolderName);
                dynamicParameters.Add("@sourceFolderLocation", insertFlpConfigurationRequest.SourceFolderLocation);
                dynamicParameters.Add("@scheduledId", insertFlpConfigurationRequest.ScheduledId);
                dynamicParameters.Add("@scheduleValue", insertFlpConfigurationRequest.scheduleValue);
                dynamicParameters.Add("@scheduledDate", insertFlpConfigurationRequest.ScheduledDate);
                dynamicParameters.Add("@scheduledTime", insertFlpConfigurationRequest.ScheduledTime);
                dynamicParameters.Add("@scheduledEndDate", insertFlpConfigurationRequest.ScheduledEndDate);
                dynamicParameters.Add("@scheduledEndTime", insertFlpConfigurationRequest.ScheduledEndTime);
                dynamicParameters.Add("@blobStorageAccount", insertFlpConfigurationRequest.BlobStorageAccount);
                dynamicParameters.Add("@blobContainerName", insertFlpConfigurationRequest.blobContainerName);
                dynamicParameters.Add("@configId", insertFlpConfigurationRequest.configurationId);
                dynamicParameters.Add("@blobSourcePath", insertFlpConfigurationRequest.blobSourcePath);
                dynamicParameters.Add("@updateSchedular", insertFlpConfigurationRequest.updateSchedular);
                dynamicParameters.Add("@securityGroupId", String.Join(",", insertFlpConfigurationRequest.securityGroups.Select(c => c.securityGroupId)));
                var weekDaysIds = string.Join(",", insertFlpConfigurationRequest.weekDays);
                dynamicParameters.Add("@weekDaysIds", weekDaysIds);
                dynamicParameters.Add("@frequencyHoursId", insertFlpConfigurationRequest.hourFrequency);
                dynamicParameters.Add("@region", insertFlpConfigurationRequest.region);
                dynamicParameters.Add("@subRegion", insertFlpConfigurationRequest.subRegion);
                dynamicParameters.Add("@clientName", insertFlpConfigurationRequest.clientName);
                dynamicParameters.Add("@deltaSource", insertFlpConfigurationRequest.deltaSource);
                dynamicParameters.Add("@deltaStorageAccountId", insertFlpConfigurationRequest.deltaStorageAccountId);
                dynamicParameters.Add("@deltaContainerName", insertFlpConfigurationRequest.deltaContainerName);
                dynamicParameters.Add("@flpProcessingServerTypeId", insertFlpConfigurationRequest.dataSource);
                dynamicParameters.Add("@sharePointApplicationId", insertFlpConfigurationRequest.SharePointApplicationId);
                dynamicParameters.Add("@sharePointApplicationSiteId", insertFlpConfigurationRequest.SharePointApplicationSiteId);
                dynamicParameters.Add("@sharePointLibraryName", insertFlpConfigurationRequest.SharePointLibraryName);
                dynamicParameters.Add("@sharePointFolderPath", insertFlpConfigurationRequest.SharePointFolderPath);
                var dbResponse = await _dapperService.InsertDataAsync<string>("[commit_InsertFlpConfiguration]", dynamicParameters, commandType: CommandType.StoredProcedure);

                // Retrieve the newly inserted FlpConfigurationId
                var newFlpConfigurationId = dynamicParameters.Get<string>("@FlpConfigurationId");

                return newFlpConfigurationId; // Return the main response (if needed)
            }
            catch (Exception ex)
            {
                // Handle exceptions (logging, etc.)
                throw;
            }
        }

        public async Task<int> InsertFlpFileConfigurationDetails(FlpFileConfigurationRequest fileConfig, string flpConfigurationId, string createdBy)
        {
            try
            {

                var fileConfigParams = new DynamicParameters();
                fileConfigParams.Add("@flpConfigurationId", flpConfigurationId); // Use the new FlpConfigurationId
                fileConfigParams.Add("@delimiter", fileConfig.Delimiter);
                fileConfigParams.Add("@quote_character", fileConfig.QuoteCharacter);
                fileConfigParams.Add("@is_header_provided", fileConfig.IsHeaderProvided);
                fileConfigParams.Add("@skip_rows", fileConfig.SkipRows);
                fileConfigParams.Add("@skip_footer_rows", fileConfig.SkipFooterRows);
                fileConfigParams.Add("@key_column_list", fileConfig.KeyColumnList);
                fileConfigParams.Add("@column_name_list", fileConfig.ColumnNameList);
                fileConfigParams.Add("@convert_datatypes_column_list", fileConfig.ConvertDatatypesColumnList);
                fileConfigParams.Add("@ignore_duplicate_rows", fileConfig.IgnoreDuplicateRows);
                fileConfigParams.Add("@dedup", fileConfig.dedup);
                fileConfigParams.Add("@do_not_archive_file", fileConfig.DoNotArchiveFile);
                fileConfigParams.Add("@keep_first_row", fileConfig.KeepFirstRow);
                fileConfigParams.Add("@createdBy", createdBy);
                fileConfigParams.Add("@db_file_column_name_list", fileConfig.db_file_column_name_list);
                fileConfigParams.Add("@spanish_to_english", fileConfig.SpanishToEnglish);
                fileConfigParams.Add("@roman_numerals_only", fileConfig.RomanNumeralsOnly);
                fileConfigParams.Add("@skip_empty_lines", fileConfig.SkipEmptyLines);
                fileConfigParams.Add("@prefix", fileConfig.landingLayerPrefix);
                fileConfigParams.Add("@dateFormatId", fileConfig.dateFormatId);
                fileConfigParams.Add("@timeFormatId", fileConfig.timeFormatId);

                if (!string.IsNullOrWhiteSpace(fileConfig.tabName))
                    fileConfigParams.Add("@tabName", fileConfig.tabName);

                // Insert into FlpFileConfiguration
                int result = await _dapperService.InsertDataAsync<int>("[commit_InsertFlpFileConfiguration]", fileConfigParams, commandType: CommandType.StoredProcedure);


                return result; // Return the main response (if needed)
            }
            catch (Exception ex)
            {
                // Handle exceptions (logging, etc.)
                throw;

            }

            #endregion
        }

        public async Task<int> InsertConfigurationTableMapping(ConfigurationTableMappingRequest tableMapping, string flpConfigurationId, string createdBy, int dataSource)
        {
            try
            {

                var tableMappingParams = new DynamicParameters();
                tableMappingParams.Add("@flpConfigurationId", flpConfigurationId); // Use the new FlpConfigurationId
                tableMappingParams.Add("@tableName", tableMapping.TableName);
                tableMappingParams.Add("@createdBy", createdBy);
                tableMappingParams.Add("@databaseConfigurationId", tableMapping.DatabaseConfigurationId);
                tableMappingParams.Add("@drop_main_table", tableMapping.DropMainTable);
                tableMappingParams.Add("@drop_history_table", tableMapping.DropHistoryTable);
                tableMappingParams.Add("@validate_fileschema", tableMapping.ValidateFileSchema);
                tableMappingParams.Add("@mergeData", tableMapping.MergeData);
                tableMappingParams.Add("@createHistoryTable", tableMapping.CreateHistoryTable);
                tableMappingParams.Add("@deltaJobId", tableMapping.DeltaJobId);
                tableMappingParams.Add("@landingLayerPath", tableMapping.landingLayerAcceptedPath);
                tableMappingParams.Add("@rejectedLayerPath", tableMapping.landingLayerRejectedPath);
                if (!string.IsNullOrWhiteSpace(tableMapping.tabName))
                    tableMappingParams.Add("@tabName", tableMapping.tabName);
                if (!string.IsNullOrWhiteSpace(tableMapping.deltaSource))
                    tableMappingParams.Add("@deltaSource", tableMapping.deltaSource);
                tableMappingParams.Add("@dataSource", dataSource);
                // Insert into ConfigurationTableMapping
                int result = await _dapperService.InsertDataAsync<int>("[commit_InsertConfigurationTableMapping]", tableMappingParams, commandType: CommandType.StoredProcedure);


                return result; // Return the main response (if needed)
            }
            catch (Exception ex)
            {
                // Handle exceptions (logging, etc.)
                throw;
            }
        }

        public async Task<DatabaseResponse> AddCampaignUserAccess(FlpCampaignUserAccess campaignUserAccess)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@campaignUserAccessId", campaignUserAccess.CampaignUserAccessId);
                dynamicParameters.Add("@internalCampaignId", campaignUserAccess.InternalCampaignId);
                dynamicParameters.Add("@upn", campaignUserAccess.Upn);
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_campaignUserAccess]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<DatabaseResponse> UpdateCampaignConfiguration(FlpCampaignConfiguration campaignConfiguration)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@internalCampaignId", campaignConfiguration.InternalCampaignId);
                dynamicParameters.Add("@campaignId", campaignConfiguration.CampaignId);
                dynamicParameters.Add("@campaignName", campaignConfiguration.CampaignName);
                dynamicParameters.Add("@regionId", campaignConfiguration.RegionId);
                dynamicParameters.Add("@subRegionId", campaignConfiguration.SubRegionId);
                dynamicParameters.Add("@clientId", campaignConfiguration.ClientId);
                dynamicParameters.Add("@addedBy", campaignConfiguration.AddedBy);
                dynamicParameters.Add("@applicationId", campaignConfiguration.ApplicationId);
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_updateCampaignConfiguration]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<FlpCampaignConfiguration> GetCampaignConfigurationByCampaignId(string campaignId,string applicationId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@campaignId", campaignId);
                dynamicParameters.Add("@applicationId", applicationId);
                var dbResponse = await _dapperService.GetSingleRowAsync<FlpCampaignConfiguration>("[sel_campaignConfigurationByCampaignId]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<DatabaseResponse> UpdateCampaignUserAccess(FlpCampaignUserAccess campaignUserAccess)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@campaignUserAccessId", campaignUserAccess.CampaignUserAccessId);
                dynamicParameters.Add("@internalCampaignId", campaignUserAccess.InternalCampaignId);
                dynamicParameters.Add("@upn", campaignUserAccess.Upn);
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_updateCampaignUserAccess]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<DatabaseResponse> DeleteCampaignUserAccess(FlpCampaignUserAccess campaignUserAccess)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@campaignUserAccessId", campaignUserAccess.CampaignUserAccessId);
                dynamicParameters.Add("@internalCampaignId", campaignUserAccess.InternalCampaignId);
                dynamicParameters.Add("@upn", campaignUserAccess.Upn);
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_DeleteCampaignUserAccess]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<bool> CheckCampaignConfigurationExists(string campaignId, string campaignName, int? regionId, string subRegionId, int? clientId, string upn)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@campaignId", campaignId);
                dynamicParameters.Add("@campaignName", campaignName);
                dynamicParameters.Add("@regionId", regionId);
                dynamicParameters.Add("@subRegionId", subRegionId);
                dynamicParameters.Add("@clientId", clientId);
                dynamicParameters.Add("@upn", upn);
                var dbResponse = await _dapperService.GetSingleRowAsync<bool>("[sel_campaignConfigurationExists]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<CampaignUserAccess>> GetCampaignUserAccessInfo(string upn)
        {
            var dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@UPN", upn);
            var dbResponse = await _dapperService.GetMultipleRowsAsync<CampaignUserAccess>("[sel_CampaignUserAccessByUPN]", dynamicParameters, commandType: CommandType.StoredProcedure);
            return dbResponse.ToList();
            
        }


        // Add this method to the existing ProcessConfigurationRepositoryV4_1 class

        public async Task<List<FlpCampaignConfiguration>> GetCampaignByClientGeoMapping(int? regionId, string? subRegionId, int? clientId,string? applicationId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@regionId", regionId);
                dynamicParameters.Add("@subRegionId", subRegionId);
                dynamicParameters.Add("@clientId", clientId);
                dynamicParameters.Add("@applicationId", applicationId);

                var dbResponse = await _dapperService.GetMultipleRowsAsync<FlpCampaignConfiguration>(
                    "[sel_campaignDetailsByClientGeoMapping]",
                    dynamicParameters,
                    commandType: CommandType.StoredProcedure);

                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }


        public async Task<List<FlpCampaignConfiguration>> GetCampaignDetails(string upn,string applicationId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                if (!string.IsNullOrWhiteSpace(upn))
                {
                    dynamicParameters.Add("@upn", upn);
                }
                dynamicParameters.Add("@applicationId", applicationId);
                var dbResponse = await _dapperService.GetMultipleRowsAsync<FlpCampaignConfiguration>(
                    "[sel_campaignDetails]",
                    dynamicParameters,
                    commandType: CommandType.StoredProcedure);

                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        // Add this method to the existing StatusRepository class

        // Update the existing method in ProcessConfigurationRepositoryV4_1 class
        public async Task<CampaignProcessedRecordsCountResponseV2?> GetCampaignProcessedRecordsCountAsyncV2(string applicationId, CampaignProcessedRecordsCountRequest request)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@campaignId", request.CampaignId);
                dynamicParameters.Add("@applicationId", applicationId);

                // Add optional date parameters - convert to DateTime for stored procedure
                DateTime? fromDateTime = null;
                DateTime? toDateTime = null;

                if (!string.IsNullOrWhiteSpace(request.FromDate))
                {
                    if (DateTime.TryParse(request.FromDate, out var fromDate))
                    {
                        fromDateTime = fromDate;
                        dynamicParameters.Add("@fromDateTime", fromDateTime);
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.ToDate))
                {
                    if (DateTime.TryParse(request.ToDate, out var toDate))
                    {
                        toDateTime = toDate.Date.AddDays(1).AddTicks(-1); // End of day
                        dynamicParameters.Add("@toDateTime", toDateTime);
                    }
                }

                using var connection = _dapperService.CreateConnection();
                using var multi = await connection.QueryMultipleAsync(
                    "[sel_CampaignProcessedRecordsCount1]",
                    dynamicParameters,
                    commandType: CommandType.StoredProcedure);

                // First result set: Main campaign processed records count
                var mainResult = await multi.ReadFirstOrDefaultAsync<CampaignProcessedRecordsCountResponseV2>();

                if (mainResult == null)
                    return null;

                // Second result set: Rule-wise invalid records count
                var ruleWiseData = (await multi.ReadAsync<RuleWiseInvalidRecords>()).ToList();

                // Transform rule-wise data to the required format
                if (ruleWiseData.Any())
                {
                    mainResult.InvalidRecordsRuleWise = ruleWiseData
                        .Where(r => r.TotalFailedRowsCount > 0)
                        .Select(r => new Dictionary<string, int> { { r.RuleTypeName, r.TotalFailedRowsCount } })
                        .ToList();

                    // Add "Record not processed due to other validation failure" if there are unaccounted records
                    var totalRuleFailures = ruleWiseData.Sum(r => r.TotalFailedRowsCount);
                    if (mainResult.InvalidRecordsCount > totalRuleFailures)
                    {
                        var otherFailures = mainResult.InvalidRecordsCount - totalRuleFailures;
                        if (otherFailures > 0)
                        {
                            mainResult.InvalidRecordsRuleWise.Add(new Dictionary<string, int>
                    {
                        { "Record not processed due to other validation failure", otherFailures }
                    });
                        }
                    }
                }

                return mainResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting campaign processed records count for campaignId: {CampaignId}, FromDate: {FromDate}, ToDate: {ToDate}",
                    request.CampaignId, request.FromDate, request.ToDate);
                throw;
            }
        }


        public async Task<CampaignProcessedRecordsCountResponse?> GetCampaignProcessedRecordsCountAsync(string applicationId, CampaignProcessedRecordsCountRequest request)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@campaignId", request.CampaignId);
                dynamicParameters.Add("@applicationId", applicationId);

                // Add optional date parameters
                if (!string.IsNullOrWhiteSpace(request.FromDate))
                    dynamicParameters.Add("@fromDate", request.FromDate);

                if (!string.IsNullOrWhiteSpace(request.ToDate))
                    dynamicParameters.Add("@toDate", request.ToDate);

                var result = await _dapperService.GetSingleRowAsync<CampaignProcessedRecordsCountResponse>(
                    "sel_CampaignProcessedRecordsCount",
                    dynamicParameters,
                    commandType: CommandType.StoredProcedure);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting campaign processed records count for campaignId: {CampaignId}, FromDate: {FromDate}, ToDate: {ToDate}",
                    request.CampaignId, request.FromDate, request.ToDate);
                throw;
            }
        }

    }
}
