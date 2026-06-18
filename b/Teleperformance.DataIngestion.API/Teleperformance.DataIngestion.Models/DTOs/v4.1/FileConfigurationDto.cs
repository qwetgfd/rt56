using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v4._1.LandingLayer;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1
{
    public class FileConfigurationDto
    {
        public ProcessSettings processSettings { get; set; }
        public List<FileSettings> fileSettings { get; set; }


    }

    public class ProcessSettings
    {
        public string flpConfigurationId { get; set; }
        //processName: string;
        public string processName { get; set; }
        //description: string;
        public string description { get; set; }
        public bool is_active { get; set; }
        public string RegionId { get; set; }
        public string SubRegionId { get; set; }
        public string ClientId { get; set; }
        public string sender_communication_email { get; set; }
        public string region { get; set; }
        public string subRegion { get; set; }
        public string clientName { get; set; }
        public string securityGroupId { get; set; }
        public SecurityGroup[] securityGroups { get; set; }
        public int dataSource { get; set; }
        public bool multisheet { get; set; }
        public bool sheetReferenceByIndex { get; set; }
    }

    public class FileSettings
    {
        public string tabName { get; set; }
        public bool ignoreSheet { get; set; }
        public AdditionalSettings additionalSettings { get; set; }
        public DatabaseSettings databaseSettings { get; set; }
        public List<ColumnNameDatatypeName> columnNameDatatypeNames { get; set; }
        public List<RuleSet> ruleSet { get; set; }
    }

    public class AdditionalSettings
    {
        public string delimiter { get; set; }
        //flexCheckHasHeaders: boolean;
        public bool flexCheckHasHeaders { get; set; }
        public int skip_header_rows { get; set; }
        public int skip_footer_rows { get; set; }
        public string txtQuoteCharacter { get; set; }
        public string column_name_list { get; set; }
        public string order_by_column_list_for_dedup { get; set; }
        public bool ignore_duplicate_rows { get; set; }
        public bool do_not_archive_file { get; set; }
        public bool keep_first_row { get; set; }
        public bool spanish_to_english { get; set; }
        public bool roman_numerals_only { get; set; }
        public bool flexCheckSkipEmptyLines { get; set; }

        #region Landing Layer
        public string? landingLayerPrefix { get; set; }
        public int? landingLayerDateformat { get; set; }
        public int? landingLayerTimeformat { get; set; }
        public List<int?>? landingLayerFileExtension { get; set; } = new List<int?>();
        public List<LandingLayerRegex> landingLayerRegex { get; set; } = new List<LandingLayerRegex>();

        #endregion

        public string campaignId { get; set; }
        public string internalCampaignId { get; set; }

    }

    public class DatabaseSettings
    {
        public string tableName { get; set; }
        public bool drop_history_table { get; set; }
        public bool drop_main_table { get; set; }
        public string databaseConfigurationId { get; set; }
        public bool validate_fileschema { get; set; }
        public string databaseName { get; set; }
        public bool mergeData { get; set; }
        public bool createHistoryTable { get; set; }
        public string deltaStorageAccountId { get; set; }
        public string deltaContainerName { get; set; }
        public string deltaSource { get; set; }
        public string deltaJobId { get; set; }
        public string deltaTableName { get; set; }
        public int? deltaServerNameId { get; set; }
        public string? landingLayerAcceptedPath { get; set; }
        public string? landingLayerRejectedPath { get; set; }
    }

    public class ColumnNameDatatypeName
    {
        public string ColumnName { get; set; }
        public string DbColumnName { get; set; }
        public string DatatypeName { get; set; }
        public int dateTimeFormatId { get; set; }
        public bool ColumnKey { get; set; }
    }

    public class InsertRuleSetsRequest
    {
        public RuleSet[] RuleSets { get; set; }
        public string description { get; set; }
        public string created_by { get; set; }
        public string username { get; set; }
    }

    public class RuleSet
    {
        public int id { get; set; }
        public string ruleSetNameId { get; set; }
        public string ruleSetName { get; set; }
        public int ruleTypeId { get; set; }
        public int? subRuleId { get; set; }
        public string[] ruleColumnName { get; set; }
        public string ruleColumnName2 { get; set; }
        public string ruleDescription { get; set; }

        public string prompt { get; set; }
        public string format { get; set; }
        public int? patternId { get; set; }
        public int? conditionId { get; set; }
        public decimal? fromValue { get; set; }
        public decimal? toValue { get; set; }
        public bool? isCombinationRule { get; set; }
        public bool isActive { get; set; }
        public bool? isGlobal { get; set; }
        public bool? isAllowNullOrEmptySpaces { get; set; }
        public int ruleSetType { get; set; }
        public int? spNameId { get; set; }
        public bool isUpdated { get; set; }

    }


}