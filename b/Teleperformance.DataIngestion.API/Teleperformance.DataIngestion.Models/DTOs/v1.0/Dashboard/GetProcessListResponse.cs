using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard
{
    public class GetProcessListResponse
    {
        public int ProcessRowCount {  get; set; }
        public int NewProcessCount { get; set; }
        public int currentMonth { get; set;}
    }
}
