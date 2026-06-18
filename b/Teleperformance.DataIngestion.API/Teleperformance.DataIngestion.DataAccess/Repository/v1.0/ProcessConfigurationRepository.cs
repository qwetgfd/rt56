using Dapper;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using System.Data;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.DataProfilling;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v4._0;
using Teleperformance.DataIngestion.Models.Entities.v4._1.DataProfilling;
using static Dapper.SqlMapper;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v1._0
{
    public class ProcessConfigurationRepository : IProcessConfigurationRepository
    {
        private readonly ILogger<ProcessConfigurationRepository> _logger;
        private readonly IDapperService _dapperService;

        public ProcessConfigurationRepository(ILogger<ProcessConfigurationRepository> logger, IDapperService dapperService)
        {
            this._logger = logger;
            this._dapperService = dapperService;
        }
        public async Task<List<DataTypeName>> GetAllDbDataTypes()
        {
            try
            {
                var dbResponse = await _dapperService.GetMultipleRowsAsync<DataTypeName>("[sel_DatatypeNames]", null, commandType: CommandType.StoredProcedure);
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<List<DateTimeFormat>> GetAllDbDateTimeFormats(bool displayOnLandingLayer)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@displayOnLandingLayer", displayOnLandingLayer);
                var dbResponse = await _dapperService.GetMultipleRowsAsync<DateTimeFormat>("[sel_DateTimeFormats]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<DIClientnames>> GetAllDIClientnames()
        {
            try
            {
                var dbResponse = await _dapperService.GetMultipleRowsAsync<DIClientnames>("[sel_ClientNames]", null, commandType: CommandType.StoredProcedure);
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<List<DIRegions>> GetAllDIRegionsAsync()
        {
            try
            {
                var dbResponse = await _dapperService.GetMultipleRowsAsync<DIRegions>("[sel_Regions]", null, commandType: CommandType.StoredProcedure);
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<List<DISubRegions>> GetAllDISubRegionsAsync()
        {
            try
            {
                var dbResponse = await _dapperService.GetMultipleRowsAsync<DISubRegions>("[sel_subregions]", null, commandType: CommandType.StoredProcedure);
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<List<FileConfigurationEntity>> GetAllProcessNamesByLoginId(string loginId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@LoginId", loginId);

                var dbResponse = await _dapperService.GetMultipleRowsAsync<FileConfigurationEntity>("[sel_flpConfiguration_ByLoginId]", dynamicParameters, commandType: CommandType.StoredProcedure);

                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<DIDatabaseNames>> GetDIDatabaseNames(int regionId, string subRegionId, int clientNameId,string securityGroupId, int fileProcessingServerTypeId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@regionId", regionId);
                dynamicParameters.Add("@subRegionId", subRegionId);
                dynamicParameters.Add("@clientNameId", clientNameId);
                dynamicParameters.Add("@securityGroupId", securityGroupId);
                dynamicParameters.Add("@fileProcessingServerTypeId", fileProcessingServerTypeId);

                var dbResponse = await _dapperService.GetMultipleRowsAsync<DIDatabaseNames>("[sel_DatabaseName]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<FileConfigurationEntity?> GetFileConfigurationByIdAsync(string id)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", id);

                //var dbResponse = await _dapperService.GetSingleRowAsync<FileConfigurationEntity>("[sel_flpConfigurationByFlpProcessConfigId]", dynamicParameters, commandType: CommandType.StoredProcedure);

                using var con = _dapperService.CreateConnection();

                var dbResponse = await con.QueryMultipleAsync("dbo.sel_flpConfigurationByFlpProcessConfigId", dynamicParameters, commandType: CommandType.StoredProcedure);
                var fileConfig = dbResponse.Read<FileConfigurationEntity>().FirstOrDefault();                

                if (fileConfig != null)
                {
                    fileConfig.FileColumnMapping = dbResponse.Read<FileColumnMapping>().ToList();               
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

        public async Task<bool> InsertConfigurationRegionMapping(ConfigurationRegionMapping configurationRegionMapping)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", configurationRegionMapping.flpConfigurationId);
                dynamicParameters.Add("@RegionId", configurationRegionMapping.RegionId);
                dynamicParameters.Add("@SubRegionId", configurationRegionMapping.SubRegionId);
                dynamicParameters.Add("@ClientId", configurationRegionMapping.ClientId);
                dynamicParameters.Add("@CreatedBy", configurationRegionMapping.CreatedBy);
                dynamicParameters.Add("@CreationDateTime", configurationRegionMapping.CreationDateTime);
                dynamicParameters.Add("@ModificationDateTime", configurationRegionMapping.ModificationDateTime);
                dynamicParameters.Add("@databaseConfigurationId", configurationRegionMapping.databaseConfigurationId);
                dynamicParameters.Add("@region", configurationRegionMapping.region);
                dynamicParameters.Add("@subRegion", configurationRegionMapping.subRegion);
                dynamicParameters.Add("@clientName", configurationRegionMapping.clientName);
                var dbResponse = await _dapperService.InsertDataAsync<int>("[add_ConfigurationRegionMapping]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse != null;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<int> InsertFileConfiguration(FileConfigurationEntity fileConfiguration)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", fileConfiguration.flpConfigurationId);
                dynamicParameters.Add("@process_name", fileConfiguration.process_name);
                dynamicParameters.Add("@is_active", fileConfiguration.is_active);
                dynamicParameters.Add("@delimiter", fileConfiguration.delimiter);
                dynamicParameters.Add("@quote_character", fileConfiguration.quote_character);
                dynamicParameters.Add("@is_header_provided", fileConfiguration.is_header_provided);
                dynamicParameters.Add("@skip_rows", fileConfiguration.skip_rows);
                dynamicParameters.Add("@skip_footer_rows", fileConfiguration.skip_footer_rows);
                dynamicParameters.Add("@key_column_list", fileConfiguration.key_column_list);
                dynamicParameters.Add("@column_name_list", fileConfiguration.column_name_list);
                dynamicParameters.Add("@db_file_column_name_list", fileConfiguration.db_file_columnName_list);                
                dynamicParameters.Add("@convert_datatypes_column_list", fileConfiguration.convert_datatypes_column_list);
                dynamicParameters.Add("@parquet_compression", "gzip");
                dynamicParameters.Add("@root_database_folder_path", "brz-data-ingestion/delta_tables/");
                dynamicParameters.Add("@database_name", fileConfiguration.database_name);
                dynamicParameters.Add("@table_name", fileConfiguration.table_name);
                dynamicParameters.Add("@is_create_history_table", fileConfiguration.is_create_history_table);
                dynamicParameters.Add("@sender_communication_email", fileConfiguration.sender_communication_email);
                dynamicParameters.Add("@support_communication_email", fileConfiguration.support_communication_email);
                dynamicParameters.Add("@created_by", fileConfiguration.created_by);
                dynamicParameters.Add("@userName", fileConfiguration.userName);
                dynamicParameters.Add("@created_date", fileConfiguration.created_date);
                dynamicParameters.Add("@modified_date", DateTime.Parse(fileConfiguration.current_timestamp));
                dynamicParameters.Add("@current_timestamp", fileConfiguration.current_timestamp.ToString());
                dynamicParameters.Add("@loginid", fileConfiguration.loginid);
                dynamicParameters.Add("@drop_main_table", fileConfiguration.drop_main_table);
                dynamicParameters.Add("@drop_history_table", fileConfiguration.drop_history_table);
                dynamicParameters.Add("@order_by_column_list_for_dedup", fileConfiguration.order_by_column_list_for_dedup);
                dynamicParameters.Add("@validate_fileschema", fileConfiguration.validate_fileschema);
                dynamicParameters.Add("@ignore_duplicate_rows", fileConfiguration.ignore_duplicate_rows);
                dynamicParameters.Add("@do_not_archive_file", fileConfiguration.do_not_archive_file);
                dynamicParameters.Add("@search_string_in_file_name", fileConfiguration.search_string_in_file_name);
                dynamicParameters.Add("@process_group_name", fileConfiguration.process_group_name);
                dynamicParameters.Add("@billing_client_name", fileConfiguration.billing_client_name);
                dynamicParameters.Add("@Description", fileConfiguration.Description);
                dynamicParameters.Add("@keep_first_row", fileConfiguration.keep_first_row);
                dynamicParameters.Add("@databaseConfigurationId", fileConfiguration.databaseConfigurationId);
                dynamicParameters.Add("@RegionId", fileConfiguration.RegionId);
                dynamicParameters.Add("@SubRegionId", fileConfiguration.SubRegionId);
                dynamicParameters.Add("@ClientId", fileConfiguration.ClientId);
                dynamicParameters.Add("@securityGroupId",String.Join(",", fileConfiguration.securityGroups.Select(c => c.securityGroupId)));
                dynamicParameters.Add("@region", fileConfiguration.region);
                dynamicParameters.Add("@subRegion", fileConfiguration.subRegion);
                dynamicParameters.Add("@clientName", fileConfiguration.clientName);
                dynamicParameters.Add("@spanishToEnglish", fileConfiguration.spanish_to_english);
                dynamicParameters.Add("@romanNumeralsOnly", fileConfiguration.roman_numerals_only);
                dynamicParameters.Add("@skipEmptyLines", fileConfiguration.skip_empty_lines);
                dynamicParameters.Add("@mergeData", fileConfiguration.mergeData);
                dynamicParameters.Add("@createHistoryTable", fileConfiguration.createHistoryTable);
                dynamicParameters.Add("@fileProcessingServerTypeId", fileConfiguration.dataSource);
                dynamicParameters.Add("@storageAccountId", fileConfiguration.deltaStorageAccountId);
                dynamicParameters.Add("@deltaContainerName", fileConfiguration.deltaContainerName);
                dynamicParameters.Add("@deltaSource", fileConfiguration.deltaSource);
                dynamicParameters.Add("@deltaJobId", fileConfiguration.deltaJobId);                
                var dbResponse = await _dapperService.InsertDataAsync<FileConfigurationEntity>("[Add_FileConfiguration]", dynamicParameters, commandType: CommandType.StoredProcedure);
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

        public async Task<string> VerifyProcessNameUnique(string processName, string configId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@processName", processName);
                dynamicParameters.Add("@configid", configId);
                var dbResponse = await _dapperService.GetSingleRowAsync<string>("[sel_flpProcessNameIsUnique]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public async Task<IEnumerable<GetProcessTypeResponse?>> GetProcessType()
        {
            var parameters = new DynamicParameters();
            var result = await _dapperService.GetMultipleRowsAsync<GetProcessTypeResponse?>("sel_GetProcessTypes", parameters, commandType: CommandType.StoredProcedure);
            return result;
        }
      

        public async Task<FlpProcessConfigurationResponse> GetFlpProcessConfigurationListAsync(FlpProcessConfigurationListRequest request)
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
            
            var result = await _dapperService.GetMultipleRowsAsync<FlpProcessConfigurationListResponse>("[sel_flpProcessConfiguration]", dynamicParameters);
            int totalCount = dynamicParameters.Get<int>("@totalCount");

            var groupedResult = result
                    .GroupBy(x => x.ProcessName)
                    .SelectMany(group => group
                        .Select((item, index) =>
                        {
                            item.RowNo = index + 1; // Assuming RowNo is a property in your model
                            return item;
                        }))
                    .ToList();

           return new FlpProcessConfigurationResponse
            {
                Response = groupedResult.ToList(),
                TotalCount = totalCount
            };
        }

        public async Task<IEnumerable<FileServerDetailsResponse?>> GetfileServerDetails()
        {
            var parameters = new DynamicParameters();
            var result = await _dapperService.GetMultipleRowsAsync<FileServerDetailsResponse?>("sel_fileServerDetails", parameters, commandType: CommandType.StoredProcedure);
            return result;
        }

        public async Task<IEnumerable<StorageAccountDetailsResponse?>> GetstorageAccountDetails(int fileProcessingServerTypeId)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@fileProcessingServerTypeId", fileProcessingServerTypeId);
            var result = await _dapperService.GetMultipleRowsAsync<StorageAccountDetailsResponse?>("sel_storageAccountDetails", parameters, commandType: CommandType.StoredProcedure);
            return result;
        }
        public async Task<string?> InsertFlpConfigurationDetails(InsertFlpConfigurationRequest insertFlpConfigurationRequest)
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
                dynamicParameters.Add("@securityGroupId", String.Join(",",insertFlpConfigurationRequest.securityGroups.Select(c=> c.securityGroupId)));
                var weekDaysIds = string.Join(",", insertFlpConfigurationRequest.weekDays);
                dynamicParameters.Add("@weekDaysIds",weekDaysIds);
                dynamicParameters.Add("@frequencyHoursId", insertFlpConfigurationRequest.hourFrequency);
                dynamicParameters.Add("@region", insertFlpConfigurationRequest.region);
                dynamicParameters.Add("@subRegion", insertFlpConfigurationRequest.subRegion);
                dynamicParameters.Add("@clientName", insertFlpConfigurationRequest.clientName);
                dynamicParameters.Add("@deltaSource", insertFlpConfigurationRequest.deltaSource);
                dynamicParameters.Add("@deltaStorageAccountId", insertFlpConfigurationRequest.deltaStorageAccountId);
                dynamicParameters.Add("@deltaContainerName", insertFlpConfigurationRequest.deltaContainerName);
                dynamicParameters.Add("@flpProcessingServerTypeId", insertFlpConfigurationRequest.dataSource);
                dynamicParameters.Add("@internalCampaignId", insertFlpConfigurationRequest.internalCampaignId);
                dynamicParameters.Add("@sharePointApplicationId", insertFlpConfigurationRequest.SharePointApplicationId);
                dynamicParameters.Add("@sharePointApplicationSiteId", insertFlpConfigurationRequest.SharePointApplicationSiteId);
                dynamicParameters.Add("@sharePointLibraryName", insertFlpConfigurationRequest.SharePointLibraryName);
                dynamicParameters.Add("@sharePointFolderPath", insertFlpConfigurationRequest.SharePointFolderPath);
                //dynamicParameters.Add("@campaignId", insertFlpConfigurationRequest.campaignId);
                // var tabName = insertFlpConfigurationRequest.ConfigurationTableMappings?.FirstOrDefault()?.tabName;
                //if (!string.IsNullOrWhiteSpace(insertFlpConfigurationRequest.ConfigurationTableMappings?.FirstOrDefault()?.tabName))
                // dynamicParameters.Add("@tabName", tabName);
                // Insert the main FlpConfiguration record and retrieve the generated FlpConfigurationId
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
        }

        public async Task<int> InsertConfigurationTableMapping(ConfigurationTableMappingRequest tableMapping, string flpConfigurationId, string createdBy)
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
                if (!string.IsNullOrWhiteSpace(tableMapping.tabName))
                    tableMappingParams.Add("@tabName", tableMapping.tabName);
                if(!string.IsNullOrWhiteSpace(tableMapping.deltaSource))
                   tableMappingParams.Add("@deltaSource", tableMapping.deltaSource);

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
        public async Task<ProcessConfigDetail?> GetConfigurationDetailsById(string flpConfigurationId,string tabName)
        {
            var param = new DynamicParameters();
            param.Add("@flpConfigurationId", flpConfigurationId, DbType.String);
            if(!string.IsNullOrWhiteSpace(tabName))
             param.Add("@tabName", tabName, DbType.String);
            try
            {
                using var con = _dapperService.CreateConnection();
                var result = await con.QueryMultipleAsync("dbo.sel_processConfigurationById", param, commandType: CommandType.StoredProcedure);
                var processConfiguration = result.Read<ProcessConfigDetail>().FirstOrDefault();
                if (processConfiguration != null)
                {
                    processConfiguration.FileConfigurationDetails = result.Read<FileConfigurationDetail>().ToList();
                    processConfiguration.ConfigurationTableMappingDetails = result.Read<ConfigurationTableMappingDetail>().ToList();
                    processConfiguration.CustomSchedulerDetails = result.Read<CustomSchedulerDetail>().ToList();
                    processConfiguration.FileColumnMapping = result.Read<FileColumnMapping>().ToList();
                    processConfiguration.ConfigurationSecurityGroupMappingList = result.Read<ConfigurationSecurityGroupMapping>().ToList();
                    processConfiguration.FlpRuleSet = result.Read<RuleSetDto>().ToList();
                }
                return processConfiguration;

            }
            catch (Exception)
            {

                throw;
            }
        }



        public async Task<IEnumerable<ScheduerType?>> GetScheduerType()
        {
            using var con = _dapperService.CreateConnection();
            return await con.QueryAsync<ScheduerType?>("sel_info_schedulerType", commandType: CommandType.StoredProcedure);            
        }


        public async Task<IEnumerable<DateTimeFormats>?> GetDateTimeFormatList()
        {
            using var con = _dapperService.CreateConnection();
            return await con.QueryAsync<DateTimeFormats?>("sel_datetimeFormat", commandType: CommandType.StoredProcedure);
        }
        public async Task<IEnumerable<WeekDayName?>> GetWeekDayName()
        {
            using var con = _dapperService.CreateConnection();
            return await con.QueryAsync<WeekDayName?>("sel_weekDays", commandType: CommandType.StoredProcedure);
        }
        public async Task<IEnumerable<FrequencyHour?>> GetFrequencyHour()
        {
            using var con = _dapperService.CreateConnection();
            return await con.QueryAsync<FrequencyHour?>("sel_frequencyHours", commandType: CommandType.StoredProcedure);
        }        
        public async Task<int> InsertCustomSchedulerDetails(string flpConfigurationId, int frequencyHoursId, int weekDaysId)
        {
            try
            {
                var dParams = new DynamicParameters();
                dParams.Add("@flpConfigurationId", flpConfigurationId);
                dParams.Add("@frequencyHoursId", frequencyHoursId);
                dParams.Add("@weekDaysId", weekDaysId);
                return await _dapperService.InsertDataAsync<int>("[commit_customSchedulerDetails]", dParams, commandType: CommandType.StoredProcedure);
            }
            catch
            {
                throw;
            }
        }

        public async Task<DatabaseResponse> UpdateFlpFileColumnMapping(string flpConfigurationId, string tabName, string fileColumnMapping)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@process_name", null);
                dynamicParameters.Add("@tabName", null);
                dynamicParameters.Add("@table_name", null);
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

        
        //
    }
    
}


