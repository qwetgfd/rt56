using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.ProcessConfiguration
{
    public class CampaignProcessedRecordsCountRequest
    {
        [Required(ErrorMessage = "CampaignId is mandatory")]
        public string CampaignId { get; set; } = string.Empty;

        /// <summary>
        /// Optional from date filter for di_uploadedFile.creationDateTime (format: YYYY-MM-DD or MM/DD/YYYY)
        /// </summary>
        public string? FromDate { get; set; }

        /// <summary>
        /// Optional to date filter for di_uploadedFile.creationDateTime (format: YYYY-MM-DD or MM/DD/YYYY)
        /// </summary>
        public string? ToDate { get; set; }
    }

    
}
