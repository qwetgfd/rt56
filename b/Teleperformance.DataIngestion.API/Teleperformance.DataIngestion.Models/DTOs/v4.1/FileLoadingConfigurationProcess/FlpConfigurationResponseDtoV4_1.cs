using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess
{
    public class FlpConfigurationResponseDtoV4_1 : FlpBaseConfigurationResponseDto
    {
        public string SourceStorageAccount { get; set; }
        public string SourceContainerName { get; set; }
        public string SourceStorageAccountKey { get; set; }
        public string SourceServerName { get; set; }
        public string SourceUserName { get; set; }
        public string SourcePassword { get; set; }
        public string SourceAppFolder { get; set; }
        public string SourceDomain { get; set; }
        public string UploadedFileId { get; set; }
        public string FileType { get; set; }
        public string UploadedFileName { get; set; }
        public string DetalakeStorageAccountPath { get; set; }
        public string DataBricksAPIToken { get; set; }
        public string DataBricksAPIVersion { get; set; }
        public string DatabricksInstance { get; set; }
        public bool ProcessModified { get; set; }
        public long ProcessId { get; set; }
        public string DatabaseConnectionSecret { get; set; }
        public string JobId { get; set; }
        //public BlobClient BlobClients { get; set; }
        public BlobClientDetails BlobClients { get; set; }
        public string TabName { get; set; }
        public string ParquetCompression { get; set; }
        public string HistoryTableName { get; set; }
        public string UnityCatalog { get; set; }
        public string SasKey { get; set; }
        public bool SasKeyToken { get; set; }
        public bool UIValidation { get; set; }
        public bool BEValidation { get; set; }
        public string silverTableName { get; set; }
        public string goldTableName { get; set; }
        public string? campaignId { get; set; }
    }
}
