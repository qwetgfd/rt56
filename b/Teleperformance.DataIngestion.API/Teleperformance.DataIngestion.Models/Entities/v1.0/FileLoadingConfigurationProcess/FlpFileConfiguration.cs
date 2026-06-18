using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess
{
    public class FlpFileConfiguration
    {
        public string flpConfigurationId { get; set; }
        public string fileNameString { get; set; }
        public string flpTabName { get; set; }
        public string delimiter { get; set; }
        public string quote_character { get; set; }
        public bool is_header_provided { get; set; }
        public int skip_rows { get; set; }
        public int skip_footer_rows { get; set; }
        public string key_column_list { get; set; }
        public string column_name_list { get; set; }
        public string convert_datatypes_column_list { get; set; }
        public string parquet_compression { get; set; }
        public string order_by_column_list_for_dedup { get; set; }
        public bool ignore_duplicate_rows { get; set; }
        public bool do_not_archive_file { get; set; }
        public bool keep_first_row { get; set; }
        public bool spanishToEnglish { get; set; }
        public bool romanNumeralsOnly { get; set; }
        public bool skipEmptyLines { get; set; }

       
    }
}
