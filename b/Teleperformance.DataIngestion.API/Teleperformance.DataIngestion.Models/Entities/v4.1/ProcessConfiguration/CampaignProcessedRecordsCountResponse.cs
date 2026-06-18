using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v4._1.ProcessConfiguration
{
    public class CampaignProcessedRecordsCountResponse
    {
        public string CampaignId { get; set; } = string.Empty;
        public int ValidRecordsCount { get; set; }
        public int InvalidRecordsCount { get; set; }

        [JsonIgnore]
        public int TotalRecordsCount { get; set; }
    }


    public class CampaignProcessedRecordsCountResponseV2
    {
        public string CampaignId { get; set; } = string.Empty;
        public int ValidRecordsCount { get; set; }
        public int InvalidRecordsCount { get; set; }

        [JsonIgnore]
        public int TotalRecordsCount { get; set; }

        /// <summary>
        /// Dynamic list of invalid records grouped by rule type
        /// </summary>
         public List<Dictionary<string, int>> InvalidRecordsRuleWise { get; set; } = new List<Dictionary<string, int>>();

    }




    /// <summary>
    /// DTO for rule-wise invalid records count
    /// </summary>
    public class RuleWiseInvalidRecords
    {
        public string RuleTypeName { get; set; } = string.Empty;
        //public string RuleTypeDescription { get; set; } = string.Empty;
        public int TotalFailedRowsCount { get; set; }
    }

}
