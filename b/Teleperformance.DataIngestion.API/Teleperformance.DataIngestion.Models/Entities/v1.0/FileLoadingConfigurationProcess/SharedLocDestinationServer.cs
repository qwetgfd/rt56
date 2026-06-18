using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess
{
    public class SharedLocDestinationServer : BaseSharedLocation
    {
        public string flpConfigurationId { get; set; }
        public string sharedLocationServerInfoId { get; set; }
    }
}
