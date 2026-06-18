using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess
{
    public class FlpBaseConfigurationResponseDto
    {
        public string FlpConfigurationId { get; set; }
        public int LocationTypeId { get; set; }
        public int? DestinationLocationTypeId { get; set; }
        public string ProcessName { get; set; }
        [JsonIgnore]
        public string SenderCommunicationEmail { get; set; }
        [JsonIgnore]
        public string LoginId { get; set; }
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public string SupportCommunicationEmail { get; set; }      
        public string SearchStringInFileName { get; set; }
        public string ProcessGroupName { get; set; }
        public string BillingClientName { get; set; }
        public string Description { get; set; }
        public int ProcessTypeId { get; set; }
    }
}
