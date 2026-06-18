using Dapper;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v4._0.DataBricksAPIJobStatus;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v4._0.DataBricksAPIJobStatus;
using static Dapper.SqlMapper;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v4._0
{
    public class DatabricksAPIDbRepository: IDatabricksAPIDbRepository
    {
        private readonly ILogger<DatabricksAPIDbRepository> logger;
        private readonly IDapperService dapperService;

        public DatabricksAPIDbRepository(ILogger<DatabricksAPIDbRepository> logger, IDapperService dapperService)
        {
            this.logger = logger;
            this.dapperService = dapperService;
        }


        public async Task<IEnumerable<JobRunIdDetails>> GetRunIdDetails()
        {
            try
            {
                var dbResponse = await dapperService.GetMultipleRowsAsync<JobRunIdDetails>("[sel_runIdDetailsByUploadFile]", null, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<IEnumerable<JobRunIdDetailsV2>> GetJobRunDetailsForUpdateStatus()
        {
            try
            {
                var dbResponse = await dapperService.GetMultipleRowsAsync<JobRunIdDetailsV2>("[sel_jobRunDetailsForUpdateStatus]", null, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<DatabriksAPIServerDetails> GetDataBricksAPIServerDetails(string flpConfigurationId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                var dbResponse = await dapperService.GetSingleRowAsync<DatabriksAPIServerDetails>("[sel_databricksAPIServerDetails]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }


        public async Task<IEnumerable<DatabricksTerminationDetails>?> GetDatabricksTerminationDetailsAsync()
        {
            var dbResponse = await dapperService.GetMultipleRowsAsync<DatabricksTerminationDetails>("[sel_databricksTerminationDetails]", null, commandType: CommandType.StoredProcedure);
            return dbResponse;
        }

        public async Task<IEnumerable<DataBricksStages>?> GetDatabricksStages()
        {
            var dbResponse = await dapperService.GetMultipleRowsAsync<DataBricksStages>("[sel_databricksStages]", null, commandType: CommandType.StoredProcedure);
            return dbResponse;
        }

        public async Task<int> UpdateLogHistoryStatus(FlpProcessLogHistoryStatusDto flpProcessLogHistoryStatus)
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
                if(!string.IsNullOrEmpty(flpProcessLogHistoryStatus.tabName))
                {
                    dynamicParameters.Add("@tabName", flpProcessLogHistoryStatus.tabName);
                }
               
                var dbResponse = await dapperService.InsertDataAsync<int>("[commit_flpProcessLogHistoryStatus]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                logger.LogError(ex, ex.Message);
                throw;
            }
        }


        public async Task<IEnumerable<FlpTabStatus>> GetCurrentTabStatus(string flpConfigurationId, string uploadedFileId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@uploadFileId", uploadedFileId);
                var dbResponse = await dapperService.GetMultipleRowsAsync<FlpTabStatus>("[sel_flpCurrentTabStatus]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<TempFileConfiguration> GetTempFileDetails(string flpConfigurationId,string uploadedFileId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@uploadFileId", uploadedFileId);
                var dbResponse = await dapperService.GetSingleRowAsync<TempFileConfiguration>("[sel_tempFileConfiguration]", dynamicParameters, commandType: CommandType.StoredProcedure);
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
