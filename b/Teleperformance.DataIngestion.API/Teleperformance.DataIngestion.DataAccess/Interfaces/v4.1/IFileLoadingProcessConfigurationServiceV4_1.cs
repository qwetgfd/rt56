using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1
{
    public interface IFileLoadingProcessConfigurationServiceV4_1
    {
        Task<APIResponse<FlpConfigurationResponseDtoV4_1>> GetMultisheetConfiguration(string flpConfigurationId, string uploadedFileId, string tabName);
        Task<bool> UpdateFlpProcessStatus(string fileUploadedId, APIResultStatus apiResultStatus);
        Task<DestinationStorageAccountDtoV4_1?> DestinationstorageAccountInfo(string flpConfigurationId);
        Task<SharedLocationDestinationServerDtoV4_1?> SharedLocationDestinationServerDetails(string flpConfigurationId);
        Task<bool> AddBackUpFileDetails(string uploadFileId, string flpConfigurationId, string? backupFileName, string? tabName, int totalRows, int insertedRows, int duplicateRows, string blobName);
        Task<DatabricksStorageAccountDto4_1?> DatabricksStorageAccountInfo(string flpConfigurationId, string tabName);
        Task<bool> UpdateProcessStatus(string fileUploadedId, int flpProcessStatusId);
        Task<APIResponse<List<RuleSetDtoV4_1>>> GetDIRuleSetByRuleSetNameId(string ruleSetNameId);


    }
}
