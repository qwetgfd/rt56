using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v2._0.ProcessConfiguration
{
    public class SecurityGroupDto
    {
        public string SecurityGroupId { get; set; }
        public string SecurityGroupName { get; set; }
        public bool UserSelectedGroup { get; set; }
        public int regionId { get; set; }
        public string? subRegionId { get; set; }
        public int clientId { get; set; }
    }

    public class SecurityGroupRegionDto
    {
        public int regionId { get; set; }
        public string? subRegionId { get; set; }
        public int clientId { get; set; }
    }

    public class SGPageAccessDto
    {
        public string component { get; set; }
    }
}
