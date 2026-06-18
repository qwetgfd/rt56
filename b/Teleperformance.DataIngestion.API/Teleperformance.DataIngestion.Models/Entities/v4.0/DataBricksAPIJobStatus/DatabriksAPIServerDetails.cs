using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v4._0.DataBricksAPIJobStatus
{
    public class DatabriksAPIServerDetails
    {
        public string flpConfigurationId { get; set; }
        public string databricksAPIToken { get; set; }
        public string databricksInstance { get; set; }
        public string databricksAPIVersion { get; set; }
    }
}
