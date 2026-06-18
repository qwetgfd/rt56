using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration
{
    public class FileConfigurationEntity
    {
        public long Id { get; set; }
        public string flpConfigurationId { get; set; }
        public string process_name { get; set; }
        public string delimiter { get; set; }
        public string quote_character { get; set; }
        public bool is_header_provided { get; set; }        
        public int skip_rows { get; set; }
        public int skip_footer_rows { get; set; }
        public string key_column_list { get; set; }
        public string column_name_list { get; set; }
        public string db_file_columnName_list { get; set; }
        
        public string convert_datatypes_column_list { get; set; }
        public string database_name { get; set; }
        public string table_name { get; set; }
        public bool is_create_history_table { get; set; }
        public string sender_communication_email { get; set; }
        public string created_by { get; set; }
        public string userName { get; set; }
        public string current_timestamp { get; set; }
        //public int ccmsid { get; set; }
        public string loginid { get; set; }

        public string azure_csv_root_path { get; set; }

        public string azure_parquet_root_path { get; set; }

        public string support_communication_email { get; set; }

        public DateTime? modified_date { get; set; }

        public DateTime? created_date { get; set; }

        public string? root_database_folder_path { get; set; }

        public string? parquet_compression { get; set; }

        public bool? is_active { get; set; }

        public int Count { get; set; }

        public bool drop_main_table { get; set; }

        public bool drop_history_table { get; set; }

        public string order_by_column_list_for_dedup { get; set; }

        public bool validate_fileschema { get; set; }

        public bool ignore_duplicate_rows { get; set; }
        public bool do_not_archive_file { get; set; }
        public bool spanish_to_english { get; set; }
        public bool roman_numerals_only { get; set; }
        public string search_string_in_file_name { get; set; }
        public string process_group_name { get; set; }
        public string notebook_name_to_run { get; set; }

        public string billing_client_name { get; set; }
        public string Description { get; set; }
        public bool skip_empty_lines { get; set; }
        public bool keep_first_row { get; set; }

        public string RegionId { get; set; }
        public string SubRegionId { get; set; }
        public string ClientId { get; set; }
        public string databaseConfigurationId { get; set; }
        public int databaseNameId { get; set; }
        public string securityGroupId { get; set; }
        public string region { get; set; }
        public string subRegion { get; set; }
        public string clientName { get; set; }

        public bool mergeData { get; set; }
        public bool createHistoryTable { get; set; }
        public int dataSource { get; set; } //this is the fileProcessingServerTypeId
        public string deltaTableName { get; set; }
        public int deltaServerNameId { get; set; }
        public string deltaJobId { get; set; }
        public string deltaStorageAccountId { get; set; }
        public string deltaContainerName { get; set; }
        public string deltaSource { get; set; }
        public string datalakeStorageAccountPath { get; set; }
        public List<FileColumnMapping>? FileColumnMapping { get; set; }

        public SecurityGroup[] securityGroups { get; set; }

    }

    public class FileColumnMapping
    {
        public string fileColumn { get; set; }

        public string dbColumn { get; set;}

        public string dataType { get; set; }

        public int formatId { get; set; }
        public string region { get; set; }
        public string subRegion { get; set; }
        public string clientName { get; set; }
    }
}
