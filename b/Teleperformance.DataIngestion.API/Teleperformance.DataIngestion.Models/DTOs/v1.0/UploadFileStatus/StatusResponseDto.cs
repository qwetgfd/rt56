using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus
{
    public class StatusResponseDto
    {
        public class FlpConfiguration
        {
            public int FlpConfigurationID { get; set; }  // Assuming ID is an integer
            public string FlpConfigurationName { get; set; }  // Assuming Name is a string
            public List<UploadedFile> UploadedFiles { get; set; } = new List<UploadedFile>();  // List of uploaded files
        }

        public class UploadedFile
        {
            public int UploadFileID { get; set; }  // Assuming ID is an integer
            public string UploadFile { get; set; }  // Assuming this is a file name or path as a string
            public string CurrentStatus { get; set; }  // Current status of the file
            //public List<FileStatus> FileStatuses { get; set; } = new List<FileStatus>();  // List of file statuses
        }

        public class FileStatus
        {
            public string StatusName { get; set; }  // Description of the status (e.g. "file moved to temp")
            public string Status { get; set; }  // Status result (e.g. "success", "failure", "in progress")
        }
               
    }
}
