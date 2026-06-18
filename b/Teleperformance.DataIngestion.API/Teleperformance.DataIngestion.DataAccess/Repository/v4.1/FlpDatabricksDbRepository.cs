using Dapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v4._1
{
    public class FlpDatabricksDbRepository : IDatabricksDbRepository
    {
        private readonly ILogger<ProcessConfigurationRepositoryV4_1> _logger;
        private readonly IDapperService _dapperService;

        public FlpDatabricksDbRepository(ILogger<ProcessConfigurationRepositoryV4_1> logger, IDapperService dapperService)
        {
            this._logger = logger;
            this._dapperService = dapperService;
        }
        public async Task<int> UpdateLogHistoryStatus(FlpProcessLogHistoryStatusDtov4_1 flpProcessLogHistoryStatus)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@lifeCycleStateId", flpProcessLogHistoryStatus.LifeCycleStateId);
                dynamicParameters.Add("@resultStateId", flpProcessLogHistoryStatus.ResultStateId);
                dynamicParameters.Add("@logHistoryId", flpProcessLogHistoryStatus.LogHistoryId);
                dynamicParameters.Add("@flpConfigurationId", flpProcessLogHistoryStatus.FlpConfigurationId);
                dynamicParameters.Add("@flpFileLogStatusId", flpProcessLogHistoryStatus.FlpFileLogStatusId);
                dynamicParameters.Add("@activityProcessStatusId", flpProcessLogHistoryStatus.ActivityProcessStatusId);
                dynamicParameters.Add("@fileUploadedId", flpProcessLogHistoryStatus.FileUploadedId);
                dynamicParameters.Add("@flpProcessStatusId", flpProcessLogHistoryStatus.FlpProcessStatusId);
                dynamicParameters.Add("@databricksStageId", flpProcessLogHistoryStatus.DatabricksStageId);
                dynamicParameters.Add("@runId", flpProcessLogHistoryStatus.RunId);
                dynamicParameters.Add("@databricksAPIResponse", flpProcessLogHistoryStatus.DatabricksAPIResponse);
                dynamicParameters.Add("@skipUpdateHistoryStatus", flpProcessLogHistoryStatus.SkipUpdateHistoryStatus);
                dynamicParameters.Add("@message", flpProcessLogHistoryStatus.Message);
                if (!string.IsNullOrWhiteSpace(flpProcessLogHistoryStatus.tabName))
                    dynamicParameters.Add("@tabName", flpProcessLogHistoryStatus.tabName);
                var dbResponse = await _dapperService.InsertDataAsync<int>("[commit_flpProcessLogHistoryStatus]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }
    }
}
