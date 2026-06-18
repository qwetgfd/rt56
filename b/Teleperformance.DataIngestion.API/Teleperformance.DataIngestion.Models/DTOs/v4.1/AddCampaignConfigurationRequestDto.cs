using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1
{
    public class AddCampaignConfigurationRequestDto
    {
        [Required(ErrorMessage = "Campaign ID is mandatory.")]
        public string CampaignId { get; set; }

        [Required(ErrorMessage = "CampaignName is mandatory.")]
        public string CampaignName { get; set; }

        [Required(ErrorMessage = "RegionId is mandatory.")]
        public int RegionId { get; set; }

        [Required(ErrorMessage = "SubRegionId is mandatory.")]
        public string SubRegionId { get; set; }

        [Required(ErrorMessage = "ClientId is mandatory.")]
        public int ClientId { get; set; }

        //[Required(ErrorMessage = "AddedBy is mandatory.")]
        //public string AddedBy { get; set; }

        [Required(ErrorMessage = "Upn is mandatory.")]
        public string Upn { get; set; }
    }
}
