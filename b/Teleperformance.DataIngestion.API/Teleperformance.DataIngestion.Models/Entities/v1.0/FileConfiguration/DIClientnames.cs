using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration
{
    public class DIClientnames
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime StartDTTM { get; set; }
        public DateTime EndDTTM { get; set; }
    }
}
