
using Azure;
using CsvHelper;
using DocumentFormat.OpenXml.Math;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using System.Data;
using System.Diagnostics;
using System.Net;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1.DataAssists;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.DataAssists;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.ValidateFileRules;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v2._0;
using APIResultStatus = Teleperformance.DataIngestion.Models.Entities.v1._0.APIResultStatus;

namespace Teleperformance.DataAssist
{
    public class DataAssists : IDataValidationServiceV4_1
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IDataValidationRepositoryV4_1 repository;
        private readonly ILogger<DataAssists> _logger;
        //IDataValidationRepositoryV4_1 repository
        public DataAssists(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILogger<DataAssists> logger, IDataValidationRepositoryV4_1 repository
            )
        {
            this._configuration = configuration;
            this.httpContextAccessor = httpContextAccessor;
            this.repository = repository;
            this._logger = logger;
        }
        //public async Task<APIResponse<Dictionary<string, List<Dictionary<string, string>>>>> GenerateResponse(ProjectQueryFormRequest request)
        //public async Task<APIResponse<string>> GenerateResponse(ProjectQueryFormRequest request)
        public async Task<string> GenerateResponse(ProjectQueryFormRequest request)
        {
            Dictionary<string, List<Dictionary<string, string>>> responseListCode = new Dictionary<string, List<Dictionary<string, string>>>();
            string result = string.Empty;
            _logger.LogError("Entered GenerateResponse method in DataAssists class. LINE no 44");
            try
            {


                string fileContent = string.Empty;
                if (request.isDataValidation)
                {
                    _logger.LogError("Entered GenerateResponse method in DataAssists class. LINE no 52");
                    OpenAIBase openAIreq = new OpenAIBase(_configuration);
                    string CSVData = string.Empty;

                    if (request.isCodeSnippet)
                    {
                        CSVData = GenerateSampleCSVData(request.versionId, request.fileHeaders).Result;
                    }
                    //else
                    //{
                    //    if (!(request.File == null || request.File.Length == 0))
                    //    {
                    //        using var reader = new StreamReader(request.File.OpenReadStream());
                    //        fileContent = await reader.ReadToEndAsync();
                    //    }
                    //    string[] fileLines = fileContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    //    CSVData = string.Join("\n", fileLines.Take(10));
                    //}
                    _logger.LogError("Entered GenerateResponse method in DataAssists class. LINE no 70");
                    int flowId = request.flowid;
                    int projectId = request.projectId;
                    string userPrompt = "";

                    if (request.overrideExistingRules)
                    {
                        if (request.validationRules != null && request.validationRules.Length > 0)
                        {
                            userPrompt = request.validationRules;
                        }
                        else
                        {
                            if (flowId != 0 && projectId != 0)
                            {
                                var prompts = await repository.getPrompt(flowId, projectId);
                                userPrompt = prompts[0].Prompt;
                            }
                        }
                    }
                    else
                    {
                        if (flowId != 0 && projectId != 0)
                        {
                            var prompts = await repository.getPrompt(flowId, projectId);
                            userPrompt = prompts[0].Prompt;
                        }
                        if (request.validationRules != null && request.validationRules.Length > 0)
                        {
                            userPrompt += "\n <additional rules>" + request.validationRules + "</additional rules>";
                        }
                    }
                    _logger.LogError("Entered GenerateResponse method in DataAssists class. LINE no 101");
                    string[] validationRules = await GenerateValidationRulesList(request.versionId, userPrompt);

                    Dictionary<string, List<string>> responseList = new Dictionary<string, List<string>>();


                    string combinedValidationRules = string.Empty;
                    foreach (string vrule in validationRules)
                    {
                        combinedValidationRules += "VALIDATION_RULE: " + vrule + "\n";
                    }

                    string[] validationRulesNew = [combinedValidationRules];

                    foreach (string vrule in validationRulesNew)
                    {
                        int retryCount = 0;
                        int maxRetries = 3;
                        List<string> vrResult = new List<string>();
                        string errorMessage = String.Empty;

                        while (retryCount < maxRetries)
                        {
                            string promptVR = "I have a CSV file loaded into the pandas DataFrame in python. I want to validate the data based on the following rule: ";
                            promptVR += "\n" + vrule + "\n";
                            promptVR += "\n Following is the sample CSV file to process - create the validation rules based on this : ";
                            promptVR += "\n<CSV_FILE_BEGIN>" + CSVData + "<CSV_FILE_END>\n";
                            promptVR += "\n TASK: Please return a python function for this rule. The python function must take in a Pandas DataFrame as argument. Use Pandas library. The python function must return a list of rows that violate this rule. When comparing numerical type of data, keep only numbers and decimals. Return a JSON object in the following structure: ";
                            promptVR += @"
{
    'rule': 'Short description of the validation rule',
    'function_name': 'Name of the python function',
    'code': 'Python code',
}
RETURN ONLY JSON OBJECT AND NOTHING ELSE
Make sure it is not preceded by any text (like json) before the json object

" + errorMessage + @"
";

                            Console.WriteLine("======================================================\n\n");
                            Console.WriteLine(promptVR);
                            Console.WriteLine("======================================================\n\n");


                            string vrResponseTmp = null;
                            await foreach (var textResp in openAIreq.QueryAI("GPT-o4_http", promptVR, "", "", request.userContext))
                            {
                                vrResponseTmp += textResp;
                            }
                            _logger.LogError($"Entered GenerateResponse method in DataAssists class. LINE no 142 {vrResponseTmp} ");
                            vrResponseTmp = vrResponseTmp.Replace("`", "");
                            ValidationRule validationRule = JsonConvert.DeserializeObject<ValidationRule>(vrResponseTmp);

                            string ruleName = (string)validationRule.rule;
                            string functionName = (string)validationRule.function_name;
                            string code = (string)validationRule.code;

                            _logger.LogError($"Entered GenerateResponse method in DataAssists class. LINE no 150 {code} ");
                            Console.WriteLine("======================================================\n\n");
                            Console.WriteLine(code);
                            Console.WriteLine("======================================================\n\n");
                            #region ValidateDataWithPythonFunction


                            vrResult = await ValidateDataWithPythonFunction(CSVData, ruleName, functionName, code);
                            if (vrResult == null || (vrResult.Count == 1 && vrResult[0].Contains("no_violations")) || (vrResult.Count == 1 && vrResult[0].Contains("ERROR")))
                            {
                                if (vrResult.Count == 1 && vrResult[0].Contains("no_violations"))
                                {
                                    Console.WriteLine("======================================================\n\n");
                                    Console.WriteLine("NO VIOLATIONS");
                                    Console.WriteLine("======================================================\n\n");
                                }
                                if (vrResult.Count == 1 && vrResult[0].Contains("ERROR"))
                                {
                                    errorMessage = @"
                        IMPORTANT:
                        Your instructions remain the same. Follow them carefully. Keep this in consideration : We already ran the following code which is giving an error. Do not repeat your mistakes.
                        <ErrorMessage>" + vrResult[0] + @"</ErrorMessage>
                        <Previous_Code>" + code + @"</Previous_Code>
                        ";
                                }
                                else
                                {
                                    errorMessage = String.Empty;
                                }
                                retryCount++;

                                if (retryCount >= maxRetries)
                                {
                                    if (vrResult.Count == 1 && vrResult[0].Contains("no_violations"))
                                    {
                                        if (request.isCodeSnippet)
                                        {
                                            responseListCode.Add(ruleName, new List<Dictionary<string, string>> {
                                                                    new Dictionary<string, string> {
                                                                        { "function_name", functionName },
                                                                        { "code", code }
                                                                    }
                                                                });
                                        }
                                        else
                                        {
                                            responseList.Add(ruleName, ["NO_VIOLATIONS"]);
                                        }
                                    }
                                    else if (vrResult.Count == 1 && vrResult[0].Contains("ERROR"))
                                    {
                                        responseList.Add(ruleName, ["ERROR"]);
                                    }
                                    else
                                    {
                                        responseList.Add(ruleName, ["NULL"]);
                                    }
                                }
                            }
                            else
                            {
                                if (request.isCodeSnippet)
                                {
                                    responseListCode.Add(ruleName, new List<Dictionary<string, string>> {
                                                            new Dictionary<string, string> {
                                                                { "function_name", functionName },
                                                                { "code", code }
                                                            }
                                                        });
                                }
                                else
                                {
                                    responseList.Add(ruleName, vrResult);
                                }
                                break; // Success, exit the loop
                            }
                            #endregion
                            //if (request.isCodeSnippet)
                            //{
                            //    responseListCode.Add(ruleName, new List<Dictionary<string, string>> {
                            //        new Dictionary<string, string> {
                            //            { "function_name", functionName },
                            //            { "code", code }
                            //        }
                            //    });
                            //}
                            //_logger.LogError($"Entered GenerateResponse method in DataAssists class. LINE no 236 {functionName} ");
                        }
                    }

                    if (request.isCodeSnippet)
                    {
                        //await Response.WriteAsync("{\"Result\": " + JsonConvert.SerializeObject(responseListCode) + "}");
                        result = "{\"Result\": " + JsonConvert.SerializeObject(responseListCode) + "}";
                        _logger.LogError($"Entered GenerateResponse method in DataAssists class. LINE no 245 {result} ");
                    }
                    else
                    {
                        //await Response.WriteAsync("{\"Result\": " + JsonConvert.SerializeObject(responseList) + "}");
                        result = "{\"Result\": " + JsonConvert.SerializeObject(responseListCode) + "}";
                        _logger.LogError($"Entered GenerateResponse method in DataAssists class. LINE no 251 {result} ");
                    }
                    //await Response.Body.FlushAsync();
                }

                //save the result to db table and return 
                //var ret = await repository.commitDataAssistGeneratedJsonResponse(result, request.ruleId);


                //return new APIResponse<string>
                //{
                //    ResultStatus = APIResultStatus.Failed,
                //    ResponseMessage = new List<string> { "Success" },
                //    Result = result
                //};
                _logger.LogError($"Entered GenerateResponse method in DataAssists class. LINE no 266 {result} ");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Entered GenerateResponse method in DataAssists class. LINE no 251 {result}, Error: {ex.Message} ");
                return ex.Message;                

            }
        }
       
