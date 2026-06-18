using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.Common.Crypto;
using Teleperformance.DataIngestion.DataAccess.Services.v3._0;
using Teleperformance.DataIngestion.DataAccess.Services.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.EIB;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.ValidateFileRules;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;

namespace Teleperformance.DataIngestion.DataAccess.Helpers
{
    public class EIBTemplateServiceHelpers
    {
        private readonly ILogger<EIBServiceV4_1> _logger;
        public EIBTemplateServiceHelpers(ILogger<EIBServiceV4_1> logger)
        {
            _logger = logger;
        }
        public async  Task<APIResponse<EIBTemplateResponse>> GenerateEIBTemplate(string EIBId,string token)
        {

            try
            {
                //string apiUrl = "http://127.0.0.1:8000/";
                _logger.LogInformation($"GenerateEIBTemplate API called for {EIBId}");
                string apiUrl = KeyVault.GetKeyVaultValue("TPDataIngestionEIBTemplateAPIURL").Result;
                string bearerToken = token;
                string tokenEncryptionKey = KeyVault.GetKeyVaultValue("TPDataIngestionEncryptionKey").Result;
                bearerToken = Crypto.Encrypt(bearerToken, tokenEncryptionKey);
                EIBTemplateRequest eIBTemplateRequest = new EIBTemplateRequest();
                eIBTemplateRequest.EIBId = EIBId;
                string jsonString = JsonConvert.SerializeObject(eIBTemplateRequest);
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(60);
                    client.BaseAddress = new Uri(apiUrl);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    //client.DefaultRequestHeaders.Add("x-tpdi-api-version", apiVersion);
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + bearerToken);
                    var response = await client.PostAsync($"EIB/generateEIB", content);
                    // Check if the request was successful
                    if (response.IsSuccessStatusCode)
                    {
                        var res = await response.Content.ReadAsStringAsync();
                        var apiResponse = JsonConvert.DeserializeObject<EIBTemplateResponse?>(res);                        
                        return new APIResponse<EIBTemplateResponse>
                        {
                            ResultStatus = APIResultStatus.Completed,
                            ResponseMessage = ["Successfully."],
                            Result = apiResponse
                        };
                    }
                    else
                    {
                        var res = await response.Content.ReadAsStringAsync();
                        string msg = res.ToString();
                        string message = $"Response code - {(int)response.StatusCode},Reason Phrase  {response.ReasonPhrase} error: {msg}";
                        _logger.LogError($"GenerateEIBTemplate Exception error occurred at line No-72: {EIBId}, {message}");
                        return new APIResponse<EIBTemplateResponse>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = ["Internal error occurred"],
                            Result = null
                        };
                    }

                }
            }
            catch (Exception ex)
            {
                //TODO: update di_EIBProcessingQueue to error (4)s

                _logger.LogError($"GenerateEIBTemplate Exception error occurred: {EIBId}, {ex.Message.ToString()}");
                return new APIResponse<EIBTemplateResponse>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = ["Internal error occurred"],
                    Result = null
                };
            }            

        }

    }
}
