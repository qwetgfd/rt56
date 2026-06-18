using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus
{
    public class FlpUploadedFileStatusResponseDto
    {
        public int ClientId { get; set; }               // Corresponds to upl.flpConfigurationId
        public string ClientName { get; set; }           // Not directly found in the query, can be added as needed        
        public List<FileConfigurationStatusDto> FileConfigurationStatusList { get; set; }      // List of uploaded files
    }
    public class FileConfigurationStatusDto
    {
        public string FlpConfigurationID { get; set; }               // Corresponds to upl.flpConfigurationId
        public string FlpConfigurationName { get; set; }           // Not directly found in the query, can be added as needed        
        public List<UploadedFileStatus> UploadedFiles { get; set; }
    }
    public class UploadedFileStatus
    {                         
        public string uploadFileId { get; set; }
        public string UploadFileName { get; set; }
        public string DatabaseName { get; set; }
        public string TableName { get; set; }
        public DateTime FileCreationDate { get; set; }
        public int FileProcessStatusId { get; set; }
        public int TotalRecords { get; set; }
        public int ProcessedRecords { get; set; }
        public int DuplicateRecords { get; set; }
        public string FileProcessstatusName { get;set; }
        public DateTime CompletionTime { get; set; }
        public int DurationInSeconds { get; set; }
        public string Description { get; set; }
        public string TabName { get; set; }
        public int fileProcessingServerTypeId { get; set; }
    }

   
}
