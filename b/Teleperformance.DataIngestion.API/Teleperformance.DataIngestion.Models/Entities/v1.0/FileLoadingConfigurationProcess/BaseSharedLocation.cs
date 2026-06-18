using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess
{
    public class BaseSharedLocation : DatabaseResponse
    {
        public string serverName { get; set; }
        public string folderName { get; set; }
        public string userName { get; set; }
        public string domain { get; set; }
        public string password { get; set; }
    }
}
