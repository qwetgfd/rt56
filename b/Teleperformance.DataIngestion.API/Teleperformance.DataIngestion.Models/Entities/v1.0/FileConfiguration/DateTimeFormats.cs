using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration
{
    public class DateTimeFormats
    {
        public int formatId { get; set; }
        public string format { get; set; }
        public string apiFormat { get; set; }
        public int formatDataTypeId { get; set; }
    }
}
