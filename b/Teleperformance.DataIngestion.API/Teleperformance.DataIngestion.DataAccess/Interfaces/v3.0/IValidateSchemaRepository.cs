using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0
{
    public interface IValidateSchemaRepository
    {
        Task<IEnumerable<FlpFileColumnMapping>> GetFileColumnList(string flpConfigurationId);
        Task<DatabaseResponse> AddNewColumnMapping(string flpConfigurationId, string processName, string tableName, string fileColumn, string dbColumn,string dataType, int dataTypeId, string tabName);
        Task<IEnumerable<FlpFileColumnMapping>> GetFileColumnListByTabName(string flpConfigurationId, string tabName);
    }
}
