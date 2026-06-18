using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess
{
    public class FlpConfiguration4_1: FlpConfiguration
    {
        public string tabName { get; set; }
        public string databaseConnectionSecret { get; set; }
        public string parquet_compression { get; set; }
        public string historyTableName { get; set; }
        public string unityCatalog { get; set; }

    }

 
}
