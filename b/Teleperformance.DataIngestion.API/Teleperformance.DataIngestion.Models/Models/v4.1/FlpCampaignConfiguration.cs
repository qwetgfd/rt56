using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Models.v4._1
{
    public class FlpCampaignConfiguration
    {
        public string InternalCampaignId { get; set; }
        public string CampaignId { get; set; }
        public string CampaignName { get; set; }
        public int RegionId { get; set; }
        public string SubRegionId { get; set; }
        public int ClientId { get; set; }
        public string AddedBy { get; set; }
        public string ApplicationId { get; set; }
    }
}
