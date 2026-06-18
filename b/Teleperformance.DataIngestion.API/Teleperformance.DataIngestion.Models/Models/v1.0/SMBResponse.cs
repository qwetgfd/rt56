using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Models.v1._0
{
    public class SMBResponse
    {
        public List<SharedFileLocation> SharedFileLocations { get; set; }
        public bool FileIsDeleted { get; set; }
        public bool CopiedFileToArchivedFolder { get; set; }
        public Stream GetFileStream { get; set; }
        public bool CopiedFileFromBlob { get; set; }

    }
}
