using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.UploadFileStatus
{
    public class ConfigFileStatusResponse : DatabaseResponse
    {
        public int ClientId { get; set; }               // Corresponds to upl.flpConfigurationId
        public string ClientName { get; set; }
        public string ConfigurationId { get; set; }
        public string ConfigurationName { get; set; }
        public string DatabaseName { get; set; }
        public string TableName { get; set; }
        public string uploadFileId { get; set; }
        public string UploadFileName { get; set; }
        public DateTime FileCreationDate { get; set; }
        public int FileProcessStatusId { get; set; }
        public int TotalRecords { get; set; }
        public int ProcessedRecords { get; set; }
        public int DuplicateRecords { get; set; }
        public string statusName { get; set; }
        public DateTime CompletionTime { get; set; }
        public int DurationInSeconds { get; set; }
        public string Description { get; set; }
        public string TabName { get; set; }
        public string blobName { get; set; }
        public int fileProcessingServerTypeId { get; set; }
    }
}
