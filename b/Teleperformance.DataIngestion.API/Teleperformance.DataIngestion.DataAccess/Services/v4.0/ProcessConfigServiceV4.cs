using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.DataAccess.Services.v4._0
{
    public class ProcessConfigServiceV4 : IProcessConfigServiceV4
    {
        private readonly IProcessConfigRepositoryV4 processConfigRepository;
        private readonly ILogger<ProcessConfigServiceV4> logger;
        private readonly IHeaderService headerService;

        public ProcessConfigServiceV4(IProcessConfigRepositoryV4 processConfigRepository, ILogger<ProcessConfigServiceV4> logger, IHeaderService headerService)
        {
            this.processConfigRepository = processConfigRepository;
            this.logger = logger;
            this.headerService = headerService;
        }

        public async Task<APIResponse<DatabaseResponse>> AddSecurityGroups(SecurityGroup securityGroup)
        {
            string errorMessage = string.Empty;

            if (string.IsNullOrEmpty(securityGroup.securityGroupId))
            {
                errorMessage = "Security Group Id is required";
                logger.LogInformation(errorMessage);
                return await Task.FromResult(new APIResponse<DatabaseResponse>
                {
                    ResultStatus = APIResultStatus.NoContent,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = null
                });
            }

            if (string.IsNullOrEmpty(securityGroup.securityGroupName))
            {
                errorMessage = "Security Group Name Id is required";
                logger.LogInformation(errorMessage);
                return await Task.FromResult(new APIResponse<DatabaseResponse>
                {
                    ResultStatus = APIResultStatus.NoContent,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = null
                });
            }

            var response = await processConfigRepository.AddSecurityGroup(securityGroup);

            return await Task.FromResult(new APIResponse<DatabaseResponse>
            {

                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { errorMessage },
                Result = response
            });
        }
    }
}
