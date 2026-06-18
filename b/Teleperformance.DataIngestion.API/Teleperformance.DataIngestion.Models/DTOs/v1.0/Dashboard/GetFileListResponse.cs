using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard
{
    public class GetFileListResponse
    {
        public int totalUploadedFiles { get; set; }
        public int successCount { get; set; }
        public int failureCount { get; set; }
        public int newFileProcessCount {  get; set; }
        public int currentMonth { get; set; }

    }
}
