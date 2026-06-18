using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v4._0.DataBricksAPIJobStatus;
using Teleperformance.DataIngestion.Models.Entities.v4._0.DataBricksAPIJobStatus;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0
{
    public interface IDatabricksAPIDbRepository
    {
        Task<IEnumerable<JobRunIdDetails>> GetRunIdDetails();
        Task<int> UpdateLogHistoryStatus(FlpProcessLogHistoryStatusDto flpProcessLogHistoryStatus);
        Task<DatabriksAPIServerDetails> GetDataBricksAPIServerDetails(string flpConfigurationId);
        Task<IEnumerable<DataBricksStages>?> GetDatabricksStages();
        Task<IEnumerable<DatabricksTerminationDetails>?> GetDatabricksTerminationDetailsAsync();
        Task<IEnumerable<FlpTabStatus>> GetCurrentTabStatus(string flpConfigurationId, string uploadedFileId);
        Task<TempFileConfiguration> GetTempFileDetails(string flpConfigurationId, string uploadedFileId);
        Task<IEnumerable<JobRunIdDetailsV2>> GetJobRunDetailsForUpdateStatus();
    }
}
