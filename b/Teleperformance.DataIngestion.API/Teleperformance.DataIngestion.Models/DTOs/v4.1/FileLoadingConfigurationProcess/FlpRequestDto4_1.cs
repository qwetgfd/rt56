using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Enums.v3._0;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess
{
    public class FlpRequestDto4_1
    {
        public string FlpConfigurationId { get; set; }
        public string ProcessName { get; set; }
        public int? SourceLocationTypeId { get; set; }
        public string? LandingLayerUploadedId { get; set; }
        public BlobClientDetails? BlobClients { get; set; }
        public OnPremFileLocation? OnPremFileLocation { get; set; }
        public FileProcessingServerType? DataStorageType { get; set; }
    }


  
}
