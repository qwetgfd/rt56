using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.Account
{
    public class ApplicationDto
    {
        public string ApplicationId { get; set; }
        public string Token { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string RefreshToken { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime ExpiresUtc { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public IEnumerable<SecurityGroupDto>? securityGroups { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public IEnumerable<SGPageAccess>? pageAccess { get; set; }
    }
}
