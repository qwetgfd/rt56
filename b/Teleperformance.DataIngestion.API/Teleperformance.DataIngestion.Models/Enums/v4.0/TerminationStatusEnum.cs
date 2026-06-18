using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Enums.v4._0
{
    public enum TerminationStatusEnum
    {
        SUCCESS = 1,
        USER_CANCELED = 2,
        CANCELED = 3,
        SKIPPED = 4,
        INTERNAL_ERROR = 5,
        DRIVER_ERROR = 6,
        CLUSTER_ERROR = 7,
        REPOSITORY_CHECKOUT_FAILED = 8,
        INVALID_CLUSTER_REQUEST = 9,
        WORKSPACE_RUN_LIMIT_EXCEEDED = 10,
        FEATURE_DISABLED = 11,
        CLUSTER_REQUEST_LIMIT_EXCEEDED = 12,
        STORAGE_ACCESS_ERROR = 13,
        RUN_EXECUTION_ERROR = 14,
        UNAUTHORIZED_ERROR = 15,
        LIBRARY_INSTALLATION_ERROR = 16,
        MAX_CONCURRENT_RUNS_EXCEEDED = 17,
        MAX_SPARK_CONTEXTS_EXCEEDED = 18,
        RESOURCE_NOT_FOUND = 19,
        INVALID_RUN_CONFIGURATION = 20,
        CLOUD_FAILURE = 21,
        MAX_JOB_QUEUE_SIZE_EXCEEDED = 22
    }
}
