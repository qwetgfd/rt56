using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Common;

namespace Teleperformance.DataAssist
{
    public class OpenAIBase
    {
        protected IConfiguration _configuration;
        public OpenAIBase(IConfiguration configuration)
        {
            _configuration = configuration;

        }

        public async IAsyncEnumerable<string> QueryAI(string modelName, string userPrompt, string systemPrompt, string filedata = "", string context = "", string schemaContext = "")
        {
            var gpto4mini_endpoint = KeyVault.GetKeyVaultValue("gpto4miniendpoint").Result;
            var gpto4mini_endpoint_http = KeyVault.GetKeyVaultValue("gpto4miniendpointhttp").Result;
            var gpto4mini_deployment_name = KeyVault.GetKeyVaultValue("gpto4minideploymentname").Result;
            var gpto4mini_key = KeyVault.GetKeyVaultValue("gpto4minikey").Result;

            modelName = "GPT-o4";

            switch (modelName)
            {
                case "GPT-4o":
                    await foreach (var response in GetOpenAIResponse(_configuration["OpenAI:gpt4o_endpoint"], _configuration["OpenAI:gpt4o_deployment_name"], _configuration["OpenAI:gpt4o_key"], userPrompt, systemPrompt, filedata, context, schemaContext))
                    {
                        yield return response;
                    }
                    break;

                case "GPT-4o_http":
                    yield return await GetOpenAIResponseHttp(_configuration["OpenAI:gpt4o_endpoint_HTTP"], _configuration["OpenAI:gpt4o_key"], userPrompt, systemPrompt, filedata, context, schemaContext);
                    break;

                case "GPT-o4":
                    await foreach (var response in GetOpenAIResponse(gpto4mini_endpoint, gpto4mini_deployment_name, gpto4mini_key, userPrompt, systemPrompt, filedata, context, schemaContext))
                    {
                        yield return response;
                    }
                    break;

                case "GPT-o4_http":
                    yield return await GetOpenAIResponseHttp(gpto4mini_endpoint_http, gpto4mini_key, userPrompt, systemPrompt, filedata, context, schemaContext);
                    break;

                default:
                    yield return "ERROR: Unsupported model name.";
                    break;
            }
        }

        public async IAsyncEnumerable<string> GetOpenAIResponse(string endpoint, string deploymentName, string key, string userPrompt, string systemPrompt, string filedata = "", string context = "", string schemaContext = "")
        {
            

            AzureOpenAIClient _openAIClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));
            ChatClient chatClient = _openAIClient.GetChatClient(deploymentName);

            string content = "<FILE_ATTACHMENT>" + filedata + "</FILE_ATTACHMENT> <SCHEMA>" + schemaContext + "</SCHEMA><CONVERSATION_HISTORY>" + context + "</CONVERSATION_HISTORY> \n User Question: " + userPrompt + "";

            AsyncCollectionResult<StreamingChatCompletionUpdate> completionUpdates = chatClient.CompleteChatStreamingAsync(
                [
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(content),
                    //new OpenAI.Chat.AssistantChatMessage("<FILE_ATTACHMENT>{filedata}</FILE_ATTACHMENT> <SCHEMA>{schemaContext}</SCHEMA>"),
                ]
            //new ChatCompletionOptions
            //{
            //    Temperature = _configuration.GetValue<float>("OpenAI:Temperature", (float)0.5),
            //    TopP = _configuration.GetValue<float>("OpenAI:TopP", (float)0.95),
            //    MaxOutputTokenCount = _configuration.GetValue<int>("OpenAI:MaxTokens", 40000),
            //}
            );

            await foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
            {
                foreach (ChatMessageContentPart contentPart in completionUpdate.ContentUpdate)
                {
                    yield return contentPart.Text;
                }
            }
            yield break;
        }

        public async Task<string> GetOpenAIResponseHttp(string endpoint, string key, string userPrompt, string systemPrompt, string filedata = "", string context = "", string schemaContext = "")
        {
            try
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
                requestMessage.Headers.Add("api-key", key);

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(5);
                    var payload = new
                    {
                        messages = new[]
                        {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = $"<FILE_ATTACHMENT>{{filedata}}</FILE_ATTACHMENT> <SCHEMA>{{schemaContext}}</SCHEMA><CONVERSATION_HISTORY>{context}</CONVERSATION_HISTORY> \n User Question: {userPrompt}" },
                        //new { role = "assistant", content = "<FILE_ATTACHMENT>{filedata}</FILE_ATTACHMENT> <SCHEMA>{schemaContext}</SCHEMA>" }
                    },
                        temperature = _configuration.GetValue<float>("OpenAI:Temperature", (float)0.5),
                        top_p = _configuration.GetValue<float>("OpenAI:TopP", (float)0.95),
                        max_tokens = _configuration.GetValue<int>("OpenAI:MaxTokens", 40000),
                    };

                    requestMessage.Content = new StringContent(
                        JsonSerializer.Serialize(payload),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );

                    var response = await client.SendAsync(requestMessage);

                    response.EnsureSuccessStatusCode();

                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (string.IsNullOrWhiteSpace(responseContent))
                    {
                        return await Task.FromResult("ERROR: Received empty response from OpenAI.");
                    }

                    var responseJson = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    string openairesponse = responseJson.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

                    if (string.IsNullOrEmpty(openairesponse))
                    {
                        return await Task.FromResult("ERROR: OpenAI response is empty.");
                    }

                    return await Task.FromResult(openairesponse.Trim());
                }
            }
            catch (Exception ex)
            {
                return await Task.FromResult($"ERROR: {ex.Message}");
            }
        }

    }
}
