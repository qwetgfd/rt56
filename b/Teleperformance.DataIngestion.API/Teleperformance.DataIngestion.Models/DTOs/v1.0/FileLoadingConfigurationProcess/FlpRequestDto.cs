using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess
{
    public class FlpRequestDto
    {
        public string FlpConfigurationId { get; set; }
        public string ProcessName { get; set; }
        public BlobClientDetails? BlobClients { get; set; }
        public OnPremFileLocation? OnPremFileLocation { get; set; }
        #region SharePoint Workspace - AY
        public SharePointFileLocation? SharePointFileLocation { get; set; }
        #endregion
    }
}
