using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration
{
    public class SecurityGroup:DatabaseResponse
    {
        public string securityGroupId { get; set; }
        public string securityGroupName { get; set; }
        public  bool userSelectedGroup { get; set; }
        public int regionId { get; set; }
        public string? subRegionId { get; set; }
        public int clientId { get; set; }        
    }

    
    public class SecurityGroupRegion
    {
        public int regionId { get; set; }
        public string? subRegionId { get; set; }
        public int clientId { get; set; }
    }
    public class DSConfig
    {
        public string consumerApplicationId { get; set; }
        public string sourceDataObjId { get; set; }
    }

    public class SGPageAccess
    {
        
        public string component { get; set; }
    }
}
