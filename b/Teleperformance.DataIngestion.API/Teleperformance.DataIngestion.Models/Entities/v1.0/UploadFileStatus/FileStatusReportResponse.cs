using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.UploadFileStatus
{
    public class FileStatusReportResponse : DatabaseResponse
    {
        public string ConfigurationId { get; set; }  // Optional, add it if necessary
        public string uploadFileId { get; set; }
        public string UploadFileName { get; set; }
        public DateTime StatusStartTime { get; set; }
        public DateTime StatusCompletionTime { get; set; }
        public int FileProcessstatusId { get; set; }
        public string statusName { get; set; }
        public string LogStatusName { get; set; }
        public string ProcessStatusName { get; set; }
        public int DurationInSeconds { get; set; }
        public int ProcessStatusId { get; set; }
        public string DatabaseName { get; set; }
        public string TableName { get; set; }
        public int TotalRecords { get; set; }
        public int ProcessedRecords { get; set; }
        public int DuplicateRecords { get; set; }
        public string ErrorMessage { get; set; }
        public string statusMessage { get; set; }

        public string Description { get; set; }
        public string TabName { get; set; }
        public string blobName { get; set; }
        public string? statusCode { get; set; }

    }
}
