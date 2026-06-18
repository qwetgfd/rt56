using Dapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v3._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v4._0
{
    public class ProcessConfigRepositoryV4 : IProcessConfigRepositoryV4
    {
        private readonly ILogger<ProcessConfigRepositoryV4> logger;
        private readonly IDapperService dapperService;

        public ProcessConfigRepositoryV4(ILogger<ProcessConfigRepositoryV4> logger, IDapperService dapperService)
        {
            this.logger = logger;
            this.dapperService = dapperService;
        }

        public async Task<DatabaseResponse> AddSecurityGroup(SecurityGroup securityGroup)
        {        
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@securityGroupId", securityGroup.securityGroupId);
                dynamicParameters.Add("@securityGroupName", securityGroup.securityGroupName);

                var dbResponse = await dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_SecurityGroup]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }
    }
}
