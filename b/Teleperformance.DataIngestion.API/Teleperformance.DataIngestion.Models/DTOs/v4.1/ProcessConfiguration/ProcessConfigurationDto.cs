using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.ProcessConfiguration
{
    public class CampaignUserAccessDto
    {
        public string internalCampaignId { get; set; }
        public string? campaignId { get; set; }
        public string? campaignName { get; set; }
        public int regionId { get; set; }
        public string subRegionId { get; set; }
        public int clientId { get; set; }
    }
}
