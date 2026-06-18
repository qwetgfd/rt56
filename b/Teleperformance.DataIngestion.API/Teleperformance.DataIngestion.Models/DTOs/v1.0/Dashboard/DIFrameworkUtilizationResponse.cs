using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard
{
    public class DIFrameworkUtilizationResponse
    {
        public int totalFileCount { get; set; }
        public int month { get; set; }
        public int clientId { get; set; }
        public string clientName { get; set; }
    }
}
