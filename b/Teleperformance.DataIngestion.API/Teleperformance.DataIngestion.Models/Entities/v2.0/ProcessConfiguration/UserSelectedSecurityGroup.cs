
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration
{
    public class UserSelectedSecurityGroup:DatabaseResponse
    {
        public string securityGroupId { get; set; }
        public string loginId { get; set; }
        public string userName { get; set; }
    }
}
