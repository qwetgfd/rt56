using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v2._0.EmailNotification
{
    public class MultisheetEmailNotificationDto
    {
        public string FlpConfigurationId { get; set; }
        public string FileName { get; set; }
        public string UploadFileId { get; set; }
        public string ClientName { get; set; }
        public string Region { get; set; }
        public string SubRegion { get; set; }
        public string Description { get; set; }
        public string SentToEmail { get; set; }
        public List<TabNameDto> TabNames{ get; set; }
    }

    public class TabNameDto
    {
        public string TabName { get; set; }
        public EmailNotificationDto NotificationDetail { get; set; }
        public bool SuccessProcess { get; set; }
    }
}
