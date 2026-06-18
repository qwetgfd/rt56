using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface ISMBLibraryRepository
    {
        Task<DatabaseResponse> AddSmbRequestLogMessage(string flpConfigurationId, string message, string info);
    }
}
