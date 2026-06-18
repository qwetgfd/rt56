using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v4._1
{
    public class FlpValidationDetails
    {
        public FileAdditionalDetails? FileAdditionalDetails { get; set; }
        public List<FlpRegex>? RegexList { get; set; }
        public List<FlpExtensions>? ExtensionList { get; set; }
    }

    public class FileAdditionalDetails
    {
        public string flpConfigurationId { get; set; }
        public string securityGroupId { get; set; }
        public string prefix { get; set; }
        public string dateFormat { get; set; }
        public string timeFormat { get; set; }
        public string landingLayerPath { get; set; }
        public string rejectedLayerPath { get; set; }
    }


    public class FlpRegex
    {
        public string regex { get; set; }
    }

    public class FlpExtensions
    {
        public string fileExtension { get; set; }
    }
}
