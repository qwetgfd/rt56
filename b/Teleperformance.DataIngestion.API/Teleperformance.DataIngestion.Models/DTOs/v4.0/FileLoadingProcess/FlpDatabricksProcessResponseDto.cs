using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._0.FileLoadingProcess
{
    public class FlpDatabricksProcessResponseDto: FlpProcessResponseDto
    {
        public string DataLakeStorageAccountPath { get; set; }
        public long? RunId { get; set; }
        public int? JobStatusId { get; set; }
        public int? TerminationStatusId { get; set; }
        public int? LifeCycleStateId { get; set; }
        public int? ResultStateId { get; set; }
        public float fileSize { get; set; }
        public string csvTempBlobName { get; set; }
    }
}
