using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.Account
{
    public class ApplicationKey
    {
        public int Id { get; set; }
        public string ApplicationId { get; set; }
        public byte[] ApplicationPassword { get; set; }
        public byte[] ApplicationSalt { get; set; }
        public string EncryptionKey { get; set; }
        public  bool ShowSecurityGroup { get; set; }
    }
}
