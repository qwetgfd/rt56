using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus
{
    public class UploadFileStatusResponseDto
    {
        public string FlpConfigurationID { get; set; }  // Represents the ID of the configuration
        public string UploadFileID { get; set; }        // Represents the ID of the uploaded file
        public string UploadFile { get; set; }        // Represents the name of the uploaded file
        public string DatabaseName { get; set; }
        public string TableName { get; set; }
        public int TotalRecords { get; set; }
        public int ProcessedRecords { get; set; }
        public int DuplicateRecords { get; set; }
        public string Description { get; set; }
        public string? TabName { get; set; }
        public string? blobName { get; set; }
        public string? statusCode { get; set; }
        public List<UIdFileStatus> FileStatus { get; set; }           // List of status logs for the file processing
    }

    public class UIdFileStatus
    {
        public string StatusName { get; set; }                     
        public string Status { get; set; }                         
        public DateTime? StatusStartTime { get; set; }
        public DateTime? StatusCompletionTime { get; set; }
        public int ProcessStatusId { get; set; }
        public int DurationInSeconds { get; set; }
        public string ErrorMessage { get; set; }
        public string StatusMessage { get; set; }


    }
}
