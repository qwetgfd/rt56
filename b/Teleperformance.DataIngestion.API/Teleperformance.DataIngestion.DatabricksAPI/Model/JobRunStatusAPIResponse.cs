using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.DatabricksAPI.Model
{
    public class JobRunStatusAPIResponse : JobRunAPIResponse
    {
        public bool RunAPISuccess { get; set; }
        public  RunJob? JobSatus { get; set; }
    }

    public class RunJob
    {
        [JsonPropertyName("status")]
        public JobRunStatus Status { get; set; }

        [JsonPropertyName("state")]
        public State State { get; set; }
        public dynamic? job_parameters { get; set; }
    }

    public class JobRunStatus
    {
        [JsonPropertyName("queue_details")]
        public QueueDetails QueueDetails { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; } // e.g., "BLOCKED", "RUNNING"

        [JsonPropertyName("termination_details")]
        public TerminationDetails? TerminationDetails { get; set; }
    }

    public class QueueDetails
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } // e.g., "ACTIVE_RUNS_LIMIT_REACHED"

        [JsonPropertyName("message")]
        public string Message { get; set; } // Additional details about the queue
    }

    public class TerminationDetails
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } // e.g., "SUCCESS", "FAILED"

        [JsonPropertyName("message")]
        public string Message { get; set; } // Additional details about termination

        [JsonPropertyName("type")]
        public string Type { get; set; } // e.g., "SUCCESS", "ERROR"
    }


    public class State
    {
        [JsonPropertyName("life_cycle_state")]
        public string LifeCycleState { get; set; }

        [JsonPropertyName("result_state")]
        public string ResultState { get; set; }

        [JsonPropertyName("state_message")]
        public string StateMessage { get; set; }

        [JsonPropertyName("user_cancelled_or_timedout")]
        public bool? UserCancelledOrTimedout { get; set; } // <-- Fix: use bool?
    }

}
