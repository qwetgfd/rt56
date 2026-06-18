using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;


namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0
{
    public interface IProcessConfigRepositoryV4
    {
        Task<DatabaseResponse> AddSecurityGroup(SecurityGroup securityGroup);
    }
}
