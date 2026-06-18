using Teleperformance.DataIngestion.Models.DTOs.v4._0.DataBricksAPIJobStatus;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0
{
    public interface IDataBricksJobStatusService
    {
        Task<APIResponse<IEnumerable<JobRunIdDetailsDto>>> GetRunIdDetails();
        Task<APIResponse<string>> UpdateLogHistoryStatus(JobRunIdDetailsDto jobRunIdDetailsDto);
        Task<APIResponse<string>> DeleteTempFileFromLocation(JobRunIdDetailsDtoV2 jobRunIdDetailsDto);

        Task<APIResponse<string>> UpdateProcessStatus(JobRunIdDetailsDtoV2 jobRunIdDetailsDto);
        Task<APIResponse<IEnumerable<JobRunIdDetailsDtoV2>>> GetRunIdDetailsForUpdateStatus();
    }
}
