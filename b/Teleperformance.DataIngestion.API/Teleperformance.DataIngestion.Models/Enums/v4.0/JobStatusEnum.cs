using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Enums.v4._0
{
    public enum JobStatusEnum
    {
        BLOCKED=1,
        PENDING=2,
        QUEUED=3,
        RUNNING=4,
        TERMINATING=5,
        TERMINATED=6,
        WAITING=7
    }

    public enum LifeCycleStateEnum
    {
        [Description("PENDING")]
        PENDING = 1,
        [Description("RUNNING")]
        RUNNING = 2,
        [Description("TERMINATING")]
        TERMINATING = 3,
        [Description("TERMINATED")]
        TERMINATED = 4,
        [Description("SKIPPED")]
        SKIPPED = 5,
        [Description("INTERNAL_ERROR")]
        INTERNAL_ERROR = 6
    }

    public enum ResultStateEnum
    {
        [Description("SUCCESS")]
        SUCCESS = 1,
        [Description("FAILED")]
        FAILED = 2,
        [Description("TIMED_OUT")]
        TIMED_OUT = 3,
        [Description("CANCELED")]
        CANCELED = 4
    }


}
