using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1
{
    public class AddCampaignUserByClientGeoMapping
    {
        [Required(ErrorMessage = "RegionId is mandatory.")]
        public int? RegionId { get; set; }

        [Required(ErrorMessage = "SubRegionId is mandatory.")]
        public string? SubRegionId { get; set; }

        [Required(ErrorMessage = "ClientId is mandatory.")]
        public int? ClientId { get; set; }

        [Required(ErrorMessage = "Upn is mandatory.")]
        public string? Upn { get; set; }

       
    }


    public class AddCampaignSuperAdmin
    {
       
        [Required(ErrorMessage = "Upn is mandatory.")]
        public string? Upn { get; set; }


    }
}
