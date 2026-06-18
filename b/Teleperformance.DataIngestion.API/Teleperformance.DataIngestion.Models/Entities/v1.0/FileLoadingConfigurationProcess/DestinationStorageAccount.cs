using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess
{
    public class DestinationStorageAccount : DatabaseResponse
    {
        public string flpConfigurationId { get; set; }
        public string storageAccountName { get; set; }
        public string storageContainerName { get; set; }
        public string storageAccountKey { get; set; }
        public string sasKey { get; set; }
        public bool sasKeyToken { get; set; }
    }
}
