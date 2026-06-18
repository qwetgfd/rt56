using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.Models.Entities.v2._0.EmailNotification
{
    public class EmailNotificationTemplate:DatabaseResponse
    {
        public string SuccessEmailTemplate { get; set; }
        public string FailureEmailTemplate { get; set; }
        public string Subject { get; set; }
        public string FromAddress { get; set; }
        public string SuccessSubject { get; set; }
        public string FailureSubject { get; set; }
        public bool SuccessSentEmail { get; set; }
        public bool FailureSentEmail { get; set; }
    }
}
