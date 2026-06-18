using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess
{
    public class FlpProcessTempFile : BlobLocationFile
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
        public BlobLocationCsvFile CsvFile { get; set; }

        public double FileSize { get; set; } // Size in bytes

        public bool SasToken    { get; set; }
    }
}