        public async Task<bool> GenerateResponse2(ProjectQueryFormRequest request)
        {
            Dictionary<string, List<Dictionary<string, string>>> responseListCode = new Dictionary<string, List<Dictionary<string, string>>>();
            string result = string.Empty;
            try
            {
                string fileContent = string.Empty;
                if (request.isDataValidation)
                {
                    OpenAIBase openAIreq = new OpenAIBase(_configuration);
                    string CSVData = string.Empty;

                    if (request.isCodeSnippet)
                    {
                        CSVData = GenerateSampleCSVData(request.versionId, request.fileHeaders).Result;
                    }
                    int flowId = request.flowid;
                    int projectId = request.projectId;
                    string userPrompt = request.validationRules;

                    string[] validationRules = await GenerateValidationRulesList(request.versionId, userPrompt);
                    Dictionary<string, List<string>> responseList = new Dictionary<string, List<string>>();

                    foreach (string vrule in validationRules)
                    {
                        int retryCount = 0;
                        int maxRetries = 3;

                        List<string> vrResult = new List<string>();
                        string errorMessage = String.Empty;

                        string promptVR = "I have a CSV file loaded into the pandas DataFrame in python. I want to validate the data based on the following rule: ";
                        promptVR += "\n" + vrule + "\n";
                        promptVR += "\n Following is the sample CSV file to process - create the validation rules based on this : ";
                        promptVR += "\n<CSV_FILE_BEGIN>" + CSVData + "<CSV_FILE_END>\n";
                        promptVR += "\n TASK: Please return a python function for this rule. The python function must take in a Pandas DataFrame as argument. Use Pandas library. The python function must return a list of rows that violate this rule. When comparing numerical type of data, keep only numbers and decimals. Return a JSON object in the following structure: ";
                        promptVR += @"
{
    'rule': 'Short description of the validation rule',
    'function_name': 'Name of the python function',
    'code': 'Python code',
}
RETURN ONLY JSON OBJECT AND NOTHING ELSE

" + errorMessage + @"
";
                        string vrResponseTmp = null;

                        await foreach (var textResp in openAIreq.QueryAI("GPT-o4_http", promptVR, "", "", request.userContext))
                        {
                            vrResponseTmp += textResp;
                        }

                        vrResponseTmp = vrResponseTmp.Replace("`", "");
                        ValidationRule validationRule = JsonConvert.DeserializeObject<ValidationRule>(vrResponseTmp);

                        string ruleName = (string)validationRule.rule;
                        string functionName = (string)validationRule.function_name;
                        string code = (string)validationRule.code;

                        responseListCode.Add(ruleName, new List<Dictionary<string, string>> {
                            new Dictionary<string, string>{
                                { "function_name", functionName },
                                { "code", code}
                            } 
                        });
                    }
                }

                result = "{\"Result\": " + JsonConvert.SerializeObject(responseListCode) + "}";

                //call python code to verify the result
                var tkn = GetBearerToken();
                var res2 = await ValidationRuleServiceHelper.ValidateGeneratedResponse(tkn, result, request.csvDataToTest);

                //var apiResponse = JsonConvert.DeserializeObject<ValidationRuleApiResponse?>(res2);

                if (res2.ResultStatus.Code == APIResultStatus.Completed.Code)
                {
                    
                    if (res2.Result)
                    {
                        //if res2 is true, no error, then lets save the responseListCode

                    }
                }
                return true;
            }
            catch (Exception)
            {

                throw;
            }
        }

