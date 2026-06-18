using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration
{
    public class FlpProcessConfigurationListResponse
    {
        public int Id { get; set; }
        public string ConfigurationId { get; set; }
        public string ProcessName { get; set; }
        public string SenderEmail { get; set; }
        public string CreatedBy { get; set; }
        public string LoginId { get; set; }
        public string ProcessTypeId { get; set; }
        public string RegionId { get; set; }
        public string SubRegionId { get; set; }
        public string CreatedDate { get; set; }
        public string SubRegionName { get; set; }
        public string RegionName { get; set; }
        public string ProcessTypeName { get; set; }
        public string ClientName { get; set; }
        public string DatabaseName { get; set; }
        public string TableName { get; set; }
        public string UpdatedBy { get; set; }
        public int IngestionTypeId { get; set; }
        public string IngestionType { get; set; }
        public string updatedOn { get; set; }

        public string Description { get; set; }

        public bool MergeData { get; set; }

        public bool CreateHistoryTable { get; set; }
        public string TabName { get; set; }
        public bool MultiSheet { get; set; }
        public int RowNo { get; set; }
        


    }

    public class FlpProcessConfigurationResponse
    {
        public List<FlpProcessConfigurationListResponse> Response { get; set; }
        public int TotalCount { get; set; }
    }
}
