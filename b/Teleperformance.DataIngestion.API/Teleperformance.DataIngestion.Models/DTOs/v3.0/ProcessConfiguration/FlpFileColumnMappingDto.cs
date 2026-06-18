using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration
{
    public class FlpFileColumnMappingDto
    {
        public string FlpConfigurationId { get; set; }
        public string FileColumn { get; set; }
        public string DbColumn { get; set; }        
        public int DataTypeId { get; set; }
        public int FormatId { get; set; }
        public string TabName { get; set; }
        public string dataType { get; set; }
    }
}
