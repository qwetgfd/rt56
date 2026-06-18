using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._0.DataBricksAPIJobStatus
{
    public class JobRunIdDetailsDto
    {

        public long LogHistoryId { get; set; }
        public string FlpConfigurationId { get; set; }
        public string UploadFileId { get; set; }
        public int? ActivityProcessStatusId { get; set; }
        public int? FlpFileLogStatusId { get; set; }
        public long? RunId { get; set; }
        public int? FlpProcessStatusId { get; set; }
        public int? DatabricksStageId { get; set; }
        public int? TerminaionDetailsId { get; set; }
        public int? LifeCycleStateId { get; set; }
        public int? ResultStateId { get; set; }
        public string? Message { get; set; }
        public string? TabName { get; set; }
        public string? blobName { get; set; }
    }


    public class JobRunIdDetailsDtoV2
    {

        public string FlpConfigurationId { get; set; }
        public string UploadFileId { get; set; }
        public long? RunId { get; set; }
        public string? TabName { get; set; }
    }
}
