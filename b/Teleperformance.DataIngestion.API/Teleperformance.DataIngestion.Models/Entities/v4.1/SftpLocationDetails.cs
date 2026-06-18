using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v4._1
{
    public class SftpLocationDetails
    {
        public string FlpConfigurationId { get; set; }
        public string UploadFileId { get; set; }
    }

    public class FileLocationDetails
    {
        public string FolderLocation { get; set; }
    }
}

