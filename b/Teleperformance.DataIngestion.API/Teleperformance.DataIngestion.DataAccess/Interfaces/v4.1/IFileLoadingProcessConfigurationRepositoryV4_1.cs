using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.ValidateFileRules;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v4._1;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1
{
    public interface IFileLoadingProcessConfigurationRepositoryV4_1
    {
        Task<DatabaseResponse> commitFlpConfigProcessStatus(string fileUploadedId, int statusId);
        Task<DestinationStorageAccount> DestinationStorageAccountInfo(string flpConfigurationId);
        Task<SharedLocDestinationServer> GetSharedLocationDestinationServer(string flpConfigurationId);
        Task<bool> InsertFileProcessStatus(FileProcessLogHistoryDto logHistory);
      
        Task<FlpConfiguration4_1> GetMultisheetConfiguration(string flpConfigurationId,
            string uploadedFileId, string tabName);

        Task<DatabaseResponse> AddBackUpFileDetails(string uploadFileId, string flpConfigurationId, string? backupFileName, string? tabName, int totalRows, int insertedRows, int duplicateRows, string blobName);
        Task<DestinationStorageAccount> DatabricksStorageAccountInfo(string flpConfigurationId, string tabName);

        Task<List<RuleSetV4_1>> GetDIRuleSetByRuleSetNameId(string ruleSetNameId);
        Task<DatabaseResponse> AddDatabricksJsonFileColumn(string fileURL, string columns, string flpConfigurationId, string uploadFileId, string tabName);


    }
}
