using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus
{
    public class FileUploadDetailedStatusRequest
    {
        public string flpConfigurationId { set; get; }
        public string uploadFileId { set; get; }
        public string? tabName { set; get; }
    }
}
