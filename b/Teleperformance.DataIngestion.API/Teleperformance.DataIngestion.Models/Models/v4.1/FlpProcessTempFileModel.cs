using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.Models.Models.v4._1
{
    public class FlpProcessTempFileModel : BlobLocationFile
    {
        public string ParquetBlobConnectionString { get; set; }
        public string DestinationFolder { get; set; }
        public string sourceTempFilePath { get; set; }
        public string fileUrl { get; set; }
        public string ErrorMessage { get; set; }

        public string CsvParquetBlobConnectionString { get; set; }
        public string CsvDestinationFolder { get; set; }
        public string csvSourceTempFilePath { get; set; }
        public string CsvFileUrl { get; set; }
        public BlobLocationCsvFileV4_1 CsvFile { get; set; }
        public double FileSize { get; set; } // Size in bytes
    }


    public class BlobLocationCsvFileV4_1
    {
        public string CsvUri { get; set; }
        public string CsvAccountName { get; set; }
        public string CsvBlobContainerName { get; set; }
        public string CsvName { get; set; }
        public bool CsvCanGenerateSasUri { get; set; }
    }
}
