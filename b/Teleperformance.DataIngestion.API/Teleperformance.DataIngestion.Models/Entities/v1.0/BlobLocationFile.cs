using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0
{
    public class BlobLocationFile
    {
        public string Uri { get; set; }
        public string AccountName { get; set; }
        public string BlobContainerName { get; set; }
        public string Name { get; set; }
        public bool CanGenerateSasUri { get; set; }
    }


    public class BlobLocationCsvFile
    {
        public string CsvUri { get; set; }
        public string CsvAccountName { get; set; }
        public string CsvBlobContainerName { get; set; }
        public string CsvName { get; set; }
        public bool CsvCanGenerateSasUri { get; set; }
    }
}
