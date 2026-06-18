using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration
{
    public class ConfigurationRegionMapping
    {
        public int Id { get; set; }
        public string flpConfigurationId { get; set; }
        public int RegionId { get; set; }
        public string SubRegionId { get; set; }
        public int ClientId { get; set; }
        public int databaseConfigurationId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreationDateTime { get; set; }
        public DateTime? ModificationDateTime { get; set; }
        public bool IsActive { get; set; }
        public string region { get; set; }
        public string subRegion { get; set; }
        public string clientName { get; set; }
    }
}
