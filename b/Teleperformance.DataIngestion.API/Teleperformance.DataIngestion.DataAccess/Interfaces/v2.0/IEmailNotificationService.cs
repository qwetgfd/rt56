using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v2._0.EmailNotification;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0
{
    public interface IEmailNotificationService
    {
        Task<APIResponse<IEnumerable<EmailNotificationRequestDto>>> GetEmailNotificationList();
        Task<APIResponse<bool>> SendEmailNotification(EmailNotificationRequestDto emailNotificationDto);
    }
}
