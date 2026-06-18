using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingProcess
{
    public class FlpProcessResponseDto
    {
        public string Message { get; set; }
        public string FileUploadedId { get; set; }
        public string BackUpFileName { get; set; }
        public List<FlpProcessTabResponseDto> FlpProcessTabResponseList { get; set; }
    }

    public class FlpProcessTabResponseDto
    {
        public string TabName { get; set; }
        public string BackUpFileName { get; set; }
        public int TotalRows { get; set; }
        public int DuplicateRows { get; set; }
        public int InsertedRows { get; set; }
        public string BlobName { get; set; }
        public long ProcessId { get; set; }
        public string TableName { get; set; }
        public bool FileProcessCompleted { get; set; }
        public int DatabricksProcessStatusId { get; set; }
    }
}
