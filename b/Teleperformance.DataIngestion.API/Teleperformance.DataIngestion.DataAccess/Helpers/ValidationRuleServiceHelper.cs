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
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.ValidateFileRules;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using static Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus.StatusResponseDto;

namespace Teleperformance.DataIngestion.DataAccess.Helpers
{
    public class ValidationRuleServiceHelper
    {
        ILogger _logger;
        // string apiUrl = "http://127.0.0.1:8000/";
        string apiUrl = KeyVault.GetKeyVaultValue("TPDataIngestionValidationAPIURL").Result;
        public ValidationRuleServiceHelper(ILogger logger)
        {
            _logger = logger;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="uiValidation"></param>
        /// <param name="flpConfigurationId"></param>
        /// <param name="uploadFileId"></param>
        /// <param name="flpProcessTempFileName"></param>
        /// <param name="parquetFilePath"></param>
        /// <param name="flpStorageAccount"></param>
        /// <param name="storageAccountKey"></param>
        /// <param name="storageContainerName"></param>
        /// <param name="tabName"></param>
        /// <returns></returns>

        public async Task<APIResponse<ValidationRuleApiResponse>> ValidateExcelRules(string token, bool uiValidation, string flpConfigurationId, string uploadFileId, string flpProcessTempFileName, string parquetFilePath, string flpStorageAccount, string storageAccountKey, string storageContainerName, string tabName)
        {

            try
            {

                // string apiUrl = "http://127.0.0.1:8000/";
               // string apiUrl = KeyVault.GetKeyVaultValue("TPDataIngestionValidationAPIURL").Result;
                _logger.LogInformation("Validation API URL called: " + apiUrl);
                string bearerToken = token;
                string tokenEncryptionKey = KeyVault.GetKeyVaultValue("TPDataIngestionEncryptionKey").Result;

                bearerToken = Crypto.Encrypt(bearerToken, tokenEncryptionKey);

                string blobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(flpStorageAccount, storageAccountKey);
                //deleting file from location:PHP/PHP/Citibank/parquet/xlsx dedup text file_20250928075755.parquet
                Stream parquetStream = await FlpConfigurationHelperV4_1.GetParquetFileStreamFromBlob(blobConnectionString,
                   storageContainerName, parquetFilePath);

                string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(flpStorageAccount, storageAccountKey);
                string destinationBlobName = $"{flpProcessTempFileName}";

                Stream fileStream = await FlpConfigurationHelperV4_1.GetParquetFileStreamFromBlob(blobConnectionString,
                   storageContainerName, destinationBlobName);


                parquetStream.Position = 0;
                fileStream.Position = 0;


                ValidateRulesDto validateRulesDto = new ValidateRulesDto();
                // validateRulesDto.RuleSet = result.Result.ToList();
                //validateRulesDto.FileTypeId = 1;
                validateRulesDto.FlpConfigurationId = flpConfigurationId;
                validateRulesDto.UploadFileId = uploadFileId;
                validateRulesDto.TabName = tabName;
                validateRulesDto.ParquetFileURL = Uri.UnescapeDataString(parquetFilePath).Replace("\\", "/") ?? "";
                validateRulesDto.UIValidation = uiValidation;

                var content = new MultipartFormDataContent();
                var jsonDto = JsonConvert.SerializeObject(validateRulesDto);
                content.Add(new StringContent(jsonDto, Encoding.UTF8, "application/json"), "validationRuleData");

                // Add Parquet stream
                var parquetContent = new StreamContent(parquetStream);
                parquetContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(parquetContent, "parquetFile", "file.parquet");

                // Add text stream
                //var textContent = new StreamContent(fileStream);
                //textContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                // content.Add(textContent, "textFile", "file.txt");

                // Excel file
                string fileExtension = FlpConfigurationHelper.GetFileExtension(flpProcessTempFileName);
                var excelContent = new StreamContent(fileStream);

                switch (fileExtension.ToLower())
                {
                    case ".xlsx":
                        excelContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                        content.Add(excelContent, "fileContent", "file.xlsx");
                        break;

                    case ".xls":
                        excelContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.ms-excel");
                        content.Add(excelContent, "fileContent", "file.xls");
                        break;

                    case ".xlsb":
                        //excelContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.ms-excel.sheet.binary.macroEnabled.12");
                        //content.Add(excelContent, "fileContent", "file.xlsb");
                        excelContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                        content.Add(excelContent, "fileContent", "file.xlsx");
                        break;
                }
                //excelContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                //content.Add(excelContent, "fileContent", "file.xlsx");



                // var content = new StringContent(data, Encoding.UTF8, "application/json");
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(60);
                    client.BaseAddress = new Uri(apiUrl);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    //client.DefaultRequestHeaders.Add("x-tpdi-api-version", apiVersion);
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + bearerToken);
                    var response = await client.PostAsync($"validate/uploaded-file", content);
                    // Check if the request was successful
                    if (response.IsSuccessStatusCode)
                    {
                        var res = await response.Content.ReadAsStringAsync();
                        string msg = res.ToString();
                        var apiResponse = JsonConvert.DeserializeObject<ValidationRuleApiResponse?>(res);
                        string message = $"Response code - {(int)response.StatusCode}, Reason Pharase  {response.ReasonPhrase} error: {msg}";
                        _logger.LogError($"Validation API error occurred at line No-217: {flpConfigurationId}, {message}");
                        return new APIResponse<ValidationRuleApiResponse>
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
                        string message = $"Response code - {(int)response.StatusCode}, Reason Pharase  {response.ReasonPhrase} error: {msg}";
                        _logger.LogError($"Validation API error occurred at line No-229: {flpConfigurationId}, {message}");
                        return new APIResponse<ValidationRuleApiResponse>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = [$"Internal error occurred:{message} "],
                            Result = null
                        };
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Validation API exception occurred at line No-242: {flpConfigurationId}, {ex.Message.ToString()}");
                // await new DataRepository().LogError($"Function App Error: in GetRunIdsForDeletTempFileUpdateStatusAsync(): {ex.Message} at {DateTime.UtcNow}", "Function App", "Error");
                return new APIResponse<ValidationRuleApiResponse>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = [$"Internal error occurred:{ex.Message.ToString()}"],
                    Result = null
                };
            }
            // return jobStatusResponse;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="uiValidation"></param>
        /// <param name="flpProcessTempFile"></param>
        /// <param name="flpConfigurationRequestDto"></param>
        /// <param name="resultResponse"></param>
        /// <param name="destinationStorageAccountDto"></param>
        /// <returns></returns>
        public async Task<APIResponse<ValidationRuleApiResponse>> ValidateCsvTextRules(string token, bool uiValidation, FlpProcessTempFile flpProcessTempFile, FlpConfigurationResponseDto flpConfigurationRequestDto, ParquetFileResponseDto resultResponse, DestinationStorageAccountDto destinationStorageAccountDto)
        {

            try
            {
                //string apiUrl = "http://127.0.0.1:8000/";
                // string apiUrl = KeyVault.GetKeyVaultValue("TPDataIngestionValidationAPIURL").Result;
                _logger.LogInformation("Validation API URL called: " + apiUrl);
                string bearerToken = token;
                string tokenEncryptionKey = KeyVault.GetKeyVaultValue("TPDataIngestionEncryptionKey").Result;

                bearerToken = Crypto.Encrypt(bearerToken, tokenEncryptionKey);

                string blobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey);
                //deleting file from location:PHP/PHP/Citibank/parquet/xlsx dedup text file_20250928075755.parquet
                Stream parquetStream = await FlpConfigurationHelperV4_1.GetParquetFileStreamFromBlob(blobConnectionString,
                   destinationStorageAccountDto.StorageContainerName, resultResponse.ParquetFilePath);

                string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(destinationStorageAccountDto.FlpStorageAccount, destinationStorageAccountDto.StorageAccountKey);
                string destinationBlobName = $"{flpProcessTempFile.Name}";

                Stream fileStream = await FlpConfigurationHelperV4_1.GetParquetFileStreamFromBlob(blobConnectionString,
                   destinationStorageAccountDto.StorageContainerName, destinationBlobName);


                parquetStream.Position = 0;
                fileStream.Position = 0;


                ValidateRulesDto validateRulesDto = new ValidateRulesDto();
                // validateRulesDto.RuleSet = result.Result.ToList();
                //validateRulesDto.FileTypeId = 1;
                validateRulesDto.FlpConfigurationId = flpConfigurationRequestDto.FlpConfigurationId;
                validateRulesDto.UploadFileId = flpConfigurationRequestDto.UploadedFileId;
                validateRulesDto.TabName = null;
                validateRulesDto.ParquetFileURL = resultResponse.ParquetFilePath;
                validateRulesDto.UIValidation = uiValidation;

                var content = new MultipartFormDataContent();
                var jsonDto = JsonConvert.SerializeObject(validateRulesDto);
                content.Add(new StringContent(jsonDto, Encoding.UTF8, "application/json"), "validationRuleData");

                // Add Parquet stream
                var parquetContent = new StreamContent(parquetStream);
                parquetContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(parquetContent, "parquetFile", "file.parquet");

                // Add text stream
                //var textContent = new StreamContent(fileStream);
                //textContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                // content.Add(textContent, "textFile", "file.txt");

                // CSV file
                var csvContent = new StreamContent(fileStream);
                csvContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
                content.Add(csvContent, "fileContent", "file.csv");



                // var content = new StringContent(data, Encoding.UTF8, "application/json");
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(60);
                    client.BaseAddress = new Uri(apiUrl);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    //client.DefaultRequestHeaders.Add("x-tpdi-api-version", apiVersion);
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + bearerToken);
                    var response = await client.PostAsync($"validate/uploaded-file", content);
                    // Check if the request was successful
                    if (response.IsSuccessStatusCode)
                    {
                        var res = await response.Content.ReadAsStringAsync();
                        var apiResponse = JsonConvert.DeserializeObject<ValidationRuleApiResponse?>(res);
                        //await new DataRepository().LogError($"Response in GetRunIdsForDeletTempFileUpdateStatusAsync(): {res} at {DateTime.UtcNow}", "Function App", "Info");
                        return new APIResponse<ValidationRuleApiResponse>
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
                        string message = $"Response code - {(int)response.StatusCode}, Reason Pharase  {response.ReasonPhrase} error: {msg}";
                        _logger.LogError($"Validation API error occurred at line No-339: {flpConfigurationRequestDto.FlpConfigurationId}, {message}");
                        return new APIResponse<ValidationRuleApiResponse>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = ["Internal error occurred " + message],
                            Result = null
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Validation API exception occurred at line No-352: {flpConfigurationRequestDto.FlpConfigurationId}, {ex.Message.ToString()}");
                // await new DataRepository().LogError($"Function App Error: in GetRunIdsForDeletTempFileUpdateStatusAsync(): {ex.Message} at {DateTime.UtcNow}", "Function App", "Error");
                return new APIResponse<ValidationRuleApiResponse>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = ["Internal error occurred " + ex.Message.ToString()],
                    Result = null
                };
            }
            // return jobStatusResponse;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="flpConfigurationId"></param>
        /// <param name="uploadFileId"></param>
        /// <param name="tabName"></param>
        /// <param name="token"></param>
        /// <param name="destinationPath"></param>
        /// <param name="containerName"></param>
        /// <param name="fileName"></param>
        /// <param name="storageAccount"></param>
        /// <param name="sasKey"></param>
        /// <param name="headerRowIndex"></param>
        /// <param name="dataStartRowIndex"></param>
        /// <param name="overWriteExisting"></param>
        /// <returns></returns>
        public async Task<APIResponse<string>> AddExcelRowNo(string flpConfigurationId, string uploadFileId, string tabName, string token, string destinationPath, string containerName, string fileName, string storageAccount, string sasKey, int headerRowIndex, int dataStartRowIndex, bool overWriteExisting, bool hasHeader, bool skip_empty_rows = true)
        {

            try
            {
                //string apiUrl = "http://127.0.0.1:8000/";
               // string apiUrl = KeyVault.GetKeyVaultValue("TPDataIngestionValidationAPIURL").Result;
                _logger.LogInformation("AddExcelRowNo API URL called: " + apiUrl);
                string bearerToken = token;
                string tokenEncryptionKey = KeyVault.GetKeyVaultValue("TPDataIngestionEncryptionKey").Result;

                bearerToken = Crypto.Encrypt(bearerToken, tokenEncryptionKey);
                var sasKeyArr = sasKey.Split("?");
                sasKey = sasKeyArr.Length > 1 ? sasKeyArr[1] : sasKeyArr[0];
                string destinationBlobPath = $"{fileName}?{sasKey}";
                string sasKeyTokenURL = destinationBlobPath;// FlpConfigurationHelper.GetBlobConnectionStringBySasKey(storageAccount, destinationBlobPath);
                                                            // sasKeyTokenURL = Uri.UnescapeDataString(sasKeyTokenURL).Replace("\\", "/") ?? "";

                string fileTypeExtension = Path.GetExtension(fileName).ToLower();
                var requestBody = new
                {
                    sas_url = sasKeyTokenURL,
                    sheet_name = tabName,
                    header_row_index = headerRowIndex,
                    data_start_row_index = dataStartRowIndex,
                    overwrite_existing = false,
                    has_header = hasHeader,
                    file_type_extention = fileTypeExtension,
                    skip_empty_rows = skip_empty_rows
                };
                var data = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(data, Encoding.UTF8, "application/json");
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(60);
                    client.BaseAddress = new Uri(apiUrl);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    //client.DefaultRequestHeaders.Add("x-tpdi-api-version", apiVersion);
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + bearerToken);
                    var response = await client.PostAsync($"files/excel/add-rowno", content);
                    // Check if the request was successful
                    if (response.IsSuccessStatusCode)
                    {
                        // var res = await response.Content.ReadAsStringAsync();
                        var res = await response.Content.ReadAsStringAsync();
                        string msg = res.ToString();
                        string message = $"Response code:{(int)response.StatusCode}, Reason Phrase:{response.ReasonPhrase},success:{msg}";
                        _logger.LogInformation($"AddExcelRowNo Success: {message} , for {flpConfigurationId}, {uploadFileId}");
                        return new APIResponse<string>
                        {
                            ResultStatus = APIResultStatus.Completed,
                            ResponseMessage = ["Successfully."],
                            Result = message
                        };
                    }
                    else
                    {
                        var res = await response.Content.ReadAsStringAsync();
                        string msg = res.ToString();
                        string message = $"Response code - {(int)response.StatusCode}, Reason Phrase  {response.ReasonPhrase} error: {msg}";
                        _logger.LogError($"AddExcelRowNo Error: {message} , for {flpConfigurationId}, {uploadFileId}");
                        return new APIResponse<string>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = ["Internal error occurred " + message],
                            Result = null
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"AddExcelRowNo Exception Occurred: {ex} , for {flpConfigurationId}, {uploadFileId}");
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = ["Internal error occurred " + ex.Message.ToString()],
                    Result = null
                };
            }


        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="flpConfigurationId"></param>
        /// <param name="uploadFileId"></param>
        /// <param name="delimiter"></param>
        /// <param name="token"></param>
        /// <param name="destinationPath"></param>
        /// <param name="containerName"></param>
        /// <param name="fileName"></param>
        /// <param name="storageAccount"></param>
        /// <param name="sasKey"></param>
        /// <param name="headerRowIndex"></param>
        /// <param name="dataStartRowIndex"></param>
        /// <param name="overWriteExisting"></param>
        /// <returns></returns>
        public async Task<APIResponse<string>> AddCsvRowNo(string flpConfigurationId, string uploadFileId, string delimiter, string token, string destinationPath, string containerName, string fileName, string storageAccount, string sasKey, int headerRowIndex, int dataStartRowIndex, bool overWriteExisting,bool hasHeader,bool skip_empty_rows = true)
        {

            try
            {
                //string apiUrl = "http://127.0.0.1:8000/";
                //string apiUrl = KeyVault.GetKeyVaultValue("TPDataIngestionValidationAPIURL").Result;
                _logger.LogInformation("AddCsvRowNo API URL called: " + apiUrl);
                string bearerToken = token;
                string tokenEncryptionKey = KeyVault.GetKeyVaultValue("TPDataIngestionEncryptionKey").Result;

                bearerToken = Crypto.Encrypt(bearerToken, tokenEncryptionKey);
                var sasKeyArr = sasKey.Split("?");
                sasKey = sasKeyArr.Length > 1 ? sasKeyArr[1] : sasKeyArr[0];
                //sasKey = $"sv=2023-01-03&st=2026-04-14T08%3A54%3A51Z&se=2027-12-15T08%3A54%3A00Z&sr=c&sp=racwdxl&sig=fN8GwbrmRE%2BhZuduebtPfFd%2BttAjvv%2BhQ3NjG8V6Czo%3D";
                string destinationBlobPath = $"{fileName}?{sasKey}";
              
                var requestBody = new
                {
                    sas_url = destinationBlobPath,
                    header_row_index = headerRowIndex,
                    data_start_row_index = dataStartRowIndex,
                    overwrite_existing = false,
                    delimeter = delimiter,
                    has_header = hasHeader,
                    skip_empty_rows = skip_empty_rows

                };
                var data = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(data, Encoding.UTF8, "application/json");
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(60);
                    client.BaseAddress = new Uri(apiUrl);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    //client.DefaultRequestHeaders.Add("x-tpdi-api-version", apiVersion);
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + bearerToken);
                    var response = await client.PostAsync($"files/csv/add-rowno", content);
                    // Check if the request was successful
                    if (response.IsSuccessStatusCode)
                    {
                        // var res = await response.Content.ReadAsStringAsync();
                        var res = await response.Content.ReadAsStringAsync();
                        string msg = res.ToString();
                        string message = $"Response code:{(int)response.StatusCode}, Reason Phrase:{response.ReasonPhrase},success:{msg}";
                        _logger.LogInformation($"AddCsvRowNo Success: {message} , for {flpConfigurationId}, {uploadFileId}");
                        return new APIResponse<string>
                        {
                            ResultStatus = APIResultStatus.Completed,
                            ResponseMessage = ["Successfully."],
                            Result = message
                        };
                    }
                    else
                    {
                        var res = await response.Content.ReadAsStringAsync();
                        string msg = res.ToString();
                        string message = $"Response code - {(int)response.StatusCode}, Reason Phrase  {response.ReasonPhrase} error: {msg}";
                        _logger.LogError($"AddCsvRowNo Error: {message} , for {flpConfigurationId}, {uploadFileId}");
                        return new APIResponse<string>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = ["Internal error occurred " + message],
                            Result = null
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"AddCsvRowNo Exception Occurred: {ex} , for {flpConfigurationId}, {uploadFileId}");
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = ["Internal error occurred " + ex.Message.ToString()],
                    Result = null
                };
            }


        }




        /// <summary>
        /// /
        /// </summary>
        /// <param name="flpConfigurationId"></param>
        /// <param name="uploadFileId"></param>
        /// <param name="delimiter"></param>
        /// <param name="token"></param>
        /// <param name="destinationPath"></param>
        /// <param name="containerName"></param>
        /// <param name="fileName"></param>
        /// <param name="storageAccount"></param>
        /// <param name="sasKey"></param>
        /// <param name="headerRowIndex"></param>
        /// <param name="dataStartRowIndex"></param>
        /// <param name="overWriteExisting"></param>
        /// <returns></returns>
        public async Task<APIResponse<string>> AddTextRowNo(string flpConfigurationId, string uploadFileId, string delimiter, string token, string destinationPath, string containerName, string fileName, string storageAccount, string sasKey, int headerRowIndex, int dataStartRowIndex, bool overWriteExisting, bool hasHeader, bool skip_empty_rows = true)
        {

            try
            {
                //string apiUrl = "http://127.0.0.1:8000/";
                //string apiUrl = KeyVault.GetKeyVaultValue("TPDataIngestionValidationAPIURL").Result;
                _logger.LogInformation("AddTextRowNo API URL called: " + apiUrl);
                string bearerToken = token;
                string tokenEncryptionKey = KeyVault.GetKeyVaultValue("TPDataIngestionEncryptionKey").Result;

                bearerToken = Crypto.Encrypt(bearerToken, tokenEncryptionKey);
                var sasKeyArr = sasKey.Split("?");
                sasKey = sasKeyArr.Length > 1 ? sasKeyArr[1] : sasKeyArr[0];
                string destinationBlobPath = $"{fileName}?{sasKey}";
                string sasKeyTokenURL = destinationBlobPath;// FlpConfigurationHelper.GetBlobConnectionStringBySasKey(storageAccount, destinationBlobPath);
                                                            // sasKeyTokenURL = Uri.UnescapeDataString(sasKeyTokenURL).Replace("\\", "/") ?? "";
                var requestBody = new
                {
                    sas_url = sasKeyTokenURL,
                    header_row_index = headerRowIndex,
                    data_start_row_index = dataStartRowIndex,
                    overwrite_existing = false,
                    delimeter = delimiter,
                    has_header = hasHeader,
                    skip_empty_rows = skip_empty_rows
                };
                var data = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(data, Encoding.UTF8, "application/json");
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(60);
                    client.BaseAddress = new Uri(apiUrl);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    //client.DefaultRequestHeaders.Add("x-tpdi-api-version", apiVersion);
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + bearerToken);
                    var response = await client.PostAsync($"files/test/add-rowno", content);
                    // Check if the request was successful
                    if (response.IsSuccessStatusCode)
                    {
                        // var res = await response.Content.ReadAsStringAsync();
                        var res = await response.Content.ReadAsStringAsync();
                        string msg = res.ToString();
                        string message = $"Response code:{(int)response.StatusCode}, Reason Phrase:{response.ReasonPhrase},success:{msg}";
                        _logger.LogInformation($"AddTextRowNo Success: {message} , for {flpConfigurationId}, {uploadFileId}");
                        return new APIResponse<string>
                        {
                            ResultStatus = APIResultStatus.Completed,
                            ResponseMessage = ["Successfully."],
                            Result = message
                        };
                    }
                    else
                    {
                        var res = await response.Content.ReadAsStringAsync();
                        string msg = res.ToString();
                        string message = $"Response code - {(int)response.StatusCode}, Reason Phrase  {response.ReasonPhrase} error: {msg}";
                        _logger.LogError($"AddTextRowNo Error: {message} , for {flpConfigurationId}, {uploadFileId}");
                        return new APIResponse<string>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = ["Internal error occurred " + message],
                            Result = null
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"AddCsvRowNo Exception Occurred: {ex} , for {flpConfigurationId}, {uploadFileId}");
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = ["Internal error occurred " + ex.Message.ToString()],
                    Result = null
                };
            }


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="validationRule">generated python code</param>
        /// <returns></returns>
        public async static Task<APIResponse<bool>> ValidateGeneratedResponse(string token, string validationRule, string csvDataToValidate)
        {
            try
            {

                string apiUrl1 = KeyVault.GetKeyVaultValue("TPDataIngestionValidationAPIURL").Result;
                string bearerToken = token;
                string tokenEncryptionKey = KeyVault.GetKeyVaultValue("TPDataIngestionEncryptionKey").Result;

                bearerToken = Crypto.Encrypt(bearerToken, tokenEncryptionKey);
                var content = new MultipartFormDataContent();
                var jsonDto = JsonConvert.SerializeObject(validationRule);
                content.Add(new StringContent(jsonDto, Encoding.UTF8, "application/json"), "validationRuleData");

                content.Add(new StringContent(csvDataToValidate, Encoding.UTF8, "application/json"), "api_data");

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(60);
                    client.BaseAddress = new Uri(apiUrl1);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    //client.DefaultRequestHeaders.Add("x-tpdi-api-version", apiVersion);
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + bearerToken);
                    var response = await client.PostAsync($"validate/validateGeneratedResponse", content);
                    // Check if the request was successful
                    if (response.IsSuccessStatusCode)
                    {
                        var res = await response.Content.ReadAsStringAsync();
                        var apiResponse = JsonConvert.DeserializeObject<ValidationRuleApiResponse?>(res);
                        //await new DataRepository().LogError($"Response in GetRunIdsForDeletTempFileUpdateStatusAsync(): {res} at {DateTime.UtcNow}", "Function App", "Info");
                        if (apiResponse != null)
                        {

                            return new APIResponse<bool>
                            {
                                ResultStatus = APIResultStatus.Completed,
                                ResponseMessage = new List<string> { "Successfully." },
                                Result = apiResponse.FailedRows > 0
                            };

                        }

                        return new APIResponse<bool>
                        {
                            ResultStatus = APIResultStatus.Completed,
                            ResponseMessage = ["Successfully."],
                            Result = false //todo
                        };
                    }
                    else
                    {
                        var res = await response.Content.ReadAsStringAsync();
                        string msg = res.ToString();
                        string message = $"Response code - {(int)response.StatusCode}, Reason Pharase  {response.ReasonPhrase} error: {msg}";
                        // await new DataRepository().LogError($"Function App Error in GetRunIdsForDeletTempFileUpdateStatusAsync(): {message} at {DateTime.UtcNow}", "Function App", "Error");
                        return new APIResponse<bool>
                        {
                            ResultStatus = APIResultStatus.Error,
                            ResponseMessage = ["Internal error occurred"],
                            Result = false
                        };
                    }

                }
            }
            catch (Exception)
            {


                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = ["Internal Error Occured."],
                    Result = true
                }; ;
            }
        }
    }
}
