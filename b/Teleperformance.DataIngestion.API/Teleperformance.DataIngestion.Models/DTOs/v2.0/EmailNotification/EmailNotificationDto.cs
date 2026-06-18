using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v2._0.EmailNotification
{
    public class EmailNotificationDto
    {

        public string? FlpConfigurationId { get; set; }
        public string? UploadFileId { get; set; }
        public int? FlpProcessStatusId { get; set; }
        public string? FileName { get; set; }
        public string? Error { get; set; }
        public string? TotalRecords { get; set; }
        public string? ProcessedRecords { get; set; }
        public string? DuplicateRecords { get; set; }
        public string? TotalDuration { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? Region { get; set; }
        public string? SubRegion { get; set; }
        public string? Client { get; set; }
        public string? SendToEmail { get; set; }
        public string? SourceStorageAccount { get; set; }
        public string? sourceContainerName { get; set; }
        public string? SourceStorageAccountKey { get; set; }
        public string? BlobName { get; set; }
        public string? Status { get; set; }
        public string? Stage { get; set; }
        public string? BackUpFileDetailsId { get; set; }
        public string? Description { get; set; }
        public string? TabName { get; set; }
        public bool? Multisheet { get; set; }
        public bool? SucessProcess { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int TotalFileCount { get; set; }
        public int FileProcessingServerTypeId { get; set; }
        public string ConfigurationName { get; set; }


    }
}
