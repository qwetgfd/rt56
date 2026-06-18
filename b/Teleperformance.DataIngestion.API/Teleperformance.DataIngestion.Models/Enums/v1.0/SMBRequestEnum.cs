using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Enums.v1._0
{
    public enum SMBRequestEnum
    {
        None = 0,
        FileListFromLocation = 1,
        Stream = 2,
        CopiedFileToArchivedFolder = 3,
        DeleteFile = 4,
        CopyFileFromBlob = 5
    }
}
