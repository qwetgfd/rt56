using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess
{
    public class ConfigurationTableMappingDto : FlpFileBaseConfigurationDto
    {
        public string FlpConfigurationId { get; set; }
        public string TableName { get; set; }
        public string TabName { get; set; }
        public string DatabaseConnectionSecret { get; set; }
        public bool DropHistoryTable { get; set; }
        public bool DropMainTable { get; set; }
        public bool ValidateFileSchema { get; set; }
        public bool mergeData { get; set; }
        public bool createHistoryTable { get; set; }
        public string historyTableName { get; set; }
        public string UnityCatalog { get; set; }
        public string? campaignId { get; set; }
    }
}
