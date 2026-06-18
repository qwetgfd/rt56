using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._0.DataBricksAPIJobStatus
{
    public class FlpProcessLogHistoryStatusDto
    {
        public long? LogHistoryId { get; set; }
        public string FlpConfigurationId { get; set; }
        public int? FlpFileLogStatusId { get; set; }
        public int? ActivityProcessStatusId { get; set; }
        public string FileUploadedId { get; set; }
        public int? FlpProcessStatusId { get; set; }
        public int? DatabricksStageId { get; set; }
        public int? TerminationDetailsId { get; set; }
        public int? LifeCycleStateId { get; set; }
        public int? ResultStateId { get; set; }
        public long? RunId { get; set; }
        public string DatabricksAPIResponse { get; set; }
        public string Message { get; set; }
        public bool SkipUpdateHistoryStatus { get; set; }
        public string? tabName { get; set; }
    }
}
    