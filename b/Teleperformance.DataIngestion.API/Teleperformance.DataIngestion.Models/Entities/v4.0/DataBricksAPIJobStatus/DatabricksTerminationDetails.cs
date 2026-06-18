using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v4._0.DataBricksAPIJobStatus
{
    public class DatabricksTerminationDetails
    {
        public int TerminationDetailsId { get; set; }
        public string TerminationDetailsName { get; set; }
        public string Message { get; set; }
    }
}
