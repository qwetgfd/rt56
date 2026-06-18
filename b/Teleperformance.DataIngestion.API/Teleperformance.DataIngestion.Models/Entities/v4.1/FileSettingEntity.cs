using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v4._1
{
    public class FileSettingEntity
    {
        public string flpConfigurationId { get; set; }
        public string tabName { get; set; }
        public bool ignoreSheet { get; set; }
        public string delimiter { get; set; }
        public bool flexCheckHasHeaders { get; set; }
        public int skip_header_rows { get; set; }
        public int skip_footer_rows { get; set; }
        public string txtQuoteCharacter { get; set; }
        public string key_column_list { get; set; }
        public string column_name_list { get; set; }
        public string convert_datatypes_column_list { get; set; }
        public string loginid { get; set; }
        public string order_by_column_list_for_dedup { get; set; }
        public bool ignore_duplicate_rows { get; set; }
        public bool do_not_archive_file { get; set; }
        public bool keep_first_row { get; set; }
        public bool spanish_to_english { get; set; }
        public bool roman_numerals_only { get; set; }
        public bool flexCheckSkipEmptyLines { get; set; }

        //landing layer
        public string prefix { get; set; }
        public int dateFormatId { get; set; }
        public int timeFormatId { get; set; }
    }
}
