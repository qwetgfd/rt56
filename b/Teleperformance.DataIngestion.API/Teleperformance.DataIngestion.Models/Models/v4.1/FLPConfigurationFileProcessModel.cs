using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.Models.Models.v4._1
{
    public class FLPConfigurationFileProcessModel
    {
        public FLPConfigurationModel flpConfiguration { get; set; }
        public string UploadedFileId { get; set; }
        public BlobClientDetails BlobClients { get; set; }
    }
}
