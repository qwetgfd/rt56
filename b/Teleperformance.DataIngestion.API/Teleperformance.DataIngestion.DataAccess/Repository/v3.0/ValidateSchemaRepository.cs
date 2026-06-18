using Dapper;
using Microsoft.Extensions.Logging;
using System.Data;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v3._0
{
    public class ValidateSchemaRepository:IValidateSchemaRepository
    {
        private readonly ILogger<ValidateSchemaRepository> _logger;
        private readonly IDapperService _dapperService;

        public ValidateSchemaRepository(ILogger<ValidateSchemaRepository> logger, IDapperService dapperService)
        {
            _logger = logger;
            _dapperService = dapperService;
        }


        public async Task<IEnumerable<FlpFileColumnMapping>> GetFileColumnList(string flpConfigurationId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);

                var dbResponse = await _dapperService.GetMultipleRowsAsync<FlpFileColumnMapping>("[sel_flpFileColumnMapping]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<IEnumerable<FlpFileColumnMapping>> GetFileColumnListByTabName(string flpConfigurationId,string tabName)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@tabName", tabName);

                var dbResponse = await _dapperService.GetMultipleRowsAsync<FlpFileColumnMapping>("[sel_flpFileColumnMapping]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }


        public async Task<DatabaseResponse> AddNewColumnMapping(string flpConfigurationId,string processName,string tableName,string fileColumn,string dbColumn,string dataType,int dataTypeId, string tabName)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@processName", processName);
                dynamicParameters.Add("@tableName", tableName);
                dynamicParameters.Add("@fileColumn", fileColumn);
                dynamicParameters.Add("@dbColumn", dbColumn);
                dynamicParameters.Add("@dataTypeId", dataTypeId);
                //dynamicParameters.Add("@formatId", flpConfigurationId);
                dynamicParameters.Add("@dataType", dataType);
                if (!string.IsNullOrWhiteSpace(tabName))
                  dynamicParameters.Add("@tabName", tabName);

                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_newColumnMapping]", dynamicParameters, commandType: CommandType.StoredProcedure);
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
