using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration
{
    public class FileValueRequest
    {
        public string FileName { get; set; }
        public string AddedBy { get; set; }
        public string DateTimeUploaded { get; set; }
        public string FlpConfigurationId { get; set; }
        public string FlpProcessStatusId { get; set; }
        public string FlpProceeAttempt { get; set; }
        public string UploadFileId { get; set; }
    }

    public class EIBFileValueRequest
    {
        public string UploadFileId { get; set; }
        public string FileName { get; set; }
        public string EIBId { get; set; }
        public string AddedBy { get; set; } //ntid
        public string AddedByName { get; set; } //fullname
        public string DateTimeUploaded { get; set; }
        
    }
}
