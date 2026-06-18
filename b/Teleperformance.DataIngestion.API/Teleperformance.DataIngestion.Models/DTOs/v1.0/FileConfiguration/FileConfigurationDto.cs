using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration
{
    public class FileConfigurationDto
    {
        public AdditionalSettings additionalSettings { get; set; }
        public List<ColumnNameDatatypeName> columnNameDatatypeNames { get; set; }
    }

    public class AdditionalSettings
    {
        public string flpConfigurationId { get; set; }
        //processName: string;
        public string processName { get; set; }
        //description: string;
        public string description { get; set; }
        //delimiter: string;
        public string delimiter { get; set; }
        //flexCheckHasHeaders: boolean;
        public bool flexCheckHasHeaders { get; set; }
        //flexCheckSkipEmptyLines: boolean;
        public bool flexCheckSkipEmptyLines { get; set; }
        //flexCheckQuoteCharacter: string;
        public string txtQuoteCharacter { get; set; }
        //flexCheckEscapeCharacter: string;
        public string column_name_list { get; set; }
        public string txtEscapeCharacter { get; set; }
        public string order_by_column_list_for_dedup { get; set; }
        public bool is_active { get; set; }
        public bool do_not_archive_file { get; set; }
        public bool spanish_to_english { get; set; }
        public bool roman_numerals_only { get; set; }
        public bool ignore_duplicate_rows { get; set; }
        public bool keep_first_row { get; set; }
        public int skip_header_rows { get; set; }
        public int skip_footer_rows { get; set; }

        //databse settings
        public string tableName { get; set; }
        public string databaseName { get; set; }
        public string databaseConfigurationId { get; set; }
        public bool validate_fileschema { get; set; }
        public bool drop_history_table { get; set; }
        public bool drop_main_table { get; set; }

        public string RegionId { get; set; }
        public string SubRegionId { get; set; }
        public string ClientId { get; set; }
        public string sender_communication_email { get; set; }
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
        public SecurityGroup[] securityGroups { get; set; }
    }

    public class ColumnNameDatatypeName
    {
        public string ColumnName { get; set; }
        public string DbColumnName { get; set; }
        public string DatatypeName { get; set; }
        public int dateTimeFormatId { get; set; }

        public bool ColumnKey { get; set; }
    }
}
