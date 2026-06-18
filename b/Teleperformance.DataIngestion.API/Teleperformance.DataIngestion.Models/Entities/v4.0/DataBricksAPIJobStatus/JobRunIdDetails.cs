using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v4._0.DataBricksAPIJobStatus
{
    public class JobRunIdDetails
    {
        public long logHistoryId { get; set; }
        public string flpConfigurationId { get; set; }
        public string uploadFileId { get; set; }
        public int activityProcessStatusId { get; set; }
        public int flpFileLogStatusId { get; set; }
        public long? runId { get; set; }
        public int? flpProcessStatusId { get; set; }
        public int? databricksStageId { get; set; }
        public int? terminaionDetailsId { get; set; }
        public int? lifeCycleStateId { get; set; }
        public int? resultStateId { get; set; }
        public string? message { get; set; }
        public string? tabName { get; set; }

    }

    public class JobRunIdDetailsV2
    {
        public string flpConfigurationId { get; set; }
        public string uploadFileId { get; set; }
        public long? runId { get; set; }
        public string? tabName { get; set; }

    }
}
