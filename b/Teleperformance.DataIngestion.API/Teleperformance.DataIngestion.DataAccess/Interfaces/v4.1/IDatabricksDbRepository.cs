using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1
{
    public interface IDatabricksDbRepository
    {
        Task<int> UpdateLogHistoryStatus(FlpProcessLogHistoryStatusDtov4_1 flpProcessLogHistoryStatus);
    }
}
