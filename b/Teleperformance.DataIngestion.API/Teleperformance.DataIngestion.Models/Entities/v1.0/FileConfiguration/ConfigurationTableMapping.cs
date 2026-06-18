using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration
{
    public class ConfigurationTableMapping: FlpFileConfiguration
    {
       public string flpConfigurationId { get; set; }
       public string tableName { get; set; }
       public string tabName { get; set; }       
       public string databaseConnectionSecret { get; set; }
       public bool drop_history_table { get; set; }
       public bool drop_main_table { get; set; }
       public bool validate_fileschema { get; set; }
       public bool mergeData { get; set; }
       public bool createHistoryTable { get; set; }
       public string historyTableName { get; set; }
       public string unityCatalog { get; set; }
       public string? campaignId { get; set; }

    }
}
