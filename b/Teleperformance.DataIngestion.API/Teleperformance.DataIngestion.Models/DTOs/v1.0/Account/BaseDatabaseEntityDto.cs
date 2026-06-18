using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.Account
{
    public abstract class BaseDatabaseEntityDto
    {
        [JsonIgnore]
        public string Result { get; set; }
        [JsonIgnore]
        public string Message { get; set; }
    }
}
