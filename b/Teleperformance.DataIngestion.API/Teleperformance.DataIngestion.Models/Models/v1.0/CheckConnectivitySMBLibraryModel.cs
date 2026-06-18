using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Models.v1._0
{
    public class CheckConnectivitySMBLibraryModel
    {
        public string serverPort { get; set; }
        public string serverIP { get; set; }
        public string domain { get; set; }
        public string sharedFolderPath { get; set; }
        public string sharedFolderName { get; set; }
        public string fileName { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string destinationFolder { get; set; }

        public string sourceFilePath { get; set; }
        public string destinationFilePath { get; set; }
        public BlobClient blobClient { get; set; }
    }
}
