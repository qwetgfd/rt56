using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration
{
    public class DIDatabaseNameDto
    {
        public int Id { get; set; }
        public string DatabaseName { get; set; }
        public string DatabaseServer { get; set; }
        public bool? DefaultDB { get; set; }
    }
}
