using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0
{
    public interface IChangeProcessStatusService
    {
        Task<APIResponse<DatabaseResponse>> UpdateProcessStatus();
    }
}
