using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v2._0.ProcessConfiguration
{
    public class ProcessNamesDto
    {
        public long Id { get; set; }
        public string FLPConfigurationId { get; set; }
        public string ProcessNames { get; set; }
        public string ProcessNamesMore { get; set; }
        public string Description { get; set; }
    }
}
