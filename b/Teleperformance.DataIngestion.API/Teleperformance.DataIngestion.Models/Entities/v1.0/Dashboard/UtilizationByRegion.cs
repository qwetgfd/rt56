using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.Dashboard
{
    public class UtilizationByRegion: UtilizationBaseEntity
    {
        public  int  regionId { get; set; }
        public  string  regionName { get; set; }
    }
}
