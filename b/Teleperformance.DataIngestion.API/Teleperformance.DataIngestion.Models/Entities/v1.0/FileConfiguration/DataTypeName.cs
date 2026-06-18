using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration
{
    public class DataTypeName
    {
        public int Id { get; set; }
        public string dataTypeName { get; set; }
        public DateTime startDTTM { get; set; }
        public DateTime endDTTM { get; set; }
    }
}
