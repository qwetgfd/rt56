using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration
{
    public class SecurityGroupMapping
    {
        public string? securityGroupId { get; set; }
        public string? subRegionId { get; set; }
        public int? clientId { get; set; }
        public int? regionId { get; set; }

    }
}
