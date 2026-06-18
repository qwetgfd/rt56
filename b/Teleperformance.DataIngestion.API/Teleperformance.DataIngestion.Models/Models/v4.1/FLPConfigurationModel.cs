using NPOI.OpenXmlFormats.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v4._1.DataProfilling;

namespace Teleperformance.DataIngestion.Models.Models.v4._1
{
    public class FLPConfigurationModel
    {
        public ProcessSettings processSettings { get; set; }
        public List<FileSettings> fileSettings { get; set; }
    }

    public class ProcessSettings
    {
        public long Id { get; set; }
        public string process_name { get; set; }
        public string Description { get; set; }
        public string flpConfigurationId { get; set; }
        public int locationTypeId { get; set; }
        public string destinationLocationTypeId { get; set; }
        public bool? is_active { get; set; }
        public string sourcePath { get; set; }
        public string destinationPath { get; set; }
        public string sender_communication_email { get; set; }
        public string support_communication_email { get; set; }
        public string loginid { get; set; }
        public int processTypeId { get; set; }
        public string RegionId { get; set; }
        public string SubRegionId { get; set; }
        public string ClientId { get; set; }
        public string region { get; set; }
        public string subRegion { get; set; }
        public string clientName { get; set; }
        public int dataSource { get; set; }
        public bool multisheet { get; set; }
        public bool sheetReferenceByIndex { get; set; }
        public string campaignId { get; set; }
        public string internalCampaignId { get; set; }
        public SecurityGroup[] securityGroups { get; set; }
    }



    public class FileSettings
    {
        public string tabName { get; set; }
        public bool ignoreSheet { get; set; }
        public AdditionalSettings additionalSettings { get; set; }
        public DatabaseSettings databaseSettings { get; set; }
        public List<FileColumnMapping> FileColumnMapping { get; set; }

        public List<RuleSet> RuleSets { get; set; }

    }

    public class AdditionalSettings
    {
        public string delimiter { get; set; }
        public string quote_character { get; set; }
        public bool is_header_provided { get; set; }
        public string key_column_list { get; set; }
        public string column_name_list { get; set; }
        public string convert_datatypes_column_list { get; set; }
        public string order_by_column_list_for_dedup { get; set; }
        public bool ignore_duplicate_rows { get; set; }
        public bool do_not_archive_file { get; set; }
        public bool keep_first_row { get; set; }
        public int skip_rows { get; set; }
        public int skip_footer_rows { get; set; }
        public bool skip_empty_lines { get; set; }
        public bool spanish_to_english { get; set; }
        public bool roman_numerals_only { get; set; }
        
        public string LandingLayerFileExtension { get; set; }
        public string Regex { get; set; }
        public string LandingLayerPrefix { get; set; }
        public int LandingLayerDateFormatId { get; set; }
        public int LandingLayerTimeFormatId { get; set; }

    }

    public class DatabaseSettings
    {
        public bool drop_main_table { get; set; }
        public bool drop_history_table { get; set; }
        public bool validate_fileschema { get; set; }
        public string databaseConfigurationId { get; set; }
        public string table_name { get; set; }
        public bool mergeData { get; set; }
        public bool createHistoryTable { get; set; }
        public string deltaJobId { get; set; }
        public string datalakeStorageAccountPath { get; set; }
        public string database_name { get; set; }
        public string deltaContainerName { get; set; }
        public string deltaStorageAccountId { get; set; }
        public string? landingLayerAcceptedPath { get; set; }
        public string? landingLayerRejectedPath { get; set; }
    }

    public class FileColumnMapping
    {
        public string fileColumn { get; set; }

        public string dbColumn { get; set; }

        public string dataType { get; set; }

        public int dataTypeId { get; set; }

        public int formatId { get; set; }
    }

    
}
