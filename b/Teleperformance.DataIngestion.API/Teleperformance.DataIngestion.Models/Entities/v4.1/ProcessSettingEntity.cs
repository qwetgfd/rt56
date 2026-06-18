using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.Models.Entities.v4._1
{
    public class ProcessSettingEntity
    {
        public string flpConfigurationId { get; set; }
        public string process_name { get; set; }
        public string description { get; set; }
        public bool is_active { get; set; }
        public string regionId { get; set; }
        public string subRegionId { get; set; }
        public string clientId { get; set; }
        public string sender_communication_email { get; set; }
        public string support_communication_email { get; set; }
        public string region { get; set; }
        public string subRegion { get; set; }
        public string clientName { get; set; }
        public int dataSource { get; set; }
        public string securityGroupId { get; set; }
        public SecurityGroup[] securityGroups { get; set; }
        public string userName { get; set; }
        public string created_by { get; set; }
        public DateTime? created_date { get; set; }
        public string loginid { get; set; }
        public string search_string_in_file_name { get; set; }
        public string process_group_name { get; set; }
        public string billing_client_name { get; set; }
        public string sourcePath { get; set; }
        public string destinationPath { get; set; }
        public bool multisheet { get; set; }
        public bool sheetReferenceByIndex { get; set; }
        public string internalCampaignId { get; set; }
    }
}
