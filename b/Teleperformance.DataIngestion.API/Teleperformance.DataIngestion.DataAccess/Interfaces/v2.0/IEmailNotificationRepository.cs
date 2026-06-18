using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v2._0.EmailNotification;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0
{
    public interface IEmailNotificationRepository
    {

//Task<DatabaseResponse?> CommitEmailNotification(string flpConfigurationId, string uploadFileId);
        Task<IEnumerable<EmailNotification>> GetEmailNotificationList();
        Task<EmailNotificationTemplate> GetEmailNotificationTemplate();
        Task<EmailNotification> GetEmailNotificationDetailByIds(string flpConfigurationId, string uploadFileId);
        Task<IEnumerable<EmailNotification>> GetMultisheetEmailNotifications(string flpConfigurationId, string uploadFileId);
        Task<DatabaseResponse> CommitEmailNotification(string flpConfigurationId, string uploadFileId, bool isEmailSent, string tabName);
    }
}
