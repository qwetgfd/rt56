using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration
{
    public class DateTimeFormatDto
    {
        public int FormatId { get; set; }
        public string Format { get; set; }
        public string DataTypeName { get; set; }
    }
}
