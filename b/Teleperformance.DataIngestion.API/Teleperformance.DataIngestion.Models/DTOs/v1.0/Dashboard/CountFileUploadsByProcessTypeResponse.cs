using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard
{
    public class CountFileUploadsByProcessTypeResponse
    {
        public int totalUploadedFiles { get; set; }
        public int processTypeId { get; set; }
        public string processType { get; set; }
        
    }
}
