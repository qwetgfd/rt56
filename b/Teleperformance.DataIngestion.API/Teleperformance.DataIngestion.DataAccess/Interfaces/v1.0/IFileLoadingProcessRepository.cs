using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IFileLoadingProcessRepository
    {
        Task<bool> InsertFileProcessStatus(FileProcessLogHistoryDto logHistory);
       
        Task<(bool, bool)> dropMainTableAndHistoryTable(bool paramDropManinTable, bool paramDropHistoryTable, string mainTable, string historyTable, string connectionString);
        Task<DatabaseResponse> AddDatabricksJsonFileColumn(string fileURL, string columns, string flpConfigurationId, string uploadFileId, string tabName);


    }
}
