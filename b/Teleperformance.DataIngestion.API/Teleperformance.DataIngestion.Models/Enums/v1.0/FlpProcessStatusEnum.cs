using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Enums.v1._0
{
    public enum FlpProcessStatusEnum
    {
        NotProcessed = 0,
        Processing = 1,
        Processed = 2,
        Error = 3,
        StoppedProcessing = 4,
        Skip=5,
        PartiallyCompleted = 6,
    }
}
