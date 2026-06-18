using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v3._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Account;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;

namespace Teleperformance.DataIngestion.DataAccess.Services.v3._0
{
    public class ChangeProcessStatusService :IChangeProcessStatusService
    {
        private readonly ILogger<ChangeProcessStatusService> _logger;
        private readonly IChangeProcessStatusRepository _changeProcessStatusRepository;
        public ChangeProcessStatusService(IChangeProcessStatusRepository changeProcessStatusRepository, ILogger<ChangeProcessStatusService> logger)
        {
            _changeProcessStatusRepository = changeProcessStatusRepository;
            _logger = logger;  
        }

        public async Task<APIResponse<DatabaseResponse>> UpdateProcessStatus()
        {
            var dbResponse = await _changeProcessStatusRepository.UpdateProcessStatus();
            if (dbResponse == null)
            {
                return await Task.FromResult(new APIResponse<DatabaseResponse>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Internal server error" },
                    Result = null
                });
            }            
            if (string.Compare(dbResponse.Result, "Error", true) == 0)
                return await Task.FromResult(new APIResponse<DatabaseResponse>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { dbResponse?.Message ?? "something went wrong" },
                    Result = null
                });
            return await Task.FromResult(new APIResponse<DatabaseResponse>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { $"Update process status" },
                Result = null
            });

        }

    }
}
