using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v4._1.LandingLayer
{
    public class FileExtensions
    {
        public int id { get; set; }
        public string fileExtension { get; set; }
    }

    public class Prefixes
    {
        public int id { get; set; }
        public string prefixName { get; set; }
    }

    public class LandingLayerUploadConfiguration
    {
        public int noOfAllowedFilesToUpload { get; set; }
        public int totalFileSize { get; set; }
    }

    public class LandingLayerRegex
    {
        public string regex { get; set; }
        public string description { get; set; }
    }
}
