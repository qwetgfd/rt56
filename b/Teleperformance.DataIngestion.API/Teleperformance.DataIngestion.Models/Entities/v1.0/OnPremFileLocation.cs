using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0
{
    public class OnPremFileLocation
    {
        public string UploadedId { get; set; }
        public string FileUrl { get; set; }
        public long FileSize { get; set; }
    }
}
