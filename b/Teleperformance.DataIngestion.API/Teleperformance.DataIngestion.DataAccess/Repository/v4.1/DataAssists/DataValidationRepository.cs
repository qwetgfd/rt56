using Dapper;
using DocumentFormat.OpenXml.Drawing.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using NPOI.SS.UserModel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1.DataAssists;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.EIB;
using Teleperformance.DataIngestion.Models.Entities.v4._1.DataAssists;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v4._1.DataAssists
{
    public class DataValidationRepository : IDataValidationRepositoryV4_1
    {
        private readonly ILogger<DataValidationRepository> logger;
        private readonly IDapperService dapperService;

        public DataValidationRepository(ILogger<DataValidationRepository> logger,
            IDapperService dapperService)
        {
            this.logger = logger;
            this.dapperService = dapperService;
        }

        public async Task<bool> commitDataAssistGeneratedJsonResponse(string response, int flpRuleSetId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@jsonResponse", response);
                dynamicParameters.Add("@flpRuleSetId", flpRuleSetId);

                // Add return value parameter
                dynamicParameters.Add("ReturnValue", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);

                await dapperService.InsertDataAsync<int>("[commit_tpDataAssistGeneratedJsonResponse]", dynamicParameters, commandType: CommandType.StoredProcedure);
                int dbResponse = dynamicParameters.Get<int>("ReturnValue");
                return dbResponse > 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<ProjectPrompts>> getPrompt(int flowId, int projectId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flowId", flowId);
                dynamicParameters.Add("@projectId", projectId);

                var dbResponse = await dapperService.GetMultipleRowsAsync<ProjectPrompts>("[GetProjectPromptsByProjectId]", dynamicParameters, commandType: CommandType.StoredProcedure);

                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }
    }
}
