using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Services.v1._0
{
    public class DapperService : IDapperService
    {
        private readonly IConfiguration _configuration;
        private string _connectionString;

        public DapperService(IConfiguration configuration)
        {
            _configuration = configuration;
           // _connectionString = KeyVault.GetKeyVaultValue("TPDataIngestionConnectionString").Result;

            // Local environment to be deleted later -AY — use local DB before KeyVault so middleware/DI does not fail offline
            var localConnectionString = Environment.GetEnvironmentVariable("connectionstring");
            if (!string.IsNullOrWhiteSpace(localConnectionString))
            {
                _connectionString = localConnectionString;
            }
            else
            {
                _connectionString = KeyVault.GetKeyVaultValue("TPDataIngestionV2ConnectionString").Result;
            }

        }

        public void Dispose()
        {
        }

        public async Task<T> GetSingleRowAsync<T>(string storedProcedureName, DynamicParameters dynamicParameters, CommandType commandType = CommandType.StoredProcedure)
        {
            using (IDbConnection dbconnection = new SqlConnection(_connectionString))
            {
                return await dbconnection.QueryFirstOrDefaultAsync<T>(storedProcedureName, dynamicParameters, commandType: commandType);
            }
        }
        public async Task<T> GetSingleRowAsync<T>(string storedProcedureName, DynamicParameters dynamicParameters, string keyVaultName, CommandType commandType = CommandType.StoredProcedure)
        {
            using (IDbConnection dbconnection = new SqlConnection(KeyVault.GetKeyVaultValue(keyVaultName).Result))
            {
                return await dbconnection.QueryFirstOrDefaultAsync<T>(storedProcedureName, dynamicParameters, commandType: commandType);
            }
        }
        public async Task<IEnumerable<T>> GetMultipleRowsAsync<T>(string storedProcedureName, DynamicParameters dynamicParameters, CommandType commandType = CommandType.StoredProcedure)
        {
            using (IDbConnection dbconnection = new SqlConnection(_connectionString))
            {
                var results = await dbconnection.QueryAsync<T>(storedProcedureName, dynamicParameters, commandType: commandType);
                return results == null ? null : results.ToList();
            }
        }

        public async Task<IEnumerable<T>> GetMultipleRowsAsync<T>(string storedProcedureName, DynamicParameters dynamicParameters, string keyVaultName, CommandType commandType = CommandType.StoredProcedure)
        {
            using (IDbConnection dbconnection = new SqlConnection(KeyVault.GetKeyVaultValue(keyVaultName).Result))
            {
                var results = await dbconnection.QueryAsync<T>(storedProcedureName, dynamicParameters, commandType: commandType);
                return results == null ? null : results.ToList();
            }
        }

        public async Task<int> InsertDataAsync<T>(string storedProcedureName, DynamicParameters dynamicParameters, CommandType commandType = CommandType.StoredProcedure)
        {
            using (IDbConnection dbconnection = new SqlConnection(_connectionString))
            {
                return await dbconnection.ExecuteAsync(storedProcedureName, dynamicParameters, commandType: commandType);
            }
        }

        public async Task<T> GetSingleRowAsync<T>(string connectionName, string storedProcedureName, DynamicParameters dynamicParameters, CommandType commandType = CommandType.StoredProcedure)
        {
            using (IDbConnection dbconnection = new SqlConnection(connectionName))
            {
                return await dbconnection.QueryFirstOrDefaultAsync<T>(storedProcedureName, dynamicParameters, commandType: commandType);
            }
        }
        public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
    }
}
