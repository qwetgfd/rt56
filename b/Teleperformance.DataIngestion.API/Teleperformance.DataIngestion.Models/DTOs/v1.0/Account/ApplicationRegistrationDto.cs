using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.Account
{
    public class ApplicationRegistrationDto
    {
        public string ApplicationId { get; set; }
        public string ApplicationName { get; set; }
        public string ApplicationDescription { get; set; }
        public string ApplicationOwnerName { get; set; }
        public string ApplicationOwnerEmail { get; set; }
        public string IncidentNumber { get; set; }
        public bool Active { get; set; }
        public string ApplicationPassword { get; set; }
        public byte[] Password { get; set; }
        public byte[] Salt { get; set; }
        public string EncryptionKey { get; set; }
    }
}