        public string GetBearerToken()
        {
            var authorizationHeader = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
            var authorizationHeader1 = httpContextAccessor.HttpContext.Request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith("Bearer "))
            {
                return authorizationHeader.Substring("Bearer ".Length).Trim();
            }

            return null;
        }


        public async Task<string> GenerateSampleCSVData(int versionId, string fileHeaders)
        {
            string userPrompt = String.Empty;

            //fileHeaders is in this format: {"ColumnHeaders":[{"Column":"Date","DataType":"string"},{"Column":"Market","DataType":"string"},{"Column":"Head of Operations","DataType":"string"},{"Column":"Manager","DataType":"string"},{"Column":"Team Leader","DataType":"string"},{"Column":"All Customer Interactions","DataType":"int"},{"Column":"App Customer Interactions","DataType":"int"},{"Column":"Cross Sell Opportunities","DataType":"int"},{"Column":"Cross Sell Attempts","DataType":"int"},{"Column":"Cross Sell Attempts Percentage","DataType":"float"},{"Column":"Conversion Percentage","DataType":"float"}]}

            //get ColumnHeaders and store the array in a var
            var columnHeaders = JsonConvert.DeserializeObject<JObject>(fileHeaders)["ColumnHeaders"].ToObject<List<Dictionary<string, string>>>();
            List<string> columns = new List<string>();
            foreach (var col in columnHeaders)
            {
                columns.Add(col["Column"]);
            }

            var columnHeadersDataTypes = JsonConvert.DeserializeObject<JObject>(fileHeaders)["ColumnHeaders"].ToObject<List<Dictionary<string, string>>>();
            List<string> dataTypes = new List<string>();
            foreach (var col in columnHeadersDataTypes)
            {
                dataTypes.Add(col["DataType"]);
            }

            userPrompt = "Generate a sample CSV data with 10 records with the following column headers: " + string.Join(", ", columns) + ". The data types for the columns are as follows: " + string.Join(", ", dataTypes) + ". Ensure that the data in each column is of the correct data type. For example, if a column is of type 'int', ensure that the values in that column are integers. If a column is of type 'string', ensure that the values in that column are strings. If a column is of type 'float', ensure that the values in that column are floating point numbers. If a column is of type 'date', ensure that the values in that column are dates in the format YYYY-MM-DD. Do not include any additional text or explanation, just provide the CSV data.";

            string systemPrompt = "From the user prompt, identify the column header for a CSV file. based on the header columns, generate a CSV with 10 records and values separated by ','. return nothing else.";

            string response = null;

            OpenAIBase openAIreq = new OpenAIBase(_configuration);
            if (versionId == 1)
            {
                await foreach (var textResp in openAIreq.QueryAI("GPT-4o", userPrompt, systemPrompt, "", ""))
                {
                    response += textResp;
                }
            }
            else if (versionId == 2)
            {
                await foreach (var textResp in openAIreq.QueryAI("GPT-o4", userPrompt, systemPrompt, "", ""))
                {
                    response += textResp;
                }
            }

            string response_csv = response.Replace("`", "");

            return response_csv;
        }

        //private async Task<List<ProjectPrompts>> getPrompt(int flowId, int projectId)
        //{
        //    try
        //    {
        //        var flows = await _db.QueryAsync<ProjectPrompts>(
        //            "GetProjectPromptsByProjectId",
        //            new { projectId, flowId },
        //            commandType: CommandType.StoredProcedure
        //        );

        //        return flows.ToList();
        //    }
        //    catch (System.Exception ex)
        //    {
        //        return null;
        //    }
        //}
        public async Task<string[]> GenerateValidationRulesList(int versionId, string userPrompt)
        {
            string systemPrompt = "From the user prompt, identify the validation rules user is trying to do. " +
                "It may or may not be a pseudocode. " +
                "Make a list of the validation rules. " +
                "If the rules are enumerated in points, make sure to keep each point as a separate rule. " +
                "If there are values for a validation rule defined in variables, make that a part of the validation rule itself. " +
                "If there are additional rules, keep them separate. Do not combine rules. " +
                "Try to keep the original language of the rule. " +
                "Return only the list of validation rules as a comma separated values separated by ';'. Return nothing else.";

            string response_validation_rules = null;

            OpenAIBase openAIreq = new OpenAIBase(_configuration);
            if (versionId == 1)
            {
                await foreach (var textResp in openAIreq.QueryAI("GPT-4o", userPrompt, systemPrompt, "", ""))
                {
                    response_validation_rules += textResp;
                }
            }
            else if (versionId == 2)
            {
                await foreach (var textResp in openAIreq.QueryAI("GPT-o4", userPrompt, systemPrompt, "", ""))
                {
                    response_validation_rules += textResp;
                }
            }

            response_validation_rules = response_validation_rules.Replace("`", "");
            string[] validation_rules = response_validation_rules.Split(";");

            return validation_rules;
        }

        public async Task<List<string>> ValidateDataWithPythonFunction(string fileContent, string ruleName, string functionName, string code)
        {
            try
            {
                List<string> violationsList = new List<string>();
                string error;
                string result;

                string pythonCode = $@"
from io import StringIO
import pandas as pd
import json

{WebUtility.UrlDecode(code)}

myCSVData = '''{fileContent}'''
buffer = StringIO(myCSVData)
df = pd.read_csv(buffer)
violations = {functionName}(df)

if isinstance(violations, list) and violations and len(violations) > 0:
    print('|||||'.join([str(x) for x in violations]))
else:
    print('no_violations')
";
                string escapedPythonCode = pythonCode.Replace("\"", "\\\"");

#if DEBUG
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "C:\\Python313\\python.exe",
                    Arguments = $"-c \"{escapedPythonCode}\"",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = "C:\\Python313"
                };
#endif

#if !DEBUG
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/python3",
                    Arguments = $"-c \"{escapedPythonCode}\"",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = "/app"
                };
#endif

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    result = await process.StandardOutput.ReadToEndAsync();
                    error = await process.StandardError.ReadToEndAsync();
                    process.WaitForExit();
                }

                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"Python Error for rule '{ruleName}': {error}");
                    return new List<string> { "ERROR:" + error };
                }

                result = result.Trim();
                string[] result_split = result.Split(new[] { "|||||" }, StringSplitOptions.None);
                return new List<string>(result_split);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception for rule '{ruleName}': {ex.Message}");
                return new List<string> { "ERROR: " + ex.Message };
            }
        }

    }
}
