using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0
{
    public class BlobClientDetails : BlobLocationFile
    {
        public string UploadedId { get; set; }
    }
}
