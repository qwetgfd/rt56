using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v4._1
{
    public class DatabaseSettingEntity
    {
        public string flpConfigurationId { get; set; }
        public string tabName { get; set; }
        public bool ignoreSheet { get; set; }
        public string process_name { get; set; }
        public string db_file_columnName_list { get; set; }
        public string table_name { get; set; }
        public string loginid { get; set; }
        public bool drop_main_table { get; set; }
        public bool drop_history_table { get; set; }
        public bool validate_fileschema { get; set; }
        public string databaseConfigurationId { get; set; }
        public bool mergeData { get; set; }
        public bool createHistoryTable { get; set; }
        public int dataSource { get; set; }
        public string deltaStorageAccountId { get; set; }
        public string deltaContainerName { get; set; }
        public string deltaSource { get; set; }
        public string deltaJobId { get; set; }
        public string landingLayerRejectedPath { get; set; }
        public string landingLayerAcceptedPath { get; set; }
    }
}
