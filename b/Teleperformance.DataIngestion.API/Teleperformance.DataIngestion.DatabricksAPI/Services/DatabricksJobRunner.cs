using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DatabricksAPI.Model;

namespace Teleperformance.DataIngestion.DatabricksAPI.Services
{
    public class DatabricksJobRunner
    {
        private readonly HttpClient _httpClient;
        private readonly string _databricksInstance;
        private readonly string _apiVersion;
        private readonly string _personalAccessToken;

        public DatabricksJobRunner(string databricksInstance, string apiVersion, string personalAccessToken)
        {
            _databricksInstance = databricksInstance;
            _apiVersion = apiVersion;
            _personalAccessToken = personalAccessToken;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri($"https://{_databricksInstance}/api/{_apiVersion}/")
            };
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_personalAccessToken}");
        }

        public async Task<JobRunAPIResponse> RunJobAsync(long jobId, object jobParameters)
        {

            var jobRunAPIResponse = new JobRunAPIResponse();
            try
            {
                jobRunAPIResponse.JobRunSuccess = false;
                var requestBody = new
                {
                    idempotency_token = Guid.NewGuid().ToString(),
                    job_id = jobId,
                    job_parameters = jobParameters,
                    performance_target = "PERFORMANCE_OPTIMIZED",
                   // pipeline_params = new { full_refresh = false },
                    queue = new { enabled = true }
                };

                var jsonContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("jobs/run-now", jsonContent);
                if (response.IsSuccessStatusCode)
                {
                    jobRunAPIResponse.JsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);

                    var responseContent = await response.Content.ReadAsStringAsync();
                    var successResponse = System.Text.Json.JsonSerializer.Deserialize<JobRunSuccessResponse>(responseContent);
                    jobRunAPIResponse.JobRunSuccess = true;
                    jobRunAPIResponse.StatusCode = response.StatusCode;
                    jobRunAPIResponse.RunId = successResponse?.RunId;
                    jobRunAPIResponse.ResponseContent = responseContent;
                    jobRunAPIResponse.Message = "Successfully called API";
                    return jobRunAPIResponse;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorResponse = System.Text.Json.JsonSerializer.Deserialize<ErrorResponse>(errorContent);
                    jobRunAPIResponse.StatusCode = response.StatusCode;
                    jobRunAPIResponse.RunId = null;
                    jobRunAPIResponse.ResponseContent = errorContent;               
                    
                    jobRunAPIResponse.Message = errorResponse?.Message??"";
                    // throw new Exception($"Error: {response.StatusCode}, Details: {errorContent}");
                    return jobRunAPIResponse;
                }
            }
            catch (Exception ex)
            {
                jobRunAPIResponse.StatusCode = HttpStatusCode.InternalServerError;
                jobRunAPIResponse.RunId =null;
                jobRunAPIResponse.ResponseContent =string.Empty;
                jobRunAPIResponse.Message = $"Failed to run Databricks job: {ex.Message}";
                return jobRunAPIResponse;
                //throw new Exception($"Failed to run Databricks job: {ex.Message}", ex);
            }
        }
        public async Task<JobRunStatusAPIResponse> GetJobRunStatusAsync(long runId)
        {
            var jobRunStatusAPIResponse = new JobRunStatusAPIResponse();
            try
            {
               
                var response = await _httpClient.GetAsync($"jobs/runs/get?run_id={runId}");
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();

                    //var run = JsonSerializer.Deserialize<RunJob>(responseContent);
                    //// Convert back to JSON string
                    //string jsonToStore = JsonSerializer.Serialize(run);

                    var settings = new JsonSerializerSettings
                    {
                        ContractResolver = new DefaultContractResolver
                        {
                            NamingStrategy = new SnakeCaseNamingStrategy()
                        }
                    };
                    var run = JsonConvert.DeserializeObject<RunJob>(responseContent, settings);

                    // Fix: Use JsonConvert.SerializeObject instead of JsonSerializer.Serialize
                    string jsonToStore = JsonConvert.SerializeObject(run);
                    // Extract state and state_message
                    if (run?.Status.TerminationDetails != null)
                    {
                        jobRunStatusAPIResponse.StatusCode = response.StatusCode;
                        jobRunStatusAPIResponse.Message = $"Success: Status:{run?.Status},message: {run?.Status.TerminationDetails.Message}";
                    }
                    else if (run?.State != null)
                    {
                        if (run.State.LifeCycleState == "INTERNAL_ERROR" && run.State.ResultState == "FAILED")
                        {
                           
                            jobRunStatusAPIResponse.StatusCode = HttpStatusCode.InternalServerError;
                            jobRunStatusAPIResponse.Message = $"Error: Life cycle state:{run.State?.LifeCycleState ?? ""}, result state:{run.State?.ResultState ?? ""}, Message:{run.State?.StateMessage ?? ""}";
                        }
                        else if (run.State.LifeCycleState == "TERMINATED" && run.State.ResultState == "SUCCESS")
                        {
                            //Always ok
                            jobRunStatusAPIResponse.StatusCode = response.StatusCode;
                            jobRunStatusAPIResponse.Message = $"Error: Life cycle state:{run.State?.LifeCycleState ?? ""}, result state:{run.State?.ResultState ?? ""}, Message:{run.State?.StateMessage ?? ""}";
                        }
                        else
                        {
                            //Always ok
                            jobRunStatusAPIResponse.StatusCode = response.StatusCode;
                            jobRunStatusAPIResponse.Message = $"Success: Status:{run?.State},message: {run?.State.StateMessage}";
                        }
                       
                    }
                    else
                    {
                        jobRunStatusAPIResponse.StatusCode = HttpStatusCode.InternalServerError;
                        jobRunStatusAPIResponse.Message = $"Internal server error: No state information available.";

                    }
                    jobRunStatusAPIResponse.JobRunSuccess = true;
                    jobRunStatusAPIResponse.ResponseContent = jsonToStore;
                    jobRunStatusAPIResponse.JobSatus = run;
                    return jobRunStatusAPIResponse;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorResponse = System.Text.Json.JsonSerializer.Deserialize<ErrorResponse>(errorContent);
                    jobRunStatusAPIResponse.StatusCode = response.StatusCode;
                    jobRunStatusAPIResponse.ResponseContent = errorContent;
                    jobRunStatusAPIResponse.Message = $"Error: {response.StatusCode}, Details:error_code: {errorResponse?.ErrorCode ?? ""} and message: {errorResponse?.Message ?? ""}";
                    return jobRunStatusAPIResponse;

                }
            }
            catch (Exception ex)
            {
                jobRunStatusAPIResponse.StatusCode = HttpStatusCode.InternalServerError;
                jobRunStatusAPIResponse.RunId = null;
                jobRunStatusAPIResponse.ResponseContent = string.Empty;
                jobRunStatusAPIResponse.Message = $"Failed to run Databricks job: {ex.Message}";
                return jobRunStatusAPIResponse;

            }
        }
    }


    
    public class JobRunSuccessResponse
    {
        [JsonPropertyName("run_id")]
        public long? RunId { get; set; }

        [JsonPropertyName("number_in_job")]
        public long? NumberInJob { get; set; }

        //[JsonPropertyName("state")]
        //public string State { get; set; }
    }

    public class ErrorResponse
    {
        [JsonPropertyName("error_code")]
        public object? ErrorCode { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        //[JsonPropertyName("details")]
        //public List<ErrorDetail> Details { get; set; }
    }

}
