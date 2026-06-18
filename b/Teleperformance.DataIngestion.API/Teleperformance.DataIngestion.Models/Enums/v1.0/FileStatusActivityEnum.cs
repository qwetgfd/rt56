using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Enums.v1._0
{
    public enum FileStatusActivityEnum
    {
        //ProcessStarted=1,
        Processing=2,
        ProcessCompleted=3,
        Error=4,
        Skip=5
    }
}
