using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v2._0.EmailNotification
{
    public class EmailNotificationDetails
    {
        public string MailBody { get; set; }
        public string SendToEmail { get; set; }
        public string FromAddress { get; set; }
        public string SuccessEmailTemplate { get; set; }
        public string FailureEmailTemplate { get; set; }
        public string SuccessSubject { get; set; }
        public string FailureSubject { get; set; }
        public bool SuccessEmailToBeSent { get; set; }
        public bool FailureEmailToBeSent { get; set; }

        
    }
}
