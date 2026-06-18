using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v2._0.EmailNotification;
using Teleperformance.DataIngestion.Models.Entities.v4._0.DataBricksAPIJobStatus;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface ICache
    {
        Task<IEnumerable<DateTimeFormats>?> GetFormatListAsync();

        Task<APIResponse<string>> ClearCache();
        Task<EmailNotificationTemplate?> GetEmailTemplateListAsync();
        Task<IEnumerable<DataBricksStages>?> GetDataBricksStagesAsync();
        Task<IEnumerable<DatabricksTerminationDetails>?> GetDatabricksTerminationDetailsAsync();
    }
}
