using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Models.v4._1
{
    public class LandingLayerValidationFile
    {
        public string  GeneratedFileName { get; set; }
        public string ErrorMessage { get; set; }
        public IFormFile File { get; set; }

        // Add these two for blob flow
      //  public string OriginalFileName { get; set; }
        public BlobClient BlobClient { get; set; }


        //For SMB flow
        public string RemoteFilePath { get; set; }


    }
}
