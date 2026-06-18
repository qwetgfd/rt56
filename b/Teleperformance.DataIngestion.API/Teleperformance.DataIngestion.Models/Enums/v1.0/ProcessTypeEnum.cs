using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Enums.v1._0
{
    public enum ProcessTypeEnum
    {
        UIUpload = 1,
        SharedLocationUpload = 2,
        BlobStarageUpload = 3,
        CustomAPI =4,
        SFTP =5,
        SharePointWorkspace = 6
    }
}
