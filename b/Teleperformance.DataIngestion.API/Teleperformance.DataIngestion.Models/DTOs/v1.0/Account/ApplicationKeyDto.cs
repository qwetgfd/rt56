using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.Account
{
    public class ApplicationKeyDto : BaseDatabaseEntityDto
    {
        public int Id { get; set; }
        public string ApplicationId { get; set; }
        public byte[] ApplicationPassword { get; set; }
        public byte[] ApplicationSalt { get; set; }
        public string EncryptionKey { get; set; }
        public bool ShowSecurityGroup { get; set; }
        //public string Result { get; set; }
        //public string Message { get; set; }
    }
}
