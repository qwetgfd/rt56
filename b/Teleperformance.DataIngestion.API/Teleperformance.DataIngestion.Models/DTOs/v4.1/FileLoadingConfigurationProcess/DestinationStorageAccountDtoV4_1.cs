using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess
{
    public class DestinationStorageAccountDtoV4_1
    {
        public string FlpConfigurationId { get; set; }
        public string FlpStorageAccount { get; set; }
        public string StorageContainerName { get; set; }
        public string StorageAccountKey { get; set; }
        public string SasKey { get; set; }
    }
}
