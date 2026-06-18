using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v4._1
{
    public class LogUploadedFileRequest
    {
        public string fileName { get; set; }
        public long fileSize { get; set; }
        public string uploadedDateTime { get; set; }
        public string uploadedBy { get; set; }
        public string? flpConfigurationId { get; set; }
        public string? uploadFileId { get; set; }
        public string? securityGroupId { get; set; }
    }
}
