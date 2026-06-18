using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Models.v1._0
{
    public class SharedFileLocation
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }

        public long FileSize { get; set; }
    }
}
