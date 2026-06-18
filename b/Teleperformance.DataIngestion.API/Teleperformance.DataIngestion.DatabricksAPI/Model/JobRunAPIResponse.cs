using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.DatabricksAPI.Model
{
    public class JobRunAPIResponse
    {
        public bool JobRunSuccess { get; set; }
        public object StatusCode { get; set; }
        public long? RunId { get; set; }
        public string ResponseContent { get; set; }
        public string Message { get; set; }
        public string? JsonContent { get; set; } 
    }


}
