using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v4._0.DataBricksAPIJobStatus
{
    public class TempFileConfiguration
    {
        public string flpConfigurationId { get; set; }
        public int destinationLocationTypeId { get; set; }
        public string blobName { get; set; }
        public string backUpFileName { get; set; }
        public float fileSize { get; set; }
        public string csvTempBlobName { get; set; }
    }
}
