using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.EIB;

namespace Teleperformance.DataIngestion.Models.Entities.v4._1.EIB
{
    public class DBViews
    {
        public int viewId { get; set; }
        public string viewName { get; set; }
        public int columnCount { get; set; }
    }
  

    public class EIBGenerationStatus
    {
        public string EIBId { get; set; }
        public string status { get; set; }
        public string fileURL { get; set; }
        public bool hasActiveFileURL { get; set; }
        public string errorMessage { get; set; }
        public string generationStartDateTime { get; set; }
    }

    public sealed class ProfilingLogs
    {
        public int Id { get; set; }
        public string runningStatus { get; set; }
        public Guid RunId { get; set; }
        //public long ProcessedId { get; set; }
        public DateTime InsertedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        //public string Description { get; set; }

        public string rule { get; set; }
        public string reason { get; set; }
        public string status { get; set; }
        public int Total { get; set; }
    }

}
