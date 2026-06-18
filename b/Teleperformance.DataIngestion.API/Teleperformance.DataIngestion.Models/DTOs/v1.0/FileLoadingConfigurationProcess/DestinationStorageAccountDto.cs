using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess
{
    public class DestinationStorageAccountDto
    {
        public string FlpConfigurationId { get; set; }
        public string FlpStorageAccount { get; set; }
        public string StorageContainerName { get; set; }
        public string StorageAccountKey { get; set; }
        public string SasKey { get; set; }
        public bool SasKeyToken { get; set; }
    }
}
