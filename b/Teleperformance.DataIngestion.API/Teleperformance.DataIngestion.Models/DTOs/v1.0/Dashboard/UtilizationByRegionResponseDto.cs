using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard
{
    public class UtilizationByRegionResponseDto:UtilizationAPIBaseClass
    {
        public int RegionId { get; set; }
        public string RegionName { get; set; }
    }
}
