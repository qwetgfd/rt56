using Azure.Storage.Blobs;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Enums.v1._0;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess
{
    public class ParquetFileResponseDto
    {
        public string ParquetFilePath { get; set; }

        public bool ParquetFileCreated { get; set; }
        public string ErrorMessage { get; set; }
        public int TotalRows { get; set; }
        public int DuplicateRows { get; set; }
        public int InsertedRows { get; set; }
        public BlobClient ParquetBlobClient { get; set; }
        public BlobClient ParquetBlobClientTemp { get; set; }
        public Dictionary<string,DataTypeDetails> ColumnDataTypeList { get; set; }
        public FlpActivityLogStatusEnum flpActivityLogStatusEnum { get; set; }

    }
}
