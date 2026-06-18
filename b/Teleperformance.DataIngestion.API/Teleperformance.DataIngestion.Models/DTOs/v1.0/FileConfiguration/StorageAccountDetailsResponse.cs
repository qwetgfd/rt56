using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration
{
    public class StorageAccountDetailsResponse
    {
        public int storageAccountId { get; set; }
        public string storageAccountName { get; set; }
        public string containerName { get; set; }
        public int configurationProcessType { get; set; }
    }
}
