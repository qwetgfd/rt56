using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess
{
    public class UploadedFile : DatabaseResponse
    {
        public string uploadFileId { get; set; }
    }
}
