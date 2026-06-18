using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IDapperService
    {
        Task<T> GetSingleRowAsync<T>(string storedProcedureName, DynamicParameters dynamicParameters, CommandType commandType = CommandType.StoredProcedure);
        Task<IEnumerable<T>> GetMultipleRowsAsync<T>(string storedProcedureName, DynamicParameters dynamicParameters, CommandType commandType = CommandType.StoredProcedure);
        Task<int> InsertDataAsync<T>(string storedProcedureName, DynamicParameters dynamicParameters, CommandType commandType = CommandType.StoredProcedure);
        Task<T> GetSingleRowAsync<T>(string connectionName, string storedProcedureName, DynamicParameters dynamicParameters, CommandType commandType = CommandType.StoredProcedure);        
        IDbConnection CreateConnection();
    }
}
