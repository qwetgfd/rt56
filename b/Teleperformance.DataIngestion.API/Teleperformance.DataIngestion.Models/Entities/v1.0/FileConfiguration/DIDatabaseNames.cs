using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration
{
    public class DIDatabaseNames
    {
        public int Id { get; set; }
        public string DatabaseName { get; set; }
        public int RegionId { get; set; }
        public string SubRegionId { get; set; }
        public int ClientNameId { get; set; }
        public string DatabaseServer { get; set; }
        public DateTime StartDTTM { get; set; }
        public DateTime EndDTTM { get; set; }
        public bool? defaultDB { get; set; }
    }
}
