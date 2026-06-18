using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess
{
    public class FlpProcessResponseDto
    {
        public string Message { get; set; }
        public string FileUploadedId { get; set; }
        public string BackUpFileName { get; set; }
        public int TotalRows { get; set; }
        public int DuplicateRows { get; set; }
        public int InsertedRows { get; set; }
        public string BlobName { get; set; }
     

    }
}
