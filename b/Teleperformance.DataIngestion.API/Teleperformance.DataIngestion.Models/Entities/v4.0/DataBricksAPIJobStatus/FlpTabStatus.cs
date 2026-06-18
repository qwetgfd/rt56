using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v4._0.DataBricksAPIJobStatus
{
    public class FlpTabStatus
    {
        public string tabName { get; set; }
        public string tableName { get; set; }
        public string processName { get; set; }        
        public long processId { get; set; }
        public int id { get; set; }
        public int totalRows { get; set; }
        public int flpFileLogStatusId { get; set; }
        public int activityProcessStatusId { get; set; }
    }
}
