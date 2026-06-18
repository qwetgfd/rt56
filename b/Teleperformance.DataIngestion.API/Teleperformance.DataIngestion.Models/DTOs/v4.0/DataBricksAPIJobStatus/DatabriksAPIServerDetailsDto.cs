using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._0.DataBricksAPIJobStatus
{
    public class DatabriksAPIServerDetailsDto
    {
        public string FlpConfigurationId { get; set; }
        public string DatabricksAPIToken { get; set; }
        public string DatabricksInstance { get; set; }
        public string DatabricksAPIVersion { get; set; }
    }
}
