using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v4._1
{
    public class DatabricksJsonFileColumnEntities
    {
        public int id { get; set; }
        public string fileURL { get; set; }
        public string flpConfigurationId { get; set; }
        public string uploadFileId { get; set; }
        public string sasKey { get; set; }
        public string tabName { get; set; }
        public string storageContainerName { get; set; }
    }
}
