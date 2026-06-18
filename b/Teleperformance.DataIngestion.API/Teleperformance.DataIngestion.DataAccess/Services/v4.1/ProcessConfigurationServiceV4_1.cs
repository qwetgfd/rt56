using Azure.Storage.Blobs;
using DocumentFormat.OpenXml.Office2010.Excel;
using Irony.Parsing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using NPOI.POIFS.Crypt;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Transactions;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.Common.Crypto;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.DataProfilling;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.LandingLayer;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v4._1;
using Teleperformance.DataIngestion.Models.Entities.v4._1.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Enums.v3._0;
using Teleperformance.DataIngestion.Models.Models.v4._1;
using static Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus.StatusResponseDto;
using IAdminRepository = Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0.IAdminRepository;
using IBlobStorageService = Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0.IBlobStorageService;
using IHeaderService = Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0.IHeaderService;
using IProcessConfigRepositoryV4 = Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0.IProcessConfigRepositoryV4;

namespace Teleperformance.DataIngestion.DataAccess.Services.v4._1
{
    public class ProcessConfigurationServiceV4_1 : IProcessConfigurationServiceV4_1
    {
        private readonly IProcessConfigurationRepositoryV4_1 _processConfigurationRepository;
        private readonly IProcessConfigRepositoryV4 _processConfigRepositoryV4;
        private readonly IAdminRepository _adminRepository;
        private readonly IBlobStorageService _blobStorageService;
        private readonly ILogger<ProcessConfigurationServiceV4_1> _logger;
        private readonly IHeaderService _headerService;
        private readonly IBlobStorageServiceV4_1 _iBlobStorageService;
        private readonly IBackgroundTaskQueue _backgroundTaskQueue;
        private readonly IHttpContextAccessor _httpContextAccessor;
        //  private readonly ILandingLayerService _landingLayerService;
        public ProcessConfigurationServiceV4_1(
            IProcessConfigurationRepositoryV4_1 processConfigurationRepository, IProcessConfigRepositoryV4 processConfigRepositoryV4,
            ILogger<ProcessConfigurationServiceV4_1> logger, IAdminRepository adminRepository, IBlobStorageService blobStorageService,
            IHeaderService headerService, IBlobStorageServiceV4_1 iBlobStorageService,
            IBackgroundTaskQueue backgroundTaskQueue, IHttpContextAccessor httpContextAccessor)
        {
            _processConfigurationRepository = processConfigurationRepository;
            _processConfigRepositoryV4 = processConfigRepositoryV4;
            _logger = logger;
            _adminRepository = adminRepository;
            _blobStorageService = blobStorageService;
            _headerService = headerService;
            _iBlobStorageService = iBlobStorageService;
            _backgroundTaskQueue = backgroundTaskQueue;
            _httpContextAccessor = httpContextAccessor;
        }
        public async Task<APIResponse<ConfigurationResponseDto>> InsertConfiguration(string json, IFormFile file, Stream stream, string loggedInUser, string userName)
        {
            Models.DTOs.v1._0.FileConfiguration.FileValueRequest pagePartValueAPIRequest = new Models.DTOs.v1._0.FileConfiguration.FileValueRequest();


            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogError("Error: Configuration json not found.");
                return new APIResponse<ConfigurationResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Configuration json not found." },
                    Result = null
                };
            }

            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");

            if (string.IsNullOrWhiteSpace(securityGroupId))
            {
                _logger.LogError("Error: Security group not found.");
                return new APIResponse<ConfigurationResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Security group not found." },
                    Result = null
                };
            }

            var jsonData = JsonSerializer.Deserialize<FileConfigurationDto>(json);


            if (jsonData.processSettings != null && jsonData.fileSettings.Count() > 0)
            {


                if (string.IsNullOrWhiteSpace(jsonData.processSettings.processName))
                {
                    _logger.LogError("Error: ProcessName is null.");
                    return new APIResponse<ConfigurationResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { "Error: ProcessName is null." },
                        Result = null
                    };
                }

                var processConfiguration = new ProcessSettingEntity
                {
                    flpConfigurationId = jsonData.processSettings.flpConfigurationId,
                    process_name = jsonData.processSettings.processName,
                    is_active = true,// jsonData.processSettings.is_active,
                    sender_communication_email = jsonData.processSettings.sender_communication_email,
                    created_by = loggedInUser,
                    created_date = DateTime.UtcNow,
                    loginid = loggedInUser,
                    description = jsonData.processSettings.description,
                    regionId = jsonData.processSettings.RegionId,
                    subRegionId = jsonData.processSettings.SubRegionId,
                    clientId = jsonData.processSettings.ClientId,
                    userName = userName,
                    securityGroupId = securityGroupId,
                    securityGroups = jsonData.processSettings.securityGroups,
                    region = jsonData.processSettings.region,
                    subRegion = jsonData.processSettings.subRegion,
                    clientName = jsonData.processSettings.clientName,
                    dataSource = jsonData.processSettings.dataSource,
                    multisheet = jsonData.processSettings.multisheet,
                    sheetReferenceByIndex = jsonData.processSettings.sheetReferenceByIndex,
                    internalCampaignId = jsonData.fileSettings[0].additionalSettings.internalCampaignId

                };
                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {


                        //upsert security group table

                        foreach (Models.Entities.v2._0.ProcessConfiguration.SecurityGroup securityGroup in jsonData.processSettings.securityGroups)
                        {
                            var retVal = await _processConfigRepositoryV4.AddSecurityGroup(securityGroup);

                        }

                        if (string.IsNullOrEmpty(jsonData.processSettings.flpConfigurationId))
                        {
                            processConfiguration.flpConfigurationId = Guid.NewGuid().ToString();
                            var retVal = await _processConfigurationRepository.InsertProcessConfiguration(processConfiguration);

                            foreach (var fileSetting in jsonData.fileSettings)
                            {
                                var fileConfiguration = new FileSettingEntity
                                {
                                    flpConfigurationId = processConfiguration.flpConfigurationId,
                                    tabName = fileSetting.tabName,
                                    ignoreSheet = fileSetting.ignoreSheet,
                                    delimiter = fileSetting.additionalSettings.delimiter,
                                    flexCheckHasHeaders = fileSetting.additionalSettings.flexCheckHasHeaders,
                                    skip_header_rows = fileSetting.additionalSettings.skip_header_rows,
                                    skip_footer_rows = fileSetting.additionalSettings.skip_footer_rows,
                                    txtQuoteCharacter = fileSetting.additionalSettings.txtQuoteCharacter,
                                    key_column_list = String.Join(",", fileSetting.columnNameDatatypeNames.Where(w => w.ColumnKey == true).Select(x => x.ColumnName)),
                                    column_name_list = fileSetting.additionalSettings.column_name_list,
                                    convert_datatypes_column_list = CreateColumns(fileSetting.columnNameDatatypeNames),
                                    loginid = loggedInUser,
                                    order_by_column_list_for_dedup = fileSetting.additionalSettings.order_by_column_list_for_dedup,
                                    ignore_duplicate_rows = fileSetting.additionalSettings.ignore_duplicate_rows,
                                    do_not_archive_file = fileSetting.additionalSettings.do_not_archive_file,
                                    keep_first_row = fileSetting.additionalSettings.keep_first_row,
                                    spanish_to_english = fileSetting.additionalSettings.spanish_to_english,
                                    roman_numerals_only = fileSetting.additionalSettings.roman_numerals_only,
                                    flexCheckSkipEmptyLines = fileSetting.additionalSettings.flexCheckSkipEmptyLines
                                };
                                var fileConfigTask = await _processConfigurationRepository.InsertProcessFileConfiguration(fileConfiguration);

                                if (fileSetting.ignoreSheet)
                                {
                                    fileSetting.databaseSettings.deltaStorageAccountId = null;
                                    fileSetting.databaseSettings.databaseConfigurationId = null;
                                    fileSetting.databaseSettings.deltaServerNameId = null;
                                }

                                var databaseConfiguration = new DatabaseSettingEntity
                                {
                                    flpConfigurationId = processConfiguration.flpConfigurationId,
                                    tabName = fileSetting.tabName,
                                    ignoreSheet = fileSetting.ignoreSheet,
                                    process_name = processConfiguration.process_name,
                                    db_file_columnName_list = CreateFileDBColumnDataTypeMapping(fileSetting.columnNameDatatypeNames),
                                    table_name = processConfiguration.dataSource == (int)FileProcessingServerType.SQLServer ? fileSetting.databaseSettings.tableName : fileSetting.databaseSettings.deltaTableName,
                                    loginid = loggedInUser,
                                    drop_main_table = fileSetting.databaseSettings.drop_main_table,
                                    drop_history_table = fileSetting.databaseSettings.drop_history_table,
                                    validate_fileschema = fileSetting.databaseSettings.validate_fileschema,
                                    databaseConfigurationId = processConfiguration.dataSource == (int)FileProcessingServerType.SQLServer ? fileSetting.databaseSettings.databaseConfigurationId : fileSetting.databaseSettings.deltaServerNameId?.ToString(),
                                    mergeData = fileSetting.databaseSettings.mergeData,
                                    createHistoryTable = fileSetting.databaseSettings.createHistoryTable,
                                    dataSource = processConfiguration.dataSource,
                                    deltaStorageAccountId = fileSetting.databaseSettings.deltaStorageAccountId,
                                    deltaContainerName = fileSetting.databaseSettings.deltaContainerName,
                                    deltaSource = fileSetting.databaseSettings.deltaSource,
                                    deltaJobId = fileSetting.databaseSettings.deltaJobId
                                };

                                var databaseConfigResult = await _processConfigurationRepository.InsertProcessDatabaseConfiguration(databaseConfiguration);


                                foreach (var rule in fileSetting.ruleSet)
                                {
                                    var ruleSet = new RuleSetEntity
                                    {

                                        id = rule.id,
                                        flpConfigurationId = processConfiguration.flpConfigurationId,
                                        ruleSetNameId = rule.ruleSetNameId,
                                        ruleSetName = rule.ruleSetName,
                                        ruleTypeId = rule.ruleTypeId,
                                        subRuleId = rule.subRuleId,
                                        ruleColumnName = rule.ruleColumnName,
                                        ruleColumnName2 = rule.ruleColumnName2,
                                        ruleDescription = rule.ruleDescription,
                                        prompt = rule.prompt,
                                        format = rule.format,
                                        patternId = rule.patternId,
                                        isCombinationRule = rule.isCombinationRule,
                                        isActive = true,
                                        isGlobal = rule.isGlobal,
                                        ruleSetType = rule.ruleSetType,
                                        conditionId = rule.conditionId,
                                        fromValue = rule.fromValue,
                                        toValue = rule.toValue,
                                        isAllowNullOrSpace = rule.isAllowNullOrEmptySpaces,
                                        spNameId = rule.spNameId

                                    };

                                    var rulSetResult = await _processConfigurationRepository
                                        .InsertProcessRuleSet(ruleSet, fileSetting.tabName, string.Join(",", jsonData.processSettings.securityGroups.Select(sg => sg.securityGroupId)), loggedInUser, userName, "");
                                }


                            }

                        }
                        else
                        {
                            foreach (var fileSetting in jsonData.fileSettings)
                            {
                                //update the convert_datatypes_column_list and column_name_list
                                //Update only which are not ignoredheet
                                if (!fileSetting.ignoreSheet)
                                {
                                    var retFromDb = await _processConfigurationRepository.UpdateColumnNameList(processConfiguration.flpConfigurationId, fileSetting.tabName, fileSetting.additionalSettings.column_name_list);
                                    retFromDb = await _processConfigurationRepository.UpdateConvertDatatypesColumnList(processConfiguration.flpConfigurationId, fileSetting.tabName, CreateColumns(fileSetting.columnNameDatatypeNames));
                                    retFromDb = await _processConfigurationRepository.UpdateFlpFileColumnMapping(processConfiguration.flpConfigurationId, processConfiguration.process_name, fileSetting.tabName, fileSetting.databaseSettings.tableName, CreateFileDBColumnDataTypeMapping(fileSetting.columnNameDatatypeNames));
                                }

                            }

                        }
                        scope.Complete();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inserting configuration: {Message}", ex.Message);
                        return new APIResponse<ConfigurationResponseDto>
                        {
                            ResultStatus = APIResultStatus.Failed,
                            ResponseMessage = new List<string> { "Error inserting configuration." },
                            Result = null
                        };
                    }
                }


                if (file.Length > 0)
                {
                    string fileName = string.Empty;
                    string destinationContainerName = "";
                    string returnUri = string.Empty;
                    try
                    {
                       
                        if (file.FileName.EndsWith("parquet"))
                        {
                            fileName = $"{processConfiguration.process_name}/parquet/{file.FileName.Trim()}";
                        }
                        else if (file.FileName.EndsWith("csv") || file.FileName.EndsWith("txt") || file.FileName.EndsWith("xls") || file.FileName.EndsWith("xlsx") || file.FileName.EndsWith("xlsb"))
                        {
                            fileName = $"{processConfiguration.process_name}/csv_files/{file.FileName.Trim()}";
                        }
                        string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(KeyVault.GetKeyVaultValue("TPTempAzureStorageAccountNameFileUpload").Result, KeyVault.GetKeyVaultValue("TPTempAzureStorageAccountKeyFileUpload").Result);

                        destinationContainerName = await _adminRepository.GetContainerName(); //destinationStorageAccountDto.StorageContainerName;
                        string destinationBlobUrl = fileName;// $"{flpConfigurationRequestDto.DestinationPath}temp/{currentTimeString}/";
                                                             //string destinationBlobName = $"{destinationBlobUrl}{backUpFileName}";

                        BlobClient destinationBlobClient = _blobStorageService.GetBlobClientDetails(destinationBlobUrl, destinationBlobConnectionString, destinationContainerName);

                        //validate file then will upload 
                        var result = await UploadFile(file, processConfiguration.process_name, stream, destinationBlobClient, fileName, processConfiguration.loginid, processConfiguration.flpConfigurationId);

                        LogUploadedFileRequest logUploadedFileRequest = new LogUploadedFileRequest
                        {
                            fileName = file.FileName,
                            fileSize = file.Length,
                            uploadedDateTime = DateTime.UtcNow.ToString(),
                            uploadedBy = userName,
                            flpConfigurationId = processConfiguration.flpConfigurationId,
                            uploadFileId = result.Result.Item2?.UploadFileId,
                            securityGroupId = securityGroupId
                        };
                        var res2 = await LogUploadedFile(logUploadedFileRequest);
                        if (result != null && result.ResponseCode == APIResultStatus.Completed.Code)// && !string.IsNullOrEmpty(result.Result.Item1))
                        {
                            returnUri = result.Result.Item1;
                            pagePartValueAPIRequest = result.Result.Item2;
                        }
                        else
                        {
                            _logger.LogError("Error: Unable to upload file.");
                            return new APIResponse<ConfigurationResponseDto>
                            {
                                ResultStatus = APIResultStatus.Failed,
                                ResponseMessage = new List<string> { "Unable to upload file." },
                                Result = null
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        this._logger.LogError(ex, ex.Message);
                        //if there's an upload
                        return new APIResponse<ConfigurationResponseDto>
                        {
                            ResultStatus = APIResultStatus.Failed,
                            ResponseMessage = new List<string> { "Invalid File." },
                            Result = new ConfigurationResponseDto()
                            {
                                ProcessName = processConfiguration.process_name,
                                FlpConfigurationId = processConfiguration.flpConfigurationId,
                                BlobClients = new BlobClientDetails()
                                {
                                    Uri = "",
                                    AccountName = "",
                                    BlobContainerName = "",
                                    CanGenerateSasUri = true,
                                    Name = "",
                                    UploadedId = pagePartValueAPIRequest.UploadFileId
                                }
                            }
                        };
                        throw;
                    }

                    //var returnModel = new FlpConvertParquetRequestDto()
                    //{
                    //    ProcessName = csvConfiguration.process_name,
                    //    FlpConfigurationId = csvConfiguration.flpConfigurationId,
                    //    BlobClients = new BlobClientDetails()
                    //    {
                    //        Uri = flpProcessTempFile.Uri,
                    //        AccountName = "tpusadevelopmenttest", //todo: hardcode as of the moment
                    //        BlobContainerName = destinationContainerName,
                    //        CanGenerateSasUri = true,
                    //        Name = flpProcessTempFile.Uri.Substring(flpProcessTempFile.Uri.IndexOf(csvConfiguration.process_name)),
                    //        UploadedId = pagePartValueAPIRequest.UploadFileId
                    //    }
                    //};
                    if (!string.IsNullOrWhiteSpace(pagePartValueAPIRequest.UploadFileId) && (file.FileName.EndsWith("xls") || file.FileName.EndsWith("xlsx") || file.FileName.EndsWith("xlsb")))
                    {
                        foreach (var fileSetting in jsonData.fileSettings)
                        {
                            if (!fileSetting.ignoreSheet && !string.IsNullOrWhiteSpace(fileSetting.tabName))
                            {
                                var retFromDb = await _processConfigurationRepository.UpdateProcessTabName(securityGroupId, processConfiguration.flpConfigurationId, pagePartValueAPIRequest.UploadFileId, fileSetting.tabName.Trim());
                            }

                        }
                    }


                    return new APIResponse<ConfigurationResponseDto>
                    {
                        Result = new ConfigurationResponseDto()
                        {
                            ProcessName = processConfiguration.process_name,
                            FlpConfigurationId = processConfiguration.flpConfigurationId,
                            BlobClients = new BlobClientDetails()
                            {
                                Uri = returnUri,
                                AccountName = "tpusadevelopmenttest", //todo: hardcode as of the moment
                                BlobContainerName = destinationContainerName,
                                CanGenerateSasUri = true,
                                Name = returnUri.Substring(returnUri.IndexOf(processConfiguration.process_name)),
                                UploadedId = pagePartValueAPIRequest.UploadFileId
                            }
                        },
                        ResponseMessage = new List<string> { "Success" },
                        ResultStatus = APIResultStatus.Completed
                    };
                }
                else
                {
                    _logger.LogError("Error: Invalid File.");
                    return new APIResponse<ConfigurationResponseDto>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "Invalid File." },
                        Result = null
                    };
                }

            }
            else
            {
                _logger.LogError("Error: Additional settings not provided.");

                return new APIResponse<ConfigurationResponseDto>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Additional settings not provided." },
                    Result = null
                };
            }
        }


        public async Task<APIResponse<(string, Models.DTOs.v1._0.FileConfiguration.FileValueRequest)>> UploadFile(IFormFile file, string processName, Stream stream, BlobClient destinationBlobClient, string fileName, string loginId, string flpConfigurationId)
        {
            string fileURL = "";
            bool status = false;
            string containerName = "";
            string message = "Unable to store the file.";
            int retUploadFileId = 0;
            Models.DTOs.v1._0.FileConfiguration.FileValueRequest pagePartValueAPIRequest = new Models.DTOs.v1._0.FileConfiguration.FileValueRequest();
            bool isCopiedFile = false;
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();

            if (file.Length > 0)
            {

                try
                {

                    string ext = Path.GetExtension(fileName).ToLower();

                    (isCopiedFile, flpProcessTempFile) = await new BlobStorageService().CopyFileStreamToDestinationBlobAsync(stream, destinationBlobClient);//sourceBlobClient, destinationBlobClient);

                    pagePartValueAPIRequest = new Models.DTOs.v1._0.FileConfiguration.FileValueRequest
                    {
                        UploadFileId = Guid.NewGuid().ToString(),
                        FileName = file.FileName.Trim(),
                        AddedBy = loginId,
                        DateTimeUploaded = DateTime.UtcNow.ToString(),
                        FlpConfigurationId = flpConfigurationId,
                        FlpProceeAttempt = "0",
                        FlpProcessStatusId = "0"
                    };

                    retUploadFileId = await _adminRepository.InsertFile(pagePartValueAPIRequest);

                   

                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, ex.Message);
                    //if there's an upload
                    return new APIResponse<(string, Models.DTOs.v1._0.FileConfiguration.FileValueRequest)>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "Invalid File." },
                        Result = (string.Empty, pagePartValueAPIRequest)
                    };
                    throw;
                }

                return new APIResponse<(string, Models.DTOs.v1._0.FileConfiguration.FileValueRequest)>
                {
                    Result = (flpProcessTempFile.Uri, pagePartValueAPIRequest),
                    ResponseMessage = new List<string> { "Success" },
                    ResultStatus = APIResultStatus.Completed
                };
            }
            else
            {
                _logger.LogError("Error: Invalid File.");
                return new APIResponse<(string, Models.DTOs.v1._0.FileConfiguration.FileValueRequest)>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Invalid File." },
                    Result = (string.Empty, pagePartValueAPIRequest)
                };
            }
        }

        public async Task<APIResponse<(string, Models.DTOs.v1._0.FileConfiguration.FileValueRequest)>> LandingLayerAddDataInUploadTable(string processName, string fileName, string loginId, string flpConfigurationId)
        {

            int retUploadFileId = 0;
            Models.DTOs.v1._0.FileConfiguration.FileValueRequest pagePartValueAPIRequest = new Models.DTOs.v1._0.FileConfiguration.FileValueRequest();
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();

            try
            {

                //string ext = Path.GetExtension(fileName).ToLower();
                //Not required here 
                // (isCopiedFile, flpProcessTempFile) = await new BlobStorageService().CopyFileStreamToDestinationBlobAsync(stream, destinationBlobClient);//sourceBlobClient, destinationBlobClient);

                pagePartValueAPIRequest = new Models.DTOs.v1._0.FileConfiguration.FileValueRequest
                {
                    UploadFileId = Guid.NewGuid().ToString(),
                    FileName = fileName,
                    AddedBy = loginId,
                    DateTimeUploaded = DateTime.UtcNow.ToString(),
                    FlpConfigurationId = flpConfigurationId,
                    FlpProceeAttempt = "0",
                    FlpProcessStatusId = "0"
                };
                retUploadFileId = await _adminRepository.InsertFile(pagePartValueAPIRequest);

            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, ex.Message);
                //if there's an upload
                return new APIResponse<(string, Models.DTOs.v1._0.FileConfiguration.FileValueRequest)>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Invalid File." },
                    Result = (string.Empty, pagePartValueAPIRequest)
                };
                throw;
            }

            return new APIResponse<(string, Models.DTOs.v1._0.FileConfiguration.FileValueRequest)>
            {
                Result = (flpProcessTempFile.Uri, pagePartValueAPIRequest),
                ResponseMessage = new List<string> { "Success" },
                ResultStatus = APIResultStatus.Completed
            };
        }

        //public async Task<APIResponse<(string, Models.DTOs.v1._0.FileConfiguration.FileValueRequest)>> LandingLayerAddDataInUploadTable(IFormFile file, string processName, string fileName, string loginId, string flpConfigurationId)
        //{

        //    int retUploadFileId = 0;
        //    Models.DTOs.v1._0.FileConfiguration.FileValueRequest pagePartValueAPIRequest = new Models.DTOs.v1._0.FileConfiguration.FileValueRequest();
        //    FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();

        //    if (file.Length > 0)
        //    {

        //        try
        //        {

        //            string ext = Path.GetExtension(fileName).ToLower();
        //            //Not required here 
        //            // (isCopiedFile, flpProcessTempFile) = await new BlobStorageService().CopyFileStreamToDestinationBlobAsync(stream, destinationBlobClient);//sourceBlobClient, destinationBlobClient);

        //            pagePartValueAPIRequest = new Models.DTOs.v1._0.FileConfiguration.FileValueRequest
        //            {
        //                UploadFileId = Guid.NewGuid().ToString(),
        //                FileName = file.FileName.Trim(),
        //                AddedBy = loginId,
        //                DateTimeUploaded = DateTime.UtcNow.ToString(),
        //                FlpConfigurationId = flpConfigurationId,
        //                FlpProceeAttempt = "0",
        //                FlpProcessStatusId = "0"
        //            };
        //            retUploadFileId = await _adminRepository.InsertFile(pagePartValueAPIRequest);

        //        }
        //        catch (Exception ex)
        //        {
        //            this._logger.LogError(ex, ex.Message);
        //            //if there's an upload
        //            return new APIResponse<(string, Models.DTOs.v1._0.FileConfiguration.FileValueRequest)>
        //            {
        //                ResultStatus = APIResultStatus.Failed,
        //                ResponseMessage = new List<string> { "Invalid File." },
        //                Result = (string.Empty, pagePartValueAPIRequest)
        //            };
        //            throw;
        //        }

        //        return new APIResponse<(string, Models.DTOs.v1._0.FileConfiguration.FileValueRequest)>
        //        {
        //            Result = (flpProcessTempFile.Uri, pagePartValueAPIRequest),
        //            ResponseMessage = new List<string> { "Success" },
        //            ResultStatus = APIResultStatus.Completed
        //        };
        //    }
        //    else
        //    {
        //        _logger.LogError("Error: Invalid File.");
        //        return new APIResponse<(string, Models.DTOs.v1._0.FileConfiguration.FileValueRequest)>
        //        {
        //            ResultStatus = APIResultStatus.Failed,
        //            ResponseMessage = new List<string> { "Invalid File." },
        //            Result = (string.Empty, pagePartValueAPIRequest)
        //        };
        //    }
        //}

        public async Task<APIResponse<FLPConfigurationModel>> GetConfigurationById(string id)
        {
            try
            {
                FLPConfigurationModel flpConfigurationModel = new FLPConfigurationModel();
                if (string.IsNullOrWhiteSpace(id))
                {
                    _logger.LogError("Error: Missing configurationId in request parameter.");
                    return new APIResponse<FLPConfigurationModel>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Missing configurationId" },
                        Result = null
                    };
                }
                var retFromDb = await _processConfigurationRepository.GetFLPConfigurationByIdAsync(id);

                if (retFromDb == null)
                {
                    _logger.LogError("Error: Configuration not found.");
                    return new APIResponse<FLPConfigurationModel>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "Configuration not found." },
                        Result = null
                    };
                }

                flpConfigurationModel.processSettings = retFromDb;

                var fileSettingsResult = await _processConfigurationRepository.GetFileSettingsByConfigIdAsync(id);
                if (fileSettingsResult == null)
                {
                    _logger.LogError("Error: File settings not found for the given configurationId.");
                    return new APIResponse<FLPConfigurationModel>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "File settings not found." },
                        Result = null
                    };
                }

                foreach (var fileSetting in fileSettingsResult)
                {
                    var additionalSettingsResult = await _processConfigurationRepository.GetAdditionalSettingsByConfigIdAsync(id, fileSetting.tabName);
                    if (additionalSettingsResult == null)
                    {
                        _logger.LogError($"Error: Additional settings not found for tab {fileSetting.tabName} in configurationId {id}.");
                        return new APIResponse<FLPConfigurationModel>
                        {
                            ResultStatus = APIResultStatus.Failed,
                            ResponseMessage = new List<string> { $"Additional settings not found for tab {fileSetting.tabName}." },
                            Result = null
                        };
                    }
                    fileSetting.additionalSettings = additionalSettingsResult;

                    var databaseSettingsResult = await _processConfigurationRepository.GetDatabaseSettingsByConfigIdAsync(id, fileSetting.tabName);
                    if (databaseSettingsResult == null)
                    {
                        _logger.LogError($"Error: Database settings not found for tab {fileSetting.tabName} in configurationId {id}.");
                        return new APIResponse<FLPConfigurationModel>
                        {
                            ResultStatus = APIResultStatus.Failed,
                            ResponseMessage = new List<string> { $"Database settings not found for tab {fileSetting.tabName}." },
                            Result = null
                        };
                    }
                    fileSetting.databaseSettings = databaseSettingsResult;

                    var fileColumnMappingResult = await _processConfigurationRepository.GetFileColumnMappingByConfigIdAsync(id, fileSetting.tabName);
                    if (fileColumnMappingResult == null)
                    {
                        _logger.LogError($"Error: File column mapping not found for tab {fileSetting.tabName} in configurationId {id}.");
                        return new APIResponse<FLPConfigurationModel>
                        {
                            ResultStatus = APIResultStatus.Failed,
                            ResponseMessage = new List<string> { $"File column mapping not found for tab {fileSetting.tabName}." },
                            Result = null
                        };
                    }
                    fileSetting.FileColumnMapping = fileColumnMappingResult;

                    var flpRuleSet = await _processConfigurationRepository.GetFlpRuleSetByConfigurationId(id, fileSetting.tabName);

                    fileSetting.RuleSets = flpRuleSet;

                }
                flpConfigurationModel.fileSettings = fileSettingsResult;

                return new APIResponse<FLPConfigurationModel>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = flpConfigurationModel
                };
            }
            catch (Exception ex)
            {
                //TODO:
                this._logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<FLPConfigurationModel>> GetMultisheetConfigurationById(string flpConfigurationId, string uploadFileId)
        {
            var fLPConfigurationModel = new FLPConfigurationModel();

            var result = await GetConfigurationById(flpConfigurationId);
            var configResult = result?.Result;

            if (configResult == null)
            {
                return new APIResponse<FLPConfigurationModel>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "flpConfiguration not found." },
                    Result = null
                };
            }

            var fileSetting = configResult.fileSettings?.Where(x => x != null && !x.ignoreSheet).ToList() ?? new List<Models.Models.v4._1.FileSettings>();
            fLPConfigurationModel.processSettings = configResult.processSettings;

            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");

            var retFromDb = await _processConfigurationRepository.GetProcessTabNameAsync(securityGroupId, flpConfigurationId, uploadFileId);

            var tabNames = retFromDb?
                .Where(item => item?.tabName != null)
                .Select(item => item.tabName)
                .ToList() ?? new List<string>();

            if (tabNames.Any())
            {
                fLPConfigurationModel.fileSettings = fileSetting
                    .Where(x => tabNames.Contains(x.tabName?.Trim()))
                    .ToList();
            }

            return new APIResponse<FLPConfigurationModel>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = fLPConfigurationModel
            };
        }

        public async Task<APIResponse<bool>> InsertRuleSets(InsertRuleSetsRequest request, string flpConfigurationId = "", string tabName = "")
        {
            try
            {

                //todo: if we will create rules for multiple Sg
                string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    _logger.LogError("Error: Security group not found.");
                    return new APIResponse<bool>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = false
                    };
                }

                foreach (var rule in request.RuleSets)
                {
                    var ruleSet = new RuleSetEntity
                    {

                        id = rule.id,
                        flpConfigurationId = flpConfigurationId,
                        ruleSetNameId = rule.ruleSetNameId,
                        ruleSetName = rule.ruleSetName,
                        ruleTypeId = rule.ruleTypeId,
                        subRuleId = rule.subRuleId,
                        ruleColumnName = rule.ruleColumnName,
                        ruleDescription = rule.ruleDescription,
                        prompt = rule.prompt,
                        format = rule.format,
                        patternId = rule.patternId,
                        isCombinationRule = rule.isCombinationRule,
                        isActive = rule.isActive,
                        isGlobal = rule.isGlobal,
                        ruleSetType = rule.ruleSetType,
                        conditionId = rule.conditionId,
                        fromValue = rule.fromValue,
                        isAllowNullOrSpace = rule.isAllowNullOrEmptySpaces,
                        toValue = rule.toValue,
                        ruleColumnName2 = rule.ruleColumnName2,
                        spNameId = rule.spNameId,
                        isUpdated = rule.isUpdated,

                    };

                    var rulSetResult = await _processConfigurationRepository
                        .InsertProcessRuleSet(ruleSet, tabName, securityGroupId, request.created_by, request.username, request.description);
                }


                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = ["Success"],
                    Result = true
                };
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, ex.Message);
                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = ["Success"],
                    Result = false
                };
                throw;
            }
        }

        public async Task<APIResponse<List<RuleSetNamesDto>>> GetDIGenericRulesNames(bool isActive)
        {
            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");

            if (string.IsNullOrWhiteSpace(securityGroupId))
            {
                _logger.LogError("Error: Security group not found.");
                return new APIResponse<List<RuleSetNamesDto>>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Security group not found." },
                    Result = null
                };
            }

            var DIRuleSetNames = await _processConfigurationRepository.GetDIGenericRulesNames(isActive, securityGroupId);
            if (DIRuleSetNames == null)
            {
                return new APIResponse<List<RuleSetNamesDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = ["No Rule Set Names found."]
                };
            }

            return new APIResponse<List<RuleSetNamesDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = DIRuleSetNames.Select(r => new RuleSetNamesDto
                {
                    ruleSetNameId = r.ruleSetNameId,
                    ruleSetName = r.ruleSetName
                }).ToList()
            };
        }

        public async Task<APIResponse<List<RuleTypesDto>>> GetRuleTypes()
        {
            var DIRules = await _processConfigurationRepository.GetDIRuleTypes();
            if (DIRules == null)
            {
                return new APIResponse<List<RuleTypesDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = ["No DI Rules found."]
                };
            }

            return new APIResponse<List<RuleTypesDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = DIRules.Select(r => new RuleTypesDto
                {
                    ruleTypeId = r.ruleTypeId,
                    ruleTypeName = r.ruleTypeName,
                    category = r.category,
                    description = r.description,
                }).ToList()
            };
        }

        public async Task<APIResponse<List<SubRulesDto>>> GetSubRules(int ruleTypeId)
        {
            var DISubRules = await _processConfigurationRepository.GetDISubRules(ruleTypeId);
            if (DISubRules == null)
            {
                return new APIResponse<List<SubRulesDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = ["No DI SubRules found."]
                };
            }

            return new APIResponse<List<SubRulesDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = DISubRules.Select(r => new SubRulesDto
                {
                    ruleTypeId = r.ruleTypeId,
                    subRuleId = r.subRuleId,
                    subRuleName = r.subRuleName,
                    id = r.id
                }).ToList()
            };
        }

        public async Task<APIResponse<List<PatternsDto>>> GetPatterns(int subRuleId)
        {
            var DIPatterns = await _processConfigurationRepository.GetDIPatterns(subRuleId);
            if (DIPatterns == null)
            {
                return new APIResponse<List<PatternsDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = ["No DI Patterns found."]
                };
            }

            return new APIResponse<List<PatternsDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = DIPatterns.Select(r => new PatternsDto
                {
                    patternName = r.patternName,
                    //ruleId = r.ruleId,
                    subRuleId = r.subRuleId,
                    patternId = r.patternId,
                    sing = r.sing, //singular form
                    plu = r.plu,//plurar form
                }).ToList()
            };
        }
        public async Task<APIResponse<List<ConditionalOperatorsDto>>> GetDIConditionalOperators()
        {
            var DIConditionalOperators = await _processConfigurationRepository.GetDIConditionalOperators();
            if (DIConditionalOperators == null)
            {
                return new APIResponse<List<ConditionalOperatorsDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = ["No DI Patterns found."]
                };
            }

            return new APIResponse<List<ConditionalOperatorsDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = DIConditionalOperators.Select(r => new ConditionalOperatorsDto
                {
                    conditionalOperatorId = r.conditionalOperatorId,
                    conditionalOperatorName = r.conditionalOperatorName
                }).ToList()
            };
        }

        public async Task<APIResponse<List<RuleSetNamesDto>>> GetDIRuleSetNamesBySecGrpId(string securityGroupId)
        {
            var DIConditionalOperators = await _processConfigurationRepository.GetDIRuleSetNamesBySecGrpId(securityGroupId);
            if (DIConditionalOperators == null)
            {
                return new APIResponse<List<RuleSetNamesDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = ["No Rule Set Names found."]
                };
            }

            return new APIResponse<List<RuleSetNamesDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = DIConditionalOperators.Select(r => new RuleSetNamesDto
                {
                    ruleSetNameId = r.ruleSetNameId,
                    ruleSetName = r.ruleSetName
                }).ToList()
            };
        }

        public async Task<APIResponse<List<RuleSetNamesDto>>> GetDIRuleSetByRuleSetName(string ruleSetName, string securityGroupId)
        {
            var DIRuleSetNames = await _processConfigurationRepository.GetDIRuleSetNamesByRuleSetName(ruleSetName, securityGroupId);
            if (DIRuleSetNames == null)
            {
                return new APIResponse<List<RuleSetNamesDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = ["No Rule Set Names found."]
                };
            }

            return new APIResponse<List<RuleSetNamesDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = DIRuleSetNames.Select(r => new RuleSetNamesDto
                {
                    ruleSetNameId = r.ruleSetNameId,
                    ruleSetName = r.ruleSetName
                }).ToList()
            };
        }

        public async Task<APIResponse<List<RuleSetDto>>> GetDIRuleSetByRuleSetNameId(string ruleSetNameId)
        {
            var DIFlpRuleSet = await _processConfigurationRepository.GetDIRuleSetByRuleSetNameId(ruleSetNameId);
            if (DIFlpRuleSet == null)
            {
                return new APIResponse<List<RuleSetDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = ["No Rule Set found."]
                };
            }

            return new APIResponse<List<RuleSetDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = DIFlpRuleSet.Select(r => new RuleSetDto
                {
                    id = r.id,
                    ruleSetNameId = r.ruleSetNameId,
                    ruleSetName = r.ruleSetName,
                    ruleTypeId = r.ruleTypeId,
                    subRuleId = r.subRuleId,
                    ruleColumnNameRaw = r.ruleColumnNameRaw,
                    ruleDescription = r.ruleDescription,
                    prompt = r.prompt,
                    format = r.format,
                    patternId = r.patternId,
                    isCombinationRule = r.isCombinationRule,
                    fromValue = r.fromValue,
                    toValue = r.toValue,
                    conditionId = r.conditionId,
                    description = r.description,
                    isActive = r.isActive,
                    isGlobal = r.isGlobal,
                    ruleSetType = r.ruleSetType,
                    isAllowNullOrEmptySpaces = r.isAllowNullOrEmptySpaces,
                    ruleColumnName2 = r.ruleColumnName2,
                    SPNameId = r.SPNameId
                }).ToList()
            };

        }

        public async Task<APIResponse<List<RuleSetDto>>> GetDIGenericRules()
        {
            var DIGenericRuleSet = await _processConfigurationRepository.GetDIGenericRules();
            if (DIGenericRuleSet == null)
            {
                return new APIResponse<List<RuleSetDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = ["No Rule Set found."]
                };
            }

            return new APIResponse<List<RuleSetDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = DIGenericRuleSet.Select(r => new RuleSetDto
                {
                    id = r.id,
                    ruleTypeId = r.ruleTypeId,
                    subRuleId = r.subRuleId,
                    ruleColumnNameRaw = r.ruleColumnNameRaw,
                    ruleDescription = r.ruleDescription,
                    prompt = r.prompt,
                    format = r.format,
                    patternId = r.patternId,
                    isCombinationRule = r.isCombinationRule,
                    fromValue = r.fromValue,
                    toValue = r.toValue,
                    conditionId = r.conditionId
                }).ToList()
            };
        }



        public async Task<APIResponse<bool>> CheckDIRuleSetNameExists(string ruleSetName)
        {
            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");

            if (string.IsNullOrWhiteSpace(securityGroupId))
            {
                _logger.LogError("Error: Security group not found.");
                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Security group not found." },
                    Result = false
                };
            }

            var ruleSetNameExists = await _processConfigurationRepository.CheckDIRuleSetNameExists(ruleSetName);


            return new APIResponse<bool>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = ruleSetNameExists
            };
        }

        public async Task<APIResponse<RuleSetNameResponse>> GetRuleSetNameList(RuleSetNameListRequest request)
        {
            try
            {
                request.securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");

                var databaseResponse = await _processConfigurationRepository.GetRuleSetNameList(request);

                if (databaseResponse != null)
                {
                    return await Task.FromResult(new APIResponse<RuleSetNameResponse>
                    {
                        ResultStatus = APIResultStatus.Completed,
                        ResponseMessage = new List<string> { "Success" },
                        Result = databaseResponse
                    });
                }
                else
                {
                    _logger.LogError("No Data Found");
                    return await Task.FromResult(new APIResponse<RuleSetNameResponse>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "No Data Found" },
                        Result = null
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return await Task.FromResult(new APIResponse<RuleSetNameResponse>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Internal Server Error" },
                    Result = null
                });
            }
        }

        public async Task<APIResponse<bool>> GetGlobalRuleCreationAccess()
        {
            try
            {
                string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    _logger.LogError("Error: Security group not found.");
                    return new APIResponse<bool>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = false
                    };
                }

                var databaseResponse = await _processConfigurationRepository.GetGlobalRuleCreationAccess(securityGroupId);


                return await Task.FromResult(new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = databaseResponse
                });



            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                return await Task.FromResult(new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Unable to retrieve access." },
                    Result = false
                });
            }
        }

        public async Task<APIResponse<List<ValidationSPNamesDto>>> GetValidationSPNames()
        {
            try
            {
                var databaseResponse = await _processConfigurationRepository.GetValidationSPNames();


                return await Task.FromResult(new APIResponse<List<ValidationSPNamesDto>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = databaseResponse.Select(sp => new ValidationSPNamesDto
                    {
                        SPNameId = sp.SPNameId,
                        SPName = sp.SPName
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return await Task.FromResult(new APIResponse<List<ValidationSPNamesDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Unable to retrieve SP Names." },
                    Result = null
                });
            }
        }

        public async Task<APIResponse<bool>> LogUploadedFile(LogUploadedFileRequest request)
        {
            try
            {
                request.securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
                var databaseResponse = await _processConfigurationRepository.LogUploadedFile(request);

                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "File has been uploaded successfully." },
                    Result = true
                };
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Unable to log uploaded file." },
                    Result = false
                };
            }
        }


        public async Task<APIResponse<bool>> DeleteJsonDatabricksColumnFiles()
        {
            try
            {
                var databricksJsonFiles = await _processConfigurationRepository.GetDatabricksJsonFileURLs();

                if (databricksJsonFiles.Any())
                {
                    foreach (var jsonFile in databricksJsonFiles)
                    {
                        try
                        {
                            // Extract the blob name from the file URL
                            var blobName = GetBlobNameFromUrl(jsonFile.fileURL).Replace(jsonFile.storageContainerName, "");

                            // Construct the full SAS URL
                            var blobSasUrl = $"{jsonFile.fileURL}?{jsonFile.sasKey.Split('?')[1]}";

                            // Get the BlobClient using the SAS token
                            BlobClient blobClient = _blobStorageService.GetBlobClientDetailsBySasToken(blobName, blobSasUrl);
                            // Check if the blob exists
                            bool exists = await blobClient.ExistsAsync();
                            if (!exists)
                            {
                                _logger.LogError($"Blob does not exist: {blobClient.Uri}");
                                continue;
                            }
                            // Delete the blob
                            bool deleted = await _blobStorageService.DeleteBlobAsync(blobClient);
                            if (deleted)
                            {
                                await _processConfigurationRepository.DeletedDatabricksColumnJsonFile(jsonFile.flpConfigurationId, jsonFile.uploadFileId, jsonFile.tabName);
                            }
                            else
                            {
                                _logger.LogError($"Failed to delete blob: {blobName}");
                            }


                        }
                        catch (Exception innerEx)
                        {
                            _logger.LogError(innerEx, $"Error processing file: {jsonFile.fileURL}");
                        }
                    }
                }

                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Processed Databricks JSON files successfully." },
                    Result = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDatabricksJsonFileURLs");
                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Unable to process Databricks JSON files." },
                    Result = false
                };
            }
        }

        public Task<UpdateLoginResponseDto> UpdateLogin(string loginId, ClaimsPrincipal user, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<APIResponse<List<CampaignUserAccessDto>>> GetCampaignUserAccessInfo(string UPN)
        {
            try
            {
                //basic validation
                if (UPN.Trim().Length == 0)
                {
                    return new APIResponse<List<CampaignUserAccessDto>>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid parameters." },
                        Result = null
                    };
                }

                string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    _logger.LogError("Error: Security group not found.");
                    return new APIResponse<List<CampaignUserAccessDto>>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = null
                    };
                }

                var retFromDbase = await _processConfigurationRepository.GetCampaignUserAccessInfo(UPN);

                var campaignUserAccessInfo = retFromDbase.Select(x => new CampaignUserAccessDto
                {
                    internalCampaignId = x.internalCampaignId,
                    campaignId = x.campaignId,
                    campaignName = x.campaignName,
                    clientId = x.clientId,
                    regionId = x.regionId,
                    subRegionId = x.subRegionId
                });

                return new APIResponse<List<CampaignUserAccessDto>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    //ResponseMessage = new List<string> { "Unable to get upn info." },
                    Result = campaignUserAccessInfo.ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCampaignUserAccessInfo");
                return new APIResponse<List<CampaignUserAccessDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Unable to get upn info." },
                    Result = null
                };
            }
        }

        #region LandingLayer
        public async Task<APIResponse<List<FileExtensionDto>>> GetValidFileExtensions()
        {
            try
            {
                var databaseResponse = await _processConfigurationRepository.GetValidFileExtensions();


                return await Task.FromResult(new APIResponse<List<FileExtensionDto>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = databaseResponse.Select(r => new FileExtensionDto
                    {
                        id = r.id,
                        fileExtension = r.fileExtension
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return await Task.FromResult(new APIResponse<List<FileExtensionDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Unable to retrieve File Extensions." },
                    Result = new List<FileExtensionDto>()
                });
            }
        }

        public async Task<APIResponse<List<PrefixesDto>>> GetPrefixes()
        {
            try
            {
                var databaseResponse = await _processConfigurationRepository.GetPrefixes();


                return await Task.FromResult(new APIResponse<List<PrefixesDto>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = databaseResponse.Select(r => new PrefixesDto
                    {
                        id = r.id,
                        prefixName = r.prefixName
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return await Task.FromResult(new APIResponse<List<PrefixesDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Unable to retrieve Prefixes." },
                    Result = new List<PrefixesDto>()
                });
            }
        }




        public async Task<APIResponse<ConfigurationResponseDto>> LandingLayerInsertConfiguration(string json, List<IFormFile> file, string loggedInUser, string userName)
        {
            Models.DTOs.v1._0.FileConfiguration.FileValueRequest pagePartValueAPIRequest = new Models.DTOs.v1._0.FileConfiguration.FileValueRequest();

            //foreach (var f in file)
            //{
            //    try
            //    {
            //        using var stream = f.OpenReadStream();
            //        using var memoryStream = new MemoryStream();
            //        await stream.CopyToAsync(memoryStream);
            //        //fileContentMap[file.FileName] = memoryStream.ToArray();
            //    }
            //    catch (Exception ex)
            //    {
            //      //  _logger.LogError($"Failed to pre-load file content for {file.FileName}: {ex.Message}");
            //        // Continue processing other files
            //    }
            //}
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogError("Error: Configuration json not found.");
                return new APIResponse<ConfigurationResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Configuration json not found." },
                    Result = null
                };
            }

            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");

            if (string.IsNullOrWhiteSpace(securityGroupId))
            {
                _logger.LogError("Error: Security group not found.");
                return new APIResponse<ConfigurationResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Security group not found." },
                    Result = null
                };
            }

            var jsonData = JsonSerializer.Deserialize<FileConfigurationDto>(json);


            if (jsonData?.processSettings != null && jsonData.fileSettings.Count() > 0)
            {


                if (string.IsNullOrWhiteSpace(jsonData.processSettings.processName))
                {
                    _logger.LogError("Error: ProcessName is null.");
                    return new APIResponse<ConfigurationResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { "Error: ProcessName is null." },
                        Result = null
                    };
                }

                var processConfiguration = new ProcessSettingEntity
                {
                    flpConfigurationId = jsonData.processSettings.flpConfigurationId,
                    process_name = jsonData.processSettings.processName,
                    is_active = true,// jsonData.processSettings.is_active,
                    sender_communication_email = jsonData.processSettings.sender_communication_email,
                    created_by = loggedInUser,
                    created_date = DateTime.UtcNow,
                    loginid = loggedInUser,
                    description = jsonData.processSettings.description,
                    regionId = jsonData.processSettings.RegionId,
                    subRegionId = jsonData.processSettings.SubRegionId,
                    clientId = jsonData.processSettings.ClientId,
                    userName = userName,
                    securityGroupId = securityGroupId,
                    securityGroups = jsonData.processSettings.securityGroups,
                    region = jsonData.processSettings.region,
                    subRegion = jsonData.processSettings.subRegion,
                    clientName = jsonData.processSettings.clientName,
                    dataSource = jsonData.processSettings.dataSource,
                    multisheet = jsonData.processSettings.multisheet,
                    sheetReferenceByIndex = jsonData.processSettings.sheetReferenceByIndex,
                };
                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {


                        //upsert security group table

                        foreach (SecurityGroup securityGroup in jsonData.processSettings.securityGroups)
                        {
                            var retVal = await _processConfigRepositoryV4.AddSecurityGroup(securityGroup);

                        }

                        if (string.IsNullOrEmpty(jsonData.processSettings.flpConfigurationId))
                        {
                            processConfiguration.flpConfigurationId = Guid.NewGuid().ToString();
                            var retVal = await _processConfigurationRepository.InsertProcessConfiguration(processConfiguration);

                            foreach (var fileSetting in jsonData.fileSettings)
                            {
                                var fileConfiguration = new FileSettingEntity
                                {
                                    flpConfigurationId = processConfiguration.flpConfigurationId,
                                    tabName = fileSetting.tabName,
                                    ignoreSheet = fileSetting.ignoreSheet,
                                    delimiter = fileSetting.additionalSettings.delimiter,
                                    flexCheckHasHeaders = fileSetting.additionalSettings.flexCheckHasHeaders,
                                    skip_header_rows = fileSetting.additionalSettings.skip_header_rows,
                                    skip_footer_rows = fileSetting.additionalSettings.skip_footer_rows,
                                    txtQuoteCharacter = fileSetting.additionalSettings.txtQuoteCharacter,
                                    key_column_list = String.Join(",", fileSetting.columnNameDatatypeNames.Where(w => w.ColumnKey == true).Select(x => x.ColumnName)),
                                    column_name_list = fileSetting.additionalSettings.column_name_list,
                                    convert_datatypes_column_list = CreateColumns(fileSetting.columnNameDatatypeNames),
                                    loginid = loggedInUser,
                                    order_by_column_list_for_dedup = fileSetting.additionalSettings.order_by_column_list_for_dedup,
                                    ignore_duplicate_rows = fileSetting.additionalSettings.ignore_duplicate_rows,
                                    do_not_archive_file = fileSetting.additionalSettings.do_not_archive_file,
                                    keep_first_row = fileSetting.additionalSettings.keep_first_row,
                                    spanish_to_english = fileSetting.additionalSettings.spanish_to_english,
                                    roman_numerals_only = fileSetting.additionalSettings.roman_numerals_only,
                                    flexCheckSkipEmptyLines = fileSetting.additionalSettings.flexCheckSkipEmptyLines,
                                    prefix = fileSetting.additionalSettings.landingLayerPrefix ?? "",
                                    dateFormatId = fileSetting.additionalSettings?.landingLayerDateformat ?? 0,
                                    timeFormatId = fileSetting.additionalSettings?.landingLayerTimeformat ?? 0
                                };
                                var fileConfigTask = await _processConfigurationRepository.InsertProcessFileConfiguration(fileConfiguration);

                                if (fileSetting.additionalSettings.landingLayerRegex.Count > 0)
                                {

                                    var retValRegex = await _processConfigurationRepository.InsertRegex(fileSetting.additionalSettings.landingLayerRegex, processConfiguration.flpConfigurationId, loggedInUser);
                                }

                                if (fileSetting.additionalSettings.landingLayerFileExtension.Count > 0)
                                {

                                    var retValExtension = await _processConfigurationRepository.InsertLandingLayerFileExtension(fileSetting.additionalSettings.landingLayerFileExtension, processConfiguration.flpConfigurationId, loggedInUser);
                                }

                                if (fileSetting.ignoreSheet)
                                {
                                    fileSetting.databaseSettings.deltaStorageAccountId = null;
                                    fileSetting.databaseSettings.databaseConfigurationId = null;
                                    fileSetting.databaseSettings.deltaServerNameId = null;
                                }

                                var databaseConfiguration = new DatabaseSettingEntity
                                {
                                    flpConfigurationId = processConfiguration.flpConfigurationId,
                                    tabName = fileSetting.tabName,
                                    ignoreSheet = fileSetting.ignoreSheet,
                                    process_name = processConfiguration.process_name,
                                    db_file_columnName_list = CreateFileDBColumnDataTypeMapping(fileSetting.columnNameDatatypeNames),
                                    table_name = processConfiguration.dataSource == (int)FileProcessingServerType.SQLServer ? fileSetting.databaseSettings.tableName : fileSetting.databaseSettings.deltaTableName,
                                    loginid = loggedInUser,
                                    drop_main_table = fileSetting.databaseSettings.drop_main_table,
                                    drop_history_table = fileSetting.databaseSettings.drop_history_table,
                                    validate_fileschema = fileSetting.databaseSettings.validate_fileschema,
                                    databaseConfigurationId = processConfiguration.dataSource == (int)FileProcessingServerType.SQLServer ? fileSetting.databaseSettings.databaseConfigurationId : fileSetting.databaseSettings.deltaServerNameId?.ToString(),
                                    mergeData = fileSetting.databaseSettings.mergeData,
                                    createHistoryTable = fileSetting.databaseSettings.createHistoryTable,
                                    dataSource = processConfiguration.dataSource,
                                    deltaStorageAccountId = fileSetting.databaseSettings.deltaStorageAccountId,
                                    deltaContainerName = fileSetting.databaseSettings.deltaContainerName,
                                    deltaSource = fileSetting.databaseSettings.deltaSource,
                                    deltaJobId = fileSetting.databaseSettings.deltaJobId,
                                    landingLayerAcceptedPath = fileSetting.databaseSettings?.landingLayerAcceptedPath,
                                    landingLayerRejectedPath = fileSetting.databaseSettings?.landingLayerRejectedPath,
                                };

                                var databaseConfigResult = await _processConfigurationRepository.InsertProcessDatabaseConfiguration(databaseConfiguration);


                                foreach (var rule in fileSetting.ruleSet)
                                {
                                    var ruleSet = new RuleSetEntity
                                    {

                                        id = rule.id,
                                        flpConfigurationId = processConfiguration.flpConfigurationId,
                                        ruleSetNameId = rule.ruleSetNameId,
                                        ruleSetName = rule.ruleSetName,
                                        ruleTypeId = rule.ruleTypeId,
                                        subRuleId = rule.subRuleId,
                                        ruleColumnName = rule.ruleColumnName,
                                        ruleColumnName2 = rule.ruleColumnName2,
                                        ruleDescription = rule.ruleDescription,
                                        prompt = rule.prompt,
                                        format = rule.format,
                                        patternId = rule.patternId,
                                        isCombinationRule = rule.isCombinationRule,
                                        isActive = true,
                                        isGlobal = rule.isGlobal,
                                        ruleSetType = rule.ruleSetType,
                                        conditionId = rule.conditionId,
                                        fromValue = rule.fromValue,
                                        toValue = rule.toValue,
                                        isAllowNullOrSpace = rule.isAllowNullOrEmptySpaces,
                                        spNameId = rule.spNameId

                                    };

                                    var rulSetResult = await _processConfigurationRepository
                                        .InsertProcessRuleSet(ruleSet, fileSetting.tabName, string.Join(",", jsonData.processSettings.securityGroups.Select(sg => sg.securityGroupId)), loggedInUser, userName, "");
                                }


                            }

                        }
                        else
                        {
                            foreach (var fileSetting in jsonData.fileSettings)
                            {
                                //update the convert_datatypes_column_list and column_name_list
                                //Update only which are not ignoredheet
                                if (!fileSetting.ignoreSheet)
                                {
                                    var retFromDb = await _processConfigurationRepository.UpdateColumnNameList(processConfiguration.flpConfigurationId, fileSetting.tabName, fileSetting.additionalSettings.column_name_list);
                                    retFromDb = await _processConfigurationRepository.UpdateConvertDatatypesColumnList(processConfiguration.flpConfigurationId, fileSetting.tabName, CreateColumns(fileSetting.columnNameDatatypeNames));
                                    retFromDb = await _processConfigurationRepository.UpdateFlpFileColumnMapping(processConfiguration.flpConfigurationId, processConfiguration.process_name, fileSetting.tabName, fileSetting.databaseSettings.tableName, CreateFileDBColumnDataTypeMapping(fileSetting.columnNameDatatypeNames));
                                }

                            }

                        }
                        scope.Complete();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inserting configuration: {Message}", ex.Message);
                        return new APIResponse<ConfigurationResponseDto>
                        {
                            ResultStatus = APIResultStatus.Failed,
                            ResponseMessage = new List<string> { "Error inserting configuration." },
                            Result = null
                        };
                    }
                }


                if (file !=null && file.Any())
                {

                    string fileName = "Landing Layer";
                    string destinationContainerName = "";
                    string returnUri = string.Empty;
                    try
                    {

                        var result = await LandingLayerAddDataInUploadTable(processConfiguration.process_name, fileName, processConfiguration.loginid, processConfiguration.flpConfigurationId);
                        if (result != null && result.ResponseCode == APIResultStatus.Completed.Code)// && !string.IsNullOrEmpty(result.Result.Item1))
                        {
                            returnUri = result.Result.Item1;
                            pagePartValueAPIRequest = result.Result.Item2;
                        }
                        else
                        {
                            _logger.LogError("Error: Unable to upload file.");
                            return new APIResponse<ConfigurationResponseDto>
                            {
                                ResultStatus = APIResultStatus.Failed,
                                ResponseMessage = new List<string> { "Unable to upload file." },
                                Result = null
                            };
                        }

                        //Task<APIResponse<FlpProcessResponseDto>> MoveUploadFilesToLayerFile(List<IFormFile> files, string processName, string flpConfigurationId, string uploadFileId, string loginId)
                       // await _landingLayerService.MoveUploadFilesToLayerFile(file, processConfiguration.process_name, processConfiguration.flpConfigurationId, pagePartValueAPIRequest.UploadFileId, loggedInUser);
                        //await _backgroundTaskQueue.QueueAsync(async (ct) =>
                        //{                            
                        //    //var status = await ExecutePDMProfilingSP(connectionString, runIdString, profilingSPConfiguration.spName, ct);

                        //    //await UpdateRunSatusAsync(runIdString, status);
                        //    await _fileProcessService.MoveFileInLandingLayerFolder(file, processConfiguration.process_name, processConfiguration.flpConfigurationId, pagePartValueAPIRequest.UploadFileId, loggedInUser);
                        //});
                        if (result != null && result.ResponseCode == APIResultStatus.Completed.Code)
                        {
                            returnUri = result.Result.Item1;
                            pagePartValueAPIRequest = result.Result.Item2;
                        }
                        else
                        {
                            _logger.LogError("Error: Unable to upload file.");
                            return new APIResponse<ConfigurationResponseDto>
                            {
                                ResultStatus = APIResultStatus.Failed,
                                ResponseMessage = new List<string> { "Unable to upload file." },
                                Result = null
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        this._logger.LogError(ex, ex.Message);
                        //if there's an upload
                        return new APIResponse<ConfigurationResponseDto>
                        {
                            ResultStatus = APIResultStatus.Failed,
                            ResponseMessage = new List<string> { "Invalid File." },
                            Result = new ConfigurationResponseDto()
                            {
                                ProcessName = processConfiguration.process_name,
                                FlpConfigurationId = processConfiguration.flpConfigurationId,
                                BlobClients = new BlobClientDetails()
                                {
                                    Uri = "",
                                    AccountName = "",
                                    BlobContainerName = "",
                                    CanGenerateSasUri = true,
                                    Name = "",
                                    UploadedId = pagePartValueAPIRequest.UploadFileId
                                }
                            }
                        };
                        throw;
                    }



                    //this is just a test
                    return new APIResponse<ConfigurationResponseDto>
                    {
                        Result = new ConfigurationResponseDto()
                        {
                            ProcessName = processConfiguration.process_name,
                            FlpConfigurationId = processConfiguration.flpConfigurationId,
                            BlobClients = new BlobClientDetails()
                            {
                                Uri = "",
                                AccountName = "tpusadevelopmenttest", //todo: hardcode as of the moment
                                BlobContainerName = "",
                                CanGenerateSasUri = true,
                                Name = "",
                                UploadedId = pagePartValueAPIRequest.UploadFileId
                            }
                        },
                        ResponseMessage = new List<string> { "Success" },
                        ResultStatus = APIResultStatus.Completed
                    };
                }
                else
                {
                    _logger.LogError("Error: Invalid File.");
                    return new APIResponse<ConfigurationResponseDto>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "Invalid File." },
                        Result = null
                    };
                }



            }
            else
            {
                _logger.LogError("Error: Additional settings not provided.");

                return new APIResponse<ConfigurationResponseDto>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Additional settings not provided." },
                    Result = null
                };
            }
        }
                        
        public async Task<APIResponse<string>> AddCampaignConfiguration(AddCampaignConfigurationRequestDto request)
        {
            try
            {
                if (request == null)
                {
                    _logger.LogError("Error: Campaign configuration request is null.");
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Campaign configuration request cannot be null." },
                        Result = null
                    };
                }

                if (string.IsNullOrWhiteSpace(request.CampaignId) || string.IsNullOrWhiteSpace(request.CampaignName))
                {
                    _logger.LogError("Error: CampaignId and CampaignName are required.");
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "CampaignId and CampaignName are required." },
                        Result = null
                    };
                }
               
                string applicationId = ExtractApplicationIdFromToken();
                if (string.IsNullOrWhiteSpace(applicationId))
                {
                    _logger.LogWarning("Application ID could not be extracted from bearer token");
                    // You might want to return an error here if applicationId is mandatory
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid token" },
                        Result = null
                    };
                }
                var existingCampaign = await _processConfigurationRepository.GetCampaignConfigurationByCampaignId(request.CampaignId,applicationId);
                if (existingCampaign != null)
                {
                    _logger.LogWarning($"Campaign configuration already exists for CampaignId: {request.CampaignId}, CampaignName: {request.CampaignName}, RegionId: {request.RegionId}, SubRegionId: {request.SubRegionId}, ClientId: {request.ClientId}, UPN: {request.Upn}, applicationId: {applicationId}");
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "This Campaign already configuration in DI. To update the details for this campaign, please use the following API endpoint: api/ProcessConfiguration/UpdateCampaignConfiguration.." },
                        Result = "Campaign details already exist."
                    };
                }

                var internalCampaignId = Guid.NewGuid().ToString();

                // Add Campaign Configuration
                var campaignConfiguration = new FlpCampaignConfiguration
                {
                    InternalCampaignId = internalCampaignId,
                    CampaignId = request.CampaignId,
                    CampaignName = request.CampaignName,
                    RegionId = request.RegionId,
                    SubRegionId = request.SubRegionId,
                    ClientId = request.ClientId,
                    ApplicationId = applicationId,  
                    AddedBy = "" //request.AddedBy
                };

                var configResult = await _processConfigurationRepository.AddCampaignConfiguration(campaignConfiguration);



                if (string.Compare(configResult.Result, "Failure", true) == 0)
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { configResult?.Message ?? "Failed to add campaign configuration." },
                        Result = null
                    };


                _logger.LogInformation($"Campaign configuration added successfully. InternalCampaignId: {internalCampaignId}");

                // Add Campaign User Access if UPNs are provided
                var addedRecords = new List<string>();
                var failedRecords = new List<string>();

                if (!string.IsNullOrWhiteSpace(request.Upn))
                {
                    var upnList = request.Upn.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(u => u.Trim())
                        .Where(u => !string.IsNullOrWhiteSpace(u))
                        .ToList();

                    if (upnList.Any())
                    {
                        foreach (var upn in upnList)
                        {
                            try
                            {
                                var campaignUserAccessId = Guid.NewGuid().ToString();

                                var campaignUserAccess = new FlpCampaignUserAccess
                                {
                                    CampaignUserAccessId = campaignUserAccessId,
                                    InternalCampaignId = internalCampaignId,
                                    Upn = upn
                                };

                                var userAccessResult = await _processConfigurationRepository.AddCampaignUserAccess(campaignUserAccess);
                                if (string.Compare(userAccessResult.Result, "Failure", true) == 0)
                                    return new APIResponse<string>
                                    {
                                        ResultStatus = APIResultStatus.Failed,
                                        ResponseMessage = new List<string> { userAccessResult?.Message ?? "Failed to add Campaign User Access details." },
                                        Result = null
                                    };

                                addedRecords.Add(upn);
                                _logger.LogInformation($"Campaign user access added successfully for UPN: {upn}");

                            }
                            catch (Exception innerEx)
                            {
                                failedRecords.Add(upn);
                                _logger.LogError(innerEx, $"Error adding campaign user access for UPN: {upn}");
                            }
                        }
                    }
                }

                // Build response message
                var responseMessages = new List<string>();// { "Campaign configuration added successfully." };
                var resultDetails = $"InternalCampaignId: {internalCampaignId}";

                if (addedRecords.Any())
                {
                    responseMessages.Add($"{addedRecords.Count} user access record(s) added successfully.");
                    //resultDetails += $"; Added UPNs: {string.Join(", ", addedRecords)}";
                }

                if (failedRecords.Any())
                {
                    responseMessages.Add($"{failedRecords.Count} user access record(s) failed to add.");
                    //resultDetails += $"; Failed UPNs: {string.Join(", ", failedRecords)}";
                }

                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = responseMessages,
                    Result = "Campaign configuration added successfully."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AddCampaignConfiguration");
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "An error occurred while adding campaign configuration." },
                    Result = null
                };
            }
        }
        
        public async Task<APIResponse<string>> UpdateCampaignConfiguration(AddCampaignConfigurationRequestDto request)
        {
            try
            {
                if (request == null)
                {
                    _logger.LogError("Error: Campaign configuration request is null.");
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Campaign configuration request cannot be null." },
                        Result = null
                    };
                }

                if (string.IsNullOrWhiteSpace(request.CampaignId) || string.IsNullOrWhiteSpace(request.CampaignName))
                {
                    _logger.LogError("Error: CampaignId and CampaignName are required.");
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "CampaignId and CampaignName are required." },
                        Result = null
                    };
                }
                string applicationId = ExtractApplicationIdFromToken();
                if (string.IsNullOrWhiteSpace(applicationId))
                {
                    _logger.LogWarning("Application ID could not be extracted from bearer token");
                    // You might want to return an error here if applicationId is mandatory
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid token" },
                        Result = null
                    };
                }
                // Get existing campaign to find internalCampaignId
                var existingCampaign = await _processConfigurationRepository.GetCampaignConfigurationByCampaignId(request.CampaignId, applicationId);

                if (existingCampaign == null)
                {
                    _logger.LogError($"Error: Campaign not found with CampaignId: {request.CampaignId}");
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Campaign not found." },
                        Result = null
                    };
                }

                // Update Campaign Configuration
                var campaignConfiguration = new FlpCampaignConfiguration
                {
                    InternalCampaignId = existingCampaign.InternalCampaignId, // Use existing internal ID
                    CampaignId = request.CampaignId,
                    CampaignName = request.CampaignName,
                    RegionId = request.RegionId,
                    SubRegionId = request.SubRegionId,
                    ClientId = request.ClientId,
                    ApplicationId = applicationId,
                    AddedBy = "" //request.AddedBy
                };

                var configResult = await _processConfigurationRepository.UpdateCampaignConfiguration(campaignConfiguration);

                if (string.Compare(configResult.Result, "Failure", true) == 0)
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { configResult?.Message ?? "Failed to update campaign configuration." },
                        Result = null
                    };

                _logger.LogInformation($"Campaign configuration updated successfully. CampaignId: {request.CampaignId}");

                // Update Campaign User Access if UPNs are provided
                var addedRecords = new List<string>();
                var failedRecords = new List<string>();

                if (!string.IsNullOrWhiteSpace(request.Upn))
                {
                    var upnList = request.Upn.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(u => u.Trim())
                        .Where(u => !string.IsNullOrWhiteSpace(u))
                        .ToList();

                    if (upnList.Any())
                    {
                        foreach (var upn in upnList)
                        {
                            try
                            {
                                var campaignUserAccessId = Guid.NewGuid().ToString();

                                var campaignUserAccess = new FlpCampaignUserAccess
                                {
                                    CampaignUserAccessId = campaignUserAccessId,
                                    InternalCampaignId = existingCampaign.InternalCampaignId,
                                    Upn = upn
                                };

                                var userAccessResult = await _processConfigurationRepository.UpdateCampaignUserAccess(campaignUserAccess);
                                if (string.Compare(userAccessResult.Result, "Failure", true) == 0)
                                {
                                    failedRecords.Add(upn);
                                    _logger.LogWarning($"Failed to update user access for UPN: {upn}. Message: {userAccessResult?.Message}");
                                }
                                else
                                {
                                    addedRecords.Add(upn);
                                    _logger.LogInformation($"Campaign user access updated successfully for UPN: {upn}");
                                }
                            }
                            catch (Exception innerEx)
                            {
                                failedRecords.Add(upn);
                                _logger.LogError(innerEx, $"Error updating campaign user access for UPN: {upn}");
                            }
                        }
                    }
                }

                // Build response message
                var responseMessages = new List<string>();// { "Campaign configuration updated successfully." };
               // var resultDetails = $"CampaignId: {request.CampaignId}";

                if (addedRecords.Any())
                {
                    responseMessages.Add($"{addedRecords.Count} user access record(s) updated successfully.");
                   // resultDetails += $"; Updated UPNs: {string.Join(", ", addedRecords)}";
                }

                if (failedRecords.Any())
                {
                    responseMessages.Add($"{failedRecords.Count} user access record(s) failed to update.");
                   // resultDetails += $"; Failed UPNs: {string.Join(", ", failedRecords)}";
                }

                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = responseMessages,
                    Result = "Campaign configuration updated successfully."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateCampaignConfiguration");
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "An error occurred while updating campaign configuration." },
                    Result = null
                };
            }
        }

        // Add this method to the existing ProcessConfigurationServiceV4_1 class

        public async Task<APIResponse<string>> AddCampaignUserByClientGeoMapping(AddCampaignUserByClientGeoMapping request)
        {
            try
            {
                if (request == null)
                {
                    _logger.LogError("Error: Campaign configuration request is null.");
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Campaign configuration request cannot be null." },
                        Result = null
                    };
                }
                string applicationId = ExtractApplicationIdFromToken();
                if (string.IsNullOrWhiteSpace(applicationId))
                {
                    _logger.LogWarning("Application ID could not be extracted from bearer token");
                    // You might want to return an error here if applicationId is mandatory
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid token" },
                        Result = null
                    };
                }
                // Get existing campaigns based on regionId, subRegionId, clientId, and upn
                var existingCampaigns = await _processConfigurationRepository.GetCampaignByClientGeoMapping(
                    request.RegionId,
                    request.SubRegionId,
                    request.ClientId,
                    applicationId);

                if (!existingCampaigns.Any())
                {
                    string message = $"Error: Not found details with RegionId: {request.RegionId}, SubRegionId: {request.SubRegionId}, ClientId: {request.ClientId}";
                    _logger.LogError(message);
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { message },
                        Result = null
                    };
                }

               
                // Update Campaign User Access for all campaigns
                var addedUserAccessRecords = new List<string>();
                var failedUserAccessRecords = new List<string>();

                if (!string.IsNullOrWhiteSpace(request.Upn))
                {
                    var upnList = request.Upn.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(u => u.Trim())
                        .Where(u => !string.IsNullOrWhiteSpace(u))
                        .ToList();

                    if (upnList.Any())
                    {
                        foreach (var campaign in existingCampaigns) // Only update user access for successfully updated campaigns
                        {
                            var campaignInternalId = campaign.InternalCampaignId;

                            if (!string.IsNullOrEmpty(campaignInternalId))
                            {
                                foreach (var upn in upnList)
                                {
                                    try
                                    {
                                        var campaignUserAccessId = Guid.NewGuid().ToString();

                                        var campaignUserAccess = new FlpCampaignUserAccess
                                        {
                                            CampaignUserAccessId = campaignUserAccessId,
                                            InternalCampaignId = campaignInternalId,
                                            Upn = upn
                                        };

                                        var userAccessResult = await _processConfigurationRepository.UpdateCampaignUserAccess(campaignUserAccess);
                                        if (string.Compare(userAccessResult.Result, "Failure", true) == 0)
                                        {
                                            failedUserAccessRecords.Add($"{upn} for {campaign.CampaignId}");
                                            _logger.LogWarning($"Failed to update user access for UPN: {upn} and Campaign: {campaign.CampaignId}. Message: {userAccessResult?.Message}");
                                        }
                                        else
                                        {
                                            addedUserAccessRecords.Add($"{upn} for {campaign.CampaignId}");
                                            _logger.LogInformation($"Campaign user access updated successfully for UPN: {upn} and Campaign: {campaign.CampaignId}");
                                        }
                                    }
                                    catch (Exception innerEx)
                                    {
                                        failedUserAccessRecords.Add($"{upn} for {campaign.CampaignId}");
                                        _logger.LogError(innerEx, $"Error updating campaign user access for UPN: {upn} and Campaign: {campaign.CampaignId}");
                                    }
                                }
                            }
                        }
                    }
                }

                // Build response message
                var responseMessages = new List<string>();

               
                if (addedUserAccessRecords.Any())
                {
                    responseMessages.Add($"{addedUserAccessRecords.Count} user access record(s) added successfully.");
                }

                if (failedUserAccessRecords.Any())
                {
                    responseMessages.Add($"{failedUserAccessRecords.Count} user access record(s) failed to add.");
                }

                var resultStatus =  failedUserAccessRecords.Any()
                    ? APIResultStatus.Error
                    : APIResultStatus.Completed;

                string msg = failedUserAccessRecords.Any() ? "Error occurred while processing the request." : "Processed Completed successfully.";

                return new APIResponse<string>
                {
                    ResultStatus = resultStatus,
                    ResponseMessage = responseMessages,
                    Result = $"{msg}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AddCampaignUserByClientGeoMapping");
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Internal server error occurred while processing the request." },
                    Result = null
                };
            }
        }


        public async Task<APIResponse<string>> RemoveCampaignUserByClientGeoMapping(AddCampaignUserByClientGeoMapping request)
        {
            try
            {
                if (request == null)
                {
                    _logger.LogError("Error: Campaign configuration request is null.");
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Campaign configuration request cannot be null." },
                        Result = null
                    };
                }

                string applicationId = ExtractApplicationIdFromToken();
                if (string.IsNullOrWhiteSpace(applicationId))
                {
                    _logger.LogWarning("Application ID could not be extracted from bearer token");
                    // You might want to return an error here if applicationId is mandatory
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid token" },
                        Result = null
                    };
                }

                // Get existing campaigns based on regionId, subRegionId, clientId, and upn
                var existingCampaigns = await _processConfigurationRepository.GetCampaignByClientGeoMapping(
                    request.RegionId,
                    request.SubRegionId,
                    request.ClientId,applicationId);

                if (!existingCampaigns.Any())
                {
                    string message = $"Error: Not found details with RegionId: {request.RegionId}, SubRegionId: {request.SubRegionId}, ClientId: {request.ClientId}";
                    _logger.LogError(message);
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { message },
                        Result = null
                    };
                }


                // Update Campaign User Access for all campaigns
                var addedUserAccessRecords = new List<string>();
                var failedUserAccessRecords = new List<string>();

                if (!string.IsNullOrWhiteSpace(request.Upn))
                {
                    var upnList = request.Upn.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(u => u.Trim())
                        .Where(u => !string.IsNullOrWhiteSpace(u))
                        .ToList();

                    if (upnList.Any())
                    {
                        foreach (var campaign in existingCampaigns) // Only update user access for successfully updated campaigns
                        {
                            var campaignInternalId = campaign.InternalCampaignId;

                            if (!string.IsNullOrEmpty(campaignInternalId))
                            {
                                foreach (var upn in upnList)
                                {
                                    try
                                    {
                                        var campaignUserAccessId = Guid.NewGuid().ToString();

                                        var campaignUserAccess = new FlpCampaignUserAccess
                                        {
                                            CampaignUserAccessId = campaignUserAccessId,
                                            InternalCampaignId = campaignInternalId,
                                            Upn = upn
                                        };

                                        var userAccessResult = await _processConfigurationRepository.DeleteCampaignUserAccess(campaignUserAccess);
                                        if (string.Compare(userAccessResult.Result, "Failure", true) == 0)
                                        {
                                            failedUserAccessRecords.Add($"{upn} for {campaign.CampaignId}");
                                            _logger.LogWarning($"Failed to update user access for UPN: {upn} and Campaign: {campaign.CampaignId}. Message: {userAccessResult?.Message}");
                                        }
                                        else
                                        {
                                            addedUserAccessRecords.Add($"{upn} for {campaign.CampaignId}");
                                            _logger.LogInformation($"Campaign user access Deleted successfully for UPN: {upn}");
                                        }
                                    }
                                    catch (Exception innerEx)
                                    {
                                        failedUserAccessRecords.Add($"{upn} for {campaign.CampaignId}");
                                        _logger.LogError(innerEx, $"Error Deleting campaign user access for UPN: {upn}");
                                    }
                                }
                            }
                        }
                    }
                }

                // Build response message
                var responseMessages = new List<string>();


                if (addedUserAccessRecords.Any())
                {
                   // responseMessages.Add($"{addedUserAccessRecords.Count} user access record(s) deleted successfully.");
                    responseMessages.Add($"Remove operation completed successfully.");
                }

                if (failedUserAccessRecords.Any())
                {
                    //responseMessages.Add($"{failedUserAccessRecords.Count} user access record(s) failed to delete.");
                    responseMessages.Add($"Remove operation failed to remove.");
                }

                var resultStatus = failedUserAccessRecords.Any()
                    ? APIResultStatus.Error
                    : APIResultStatus.Completed;
                string msg = failedUserAccessRecords.Any() ? "Error occurred while processing the request." : "Processed Completed successfully.";

                return new APIResponse<string>
                {
                    ResultStatus = resultStatus,
                    ResponseMessage = responseMessages,
                    Result = $"{msg}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RemoveCampaignUserByClientGeoMapping");
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "An error occurred while processing the request." },
                    Result = null
                };
            }
        }



        public async Task<APIResponse<string>> AddCampaignSuperAdmin(AddCampaignSuperAdmin request)
        {
            try
            {
                if (request == null)
                {
                    _logger.LogError("Error: upn is null.");
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "upn  request cannot be null." },
                        Result = null
                    };
                }

                string applicationId = ExtractApplicationIdFromToken();
                if (string.IsNullOrWhiteSpace(applicationId))
                {
                    _logger.LogWarning("Application ID could not be extracted from bearer token");
                    // You might want to return an error here if applicationId is mandatory
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid token" },
                        Result = null
                    };
                }
                // Get existing campaigns based on regionId, subRegionId, clientId, and upn
                var existingCampaigns = await _processConfigurationRepository.GetCampaignDetails("",applicationId);

                if (!existingCampaigns.Any())
                {
                    string message = $"Error: Not found campaign details";
                    _logger.LogError(message);
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { message },
                        Result = null
                    };
                }


                // Update Campaign User Access for all campaigns
                var addedUserAccessRecords = new List<string>();
                var failedUserAccessRecords = new List<string>();

                if (!string.IsNullOrWhiteSpace(request.Upn))
                {
                    var upnList = request.Upn.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(u => u.Trim())
                        .Where(u => !string.IsNullOrWhiteSpace(u))
                        .ToList();

                    if (upnList.Any())
                    {
                        foreach (var campaign in existingCampaigns) // Only update user access for successfully updated campaigns
                        {
                            var campaignInternalId = campaign.InternalCampaignId;

                            if (!string.IsNullOrEmpty(campaignInternalId))
                            {
                                foreach (var upn in upnList)
                                {
                                    try
                                    {
                                        var campaignUserAccessId = Guid.NewGuid().ToString();

                                        var campaignUserAccess = new FlpCampaignUserAccess
                                        {
                                            CampaignUserAccessId = campaignUserAccessId,
                                            InternalCampaignId = campaignInternalId,
                                            Upn = upn
                                        };

                                        var userAccessResult = await _processConfigurationRepository.UpdateCampaignUserAccess(campaignUserAccess);
                                        if (string.Compare(userAccessResult.Result, "Failure", true) == 0)
                                        {
                                            failedUserAccessRecords.Add($"{upn} for {campaign.CampaignId}");
                                            _logger.LogWarning($"Failed to update user access for UPN: {upn} and Campaign: {campaign.CampaignId}. Message: {userAccessResult?.Message}");
                                        }
                                        else
                                        {
                                            addedUserAccessRecords.Add($"{upn} for {campaign.CampaignId}");
                                            _logger.LogInformation($"Campaign user access updated successfully for UPN: {upn} and Campaign: {campaign.CampaignId}");
                                        }
                                    }
                                    catch (Exception innerEx)
                                    {
                                        failedUserAccessRecords.Add($"{upn} for {campaign.CampaignId}");
                                        _logger.LogError(innerEx, $"Error updating campaign user access for UPN: {upn} and Campaign: {campaign.CampaignId}");
                                    }
                                }
                            }
                        }
                    }
                }

                // Build response message
                var responseMessages = new List<string>();


                if (addedUserAccessRecords.Any())
                {
                     responseMessages.Add($"{addedUserAccessRecords.Count} user access record(s) added successfully.");
                   
                }

                if (failedUserAccessRecords.Any())
                {
                    responseMessages.Add($"{failedUserAccessRecords.Count} user access record(s) failed to add.");
                }

                var resultStatus = failedUserAccessRecords.Any()
                    ? APIResultStatus.Error
                    : APIResultStatus.Completed;
                string msg = failedUserAccessRecords.Any() ? "Error occurred while processing the request." : "Processed Completed successfully.";

                return new APIResponse<string>
                {
                    ResultStatus = resultStatus,
                    ResponseMessage = responseMessages,
                    Result = $"{msg}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AddCampaignSuperAdmin");
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Internal server error occurred while processing the request." },
                    Result = null
                };
            }
        }
        public async Task<APIResponse<string>> RemoveCampaignSuperAdmin(AddCampaignSuperAdmin request)
        {
            try
            {
                if (request == null)
                {
                    _logger.LogError("Error: upn is null.");
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "upn cannot be null." },
                        Result = null
                    };
                }


                string applicationId = ExtractApplicationIdFromToken();
                if (string.IsNullOrWhiteSpace(applicationId))
                {
                    _logger.LogWarning("Application ID could not be extracted from bearer token");
                    // You might want to return an error here if applicationId is mandatory
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid token" },
                        Result = null
                    };
                }

                // Get existing campaigns based on regionId, subRegionId, clientId, and upn
                var existingCampaigns = await _processConfigurationRepository.GetCampaignDetails(request?.Upn??"", applicationId);

                if (!existingCampaigns.Any())
                {
                    string message = $"Error: Not found campaign details";
                    _logger.LogError(message);
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { message },
                        Result = null
                    };
                }


                // Update Campaign User Access for all campaigns
                var addedUserAccessRecords = new List<string>();
                var failedUserAccessRecords = new List<string>();

                if (!string.IsNullOrWhiteSpace(request.Upn))
                {
                    var upnList = request.Upn.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(u => u.Trim())
                        .Where(u => !string.IsNullOrWhiteSpace(u))
                        .ToList();

                    if (upnList.Any())
                    {
                        foreach (var campaign in existingCampaigns) // Only update user access for successfully updated campaigns
                        {
                            var campaignInternalId = campaign.InternalCampaignId;

                            if (!string.IsNullOrEmpty(campaignInternalId))
                            {
                                foreach (var upn in upnList)
                                {
                                    try
                                    {
                                        var campaignUserAccessId = Guid.NewGuid().ToString();

                                        var campaignUserAccess = new FlpCampaignUserAccess
                                        {
                                            CampaignUserAccessId = campaignUserAccessId,
                                            InternalCampaignId = campaignInternalId,
                                            Upn = upn
                                        };

                                        var userAccessResult = await _processConfigurationRepository.DeleteCampaignUserAccess(campaignUserAccess);
                                        if (string.Compare(userAccessResult.Result, "Failure", true) == 0)
                                        {
                                            failedUserAccessRecords.Add($"{upn} for {campaign.CampaignId}");
                                            _logger.LogWarning($"Failed to update user access for UPN: {upn} and Campaign: {campaign.CampaignId}. Message: {userAccessResult?.Message}");
                                        }
                                        else
                                        {
                                            addedUserAccessRecords.Add($"{upn} for {campaign.CampaignId}");
                                            _logger.LogInformation($"Campaign user access updated successfully for UPN: {upn} and Campaign: {campaign.CampaignId}");
                                        }
                                    }
                                    catch (Exception innerEx)
                                    {
                                        failedUserAccessRecords.Add($"{upn} for {campaign.CampaignId}");
                                        _logger.LogError(innerEx, $"Error updating campaign user access for UPN: {upn} and Campaign: {campaign.CampaignId}");
                                    }
                                }
                            }
                        }
                    }
                }

                // Build response message
                var responseMessages = new List<string>();


                if (addedUserAccessRecords.Any())
                {
                    // responseMessages.Add($"{addedUserAccessRecords.Count} user access record(s) deleted successfully.");
                    responseMessages.Add($"Remove operation completed successfully.");
                }

                if (failedUserAccessRecords.Any())
                {
                    //responseMessages.Add($"{failedUserAccessRecords.Count} user access record(s) failed to delete.");
                    responseMessages.Add($"Remove operation failed to delete.");
                }

                var resultStatus = failedUserAccessRecords.Any()
                    ? APIResultStatus.Error
                    : APIResultStatus.Completed;
                string msg = failedUserAccessRecords.Any() ? "Error occurred while processing the request." : "Processed Completed successfully.";

                return new APIResponse<string>
                {
                    ResultStatus = resultStatus,
                    ResponseMessage = responseMessages,
                    Result = $"{msg}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RemoveCampaignSuperAdmin");
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Internal server error occurred while processing the request." },
                    Result = null
                };
            }
        }


        // Add this method to the existing StatusService class
        // Update the existing method in ProcessConfigurationServiceV4_1 class

        public async Task<APIResponse<CampaignProcessedRecordsCountResponse?>> GetCampaignProcessedRecordsCountAsync(CampaignProcessedRecordsCountRequest request)
        {
            try
            {
                // Validate request
                if (request == null || string.IsNullOrWhiteSpace(request.CampaignId))
                {
                    _logger.LogError("Invalid request: CampaignId is required");
                    return new APIResponse<CampaignProcessedRecordsCountResponse?>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "CampaignId is required" },
                        Result = null
                    };
                }

                // Validate date formats if provided
                if (!string.IsNullOrWhiteSpace(request.FromDate) && !IsValidDateFormat(request.FromDate))
                {
                    _logger.LogError("Invalid FromDate format: {FromDate}", request.FromDate);
                    return new APIResponse<CampaignProcessedRecordsCountResponse?>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid FromDate format. Please use YYYY-MM-DD or MM/DD/YYYY format" },
                        Result = null
                    };
                }

                if (!string.IsNullOrWhiteSpace(request.ToDate) && !IsValidDateFormat(request.ToDate))
                {
                    _logger.LogError("Invalid ToDate format: {ToDate}", request.ToDate);
                    return new APIResponse<CampaignProcessedRecordsCountResponse?>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid ToDate format. Please use YYYY-MM-DD or MM/DD/YYYY format" },
                        Result = null
                    };
                }

                string applicationId = ExtractApplicationIdFromToken();
                if (string.IsNullOrWhiteSpace(applicationId))
                {
                    _logger.LogWarning("Application ID could not be extracted from bearer token");
                    // You might want to return an error here if applicationId is mandatory
                    return new APIResponse<CampaignProcessedRecordsCountResponse?>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid ToDate format. Please use YYYY-MM-DD or MM/DD/YYYY format" },
                        Result = null
                    };
                }

                var result = await _processConfigurationRepository.GetCampaignProcessedRecordsCountAsync(applicationId,request);

                if (result == null)
                {
                    _logger.LogError("No data found for CampaignId: {CampaignId}", request.CampaignId);
                    return new APIResponse<CampaignProcessedRecordsCountResponse?>
                    {
                        ResultStatus = APIResultStatus.NoContent,
                        ResponseMessage = new List<string> { $"No data found for CampaignId: {request.CampaignId}" },
                        Result = null
                    };
                }

                return new APIResponse<CampaignProcessedRecordsCountResponse?>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing campaign records count request");
                return new APIResponse<CampaignProcessedRecordsCountResponse?>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "An error occurred while processing the request" },
                    Result = null
                };
            }
        }


        public async Task<APIResponse<CampaignProcessedRecordsCountResponseV2?>> GetCampaignProcessedRecordsCountAsyncV2(CampaignProcessedRecordsCountRequest request)
        {
            try
            {
                // Validate request
                if (request == null || string.IsNullOrWhiteSpace(request.CampaignId))
                {
                    _logger.LogError("Invalid request: CampaignId is required");
                    return new APIResponse<CampaignProcessedRecordsCountResponseV2?>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "CampaignId is required" },
                        Result = null
                    };
                }

                // Validate date formats if provided
                if (!string.IsNullOrWhiteSpace(request.FromDate) && !IsValidDateFormat(request.FromDate))
                {
                    _logger.LogError("Invalid FromDate format: {FromDate}", request.FromDate);
                    return new APIResponse<CampaignProcessedRecordsCountResponseV2?>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid FromDate format. Please use YYYY-MM-DD or MM/DD/YYYY format" },
                        Result = null
                    };
                }

                if (!string.IsNullOrWhiteSpace(request.ToDate) && !IsValidDateFormat(request.ToDate))
                {
                    _logger.LogError("Invalid ToDate format: {ToDate}", request.ToDate);
                    return new APIResponse<CampaignProcessedRecordsCountResponseV2?>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid ToDate format. Please use YYYY-MM-DD or MM/DD/YYYY format" },
                        Result = null
                    };
                }

                string applicationId = ExtractApplicationIdFromToken();
                if (string.IsNullOrWhiteSpace(applicationId))
                {
                    _logger.LogWarning("Application ID could not be extracted from bearer token");
                    // You might want to return an error here if applicationId is mandatory
                    return new APIResponse<CampaignProcessedRecordsCountResponseV2?>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid ToDate format. Please use YYYY-MM-DD or MM/DD/YYYY format" },
                        Result = null
                    };
                }

                var result = await _processConfigurationRepository.GetCampaignProcessedRecordsCountAsyncV2(applicationId, request);

                if (result == null)
                {
                    _logger.LogError("No data found for CampaignId: {CampaignId}", request.CampaignId);
                    return new APIResponse<CampaignProcessedRecordsCountResponseV2?>
                    {
                        ResultStatus = APIResultStatus.NoContent,
                        ResponseMessage = new List<string> { $"No data found for CampaignId: {request.CampaignId}" },
                        Result = null
                    };
                }

                return new APIResponse<CampaignProcessedRecordsCountResponseV2?>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing campaign records count request");
                return new APIResponse<CampaignProcessedRecordsCountResponseV2?>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "An error occurred while processing the request" },
                    Result = null
                };
            }
        }

        // Helper method to validate date format
        private bool IsValidDateFormat(string dateString)
        {
            return DateTime.TryParse(dateString, out _);
        }

        public async Task<APIResponse<LandingLayerUploadConfigurationDto>> GetLandingLayerUploadConfiguration()
        {
            try
            {
                var databaseResponse = await _processConfigurationRepository.GetLandingLayerUploadConfiguration();


                return await Task.FromResult(new APIResponse<LandingLayerUploadConfigurationDto>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = new LandingLayerUploadConfigurationDto
                    {
                        noOfAllowedFilesToUpload = databaseResponse.noOfAllowedFilesToUpload,
                        totalFileSize = databaseResponse.totalFileSize,
                    }
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return await Task.FromResult(new APIResponse<LandingLayerUploadConfigurationDto>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Unable to retrieve landing layer configurations." },
                    Result = new LandingLayerUploadConfigurationDto()
                });
            }
        }



        //public async Task<APIResponse<string>> LandingLayerOfflineModeInsertConfiguration(Models.DTOs.v1._0.FileConfiguration.InsertFlpConfigurationRequest insertFlpConfigurationRequest)
        //{
        //    string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");

        //    if (string.IsNullOrWhiteSpace(securityGroupId))
        //    {
        //        _logger.LogError("Error: Security group not found.");

        //        return new APIResponse<string>
        //        {
        //            ResultStatus = APIResultStatus.Failed,
        //            ResponseMessage = new List<string> { "Security group not found." },
        //            Result = null  // Return 0 as row count if no valid data is found
        //        };
        //    }

        //    //upsert security group table
        //    foreach (SecurityGroup securityGroup in insertFlpConfigurationRequest.securityGroups)
        //    {
        //        var retVal = await _processConfigRepositoryV4.AddSecurityGroup(securityGroup);
        //    }

        //    //insertFlpConfigurationRequest.SecurityGroupId = securityGroupId;
        //    // Fetch the row count from the repository (assumed to return an integer)
        //    insertFlpConfigurationRequest.FlpConfigurationId = await _processConfigurationRepository.InsertFlpConfigurationDetails(insertFlpConfigurationRequest);

        //    // Check if the result is valid (shouldn't be less than 0)
        //    if (insertFlpConfigurationRequest.FlpConfigurationId is null)
        //    {
        //        _logger.LogError("Error: FlpConfigurationId is null ");

        //        return new APIResponse<string>
        //        {
        //            ResultStatus = APIResultStatus.Failed,
        //            ResponseMessage = new List<string> { "FlpConfigurationId is null" },
        //            Result = null  // Return 0 as row count if no valid data is found
        //        };
        //    }

        //    // Handle the list of FileConfigurations (second model)
        //    foreach (var fileConfig in insertFlpConfigurationRequest.FileConfigurations)
        //    {

        //        int result = await _processConfigurationRepository.InsertFlpFileConfigurationDetails(fileConfig, insertFlpConfigurationRequest.FlpConfigurationId, insertFlpConfigurationRequest.CreatedBy);
        //        if (result < 1)
        //        {
        //            _logger.LogError("Error: FlpFileConfigurationDetails failed to insert");
        //        }
        //    }

        //    foreach (var tableMapping in insertFlpConfigurationRequest.ConfigurationTableMappings)
        //    {

        //        int result = await _processConfigurationRepository.InsertConfigurationTableMapping(tableMapping, insertFlpConfigurationRequest.FlpConfigurationId, insertFlpConfigurationRequest.CreatedBy);
        //        if (result < 1)
        //        {
        //            _logger.LogError("Error: ConfigurationTableMapping failed to insert");
        //        }
        //    }

        //    // Return success response with the row count as the result
        //    return new APIResponse<string>
        //    {
        //        ResultStatus = APIResultStatus.Completed,
        //        ResponseMessage = new List<string> { "Success" },
        //        Result = insertFlpConfigurationRequest.FlpConfigurationId // Return the row count
        //    };
        //}
        #endregion



        #region private helpers
        private string CreateColumns(List<ColumnNameDatatypeName> columnNameDatatypeNames)
        {
            List<string> columns = new List<string>();
            foreach (var columnNameDatatypeName in columnNameDatatypeNames)
            {

                switch (columnNameDatatypeName.DatatypeName)
                {
                    case "date":
                    case "time":
                    case "datetime":
                        columns.Add($"{columnNameDatatypeName.ColumnName}={columnNameDatatypeName.DatatypeName}|{columnNameDatatypeName.dateTimeFormatId}");
                        break;
                    default:
                        columns.Add($"{columnNameDatatypeName.ColumnName}={columnNameDatatypeName.DatatypeName}");
                        break;
                }
            }

            return String.Join(",", columns);
        }

        private string CreateFileDBColumnDataTypeMapping(List<ColumnNameDatatypeName> columnNameDatatypeNames)
        {
            List<string> columns = new List<string>();

            foreach (var column in columnNameDatatypeNames)
            {
                switch (column.DatatypeName)
                {
                    case "date":
                    case "time":
                    case "datetime":
                        if (column.dateTimeFormatId > 0)
                            columns.Add($"{column.DbColumnName}#{column.ColumnName}={column.DatatypeName}|{column.dateTimeFormatId}");
                        else
                            columns.Add($"{column.DbColumnName}#{column.ColumnName}=string");
                        break;
                    default:
                        columns.Add($"{column.DbColumnName}#{column.ColumnName}={column.DatatypeName}");
                        break;
                }
            }

            return String.Join(",", columns);
        }

        // Helper method to extract the blob name from the file URL
        private string GetBlobNameFromUrl(string fileUrl)
        {
            var uri = new Uri(fileUrl);
            return uri.AbsolutePath.TrimStart('/');
        }

        public async Task<APIResponse<string>> LandingLayerOfflineModuleInsertConfiguration(Models.DTOs.v1._0.FileConfiguration.InsertFlpConfigurationRequest insertFlpConfigurationRequest)
        {
            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");

            if (string.IsNullOrWhiteSpace(securityGroupId))
            {
                _logger.LogError("Error: Security group not found.");

                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Security group not found." },
                    Result = null  // Return 0 as row count if no valid data is found
                };
            }

            

            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    //upsert security group table
                    foreach (SecurityGroup securityGroup in insertFlpConfigurationRequest.securityGroups)
                    {
                        var retVal = await _processConfigRepositoryV4.AddSecurityGroup(securityGroup);
                    }

                    insertFlpConfigurationRequest.FlpConfigurationId = await _processConfigurationRepository.InsertFlpConfigurationDetails(insertFlpConfigurationRequest);

                    // Check if the result is valid(shouldn't be less than 0)
                    if (insertFlpConfigurationRequest.FlpConfigurationId is null)
                    {
                        _logger.LogError("Error: FlpConfigurationId is null ");

                        return new APIResponse<string>
                        {
                            ResultStatus = APIResultStatus.Failed,
                            ResponseMessage = new List<string> { "FlpConfigurationId is null" },
                            Result = null  // Return 0 as row count if no valid data is found
                        };
                    }

                    if (insertFlpConfigurationRequest.FileConfigurations[0].landingLayerRegex.Count > 0)
                    {

                        var retValRegex = await _processConfigurationRepository.InsertRegex(insertFlpConfigurationRequest.FileConfigurations[0].landingLayerRegex, insertFlpConfigurationRequest.FlpConfigurationId, insertFlpConfigurationRequest.CreatedBy);
                    }

                    if (insertFlpConfigurationRequest.FileConfigurations[0].landingLayerFileExtension.Count > 0)
                    {

                        var retValExtension = await _processConfigurationRepository.InsertLandingLayerFileExtension(insertFlpConfigurationRequest.FileConfigurations[0].landingLayerFileExtension, insertFlpConfigurationRequest.FlpConfigurationId, insertFlpConfigurationRequest.CreatedBy);
                    }

                    // Handle the list of FileConfigurations (second model)
                    foreach (var fileConfig in insertFlpConfigurationRequest.FileConfigurations)
                    {

                        int result = await _processConfigurationRepository.InsertFlpFileConfigurationDetails(fileConfig, insertFlpConfigurationRequest.FlpConfigurationId, insertFlpConfigurationRequest.CreatedBy);
                        if (result < 1)
                        {
                            _logger.LogError("Error: FlpFileConfigurationDetails failed to insert");
                        }
                    }
                    
                    foreach (var tableMapping in insertFlpConfigurationRequest.ConfigurationTableMappings)
                    {
                        _logger.LogInformation($"landing layer path {tableMapping.landingLayerAcceptedPath}");
                        int result = await _processConfigurationRepository.InsertConfigurationTableMapping(tableMapping, insertFlpConfigurationRequest.FlpConfigurationId, insertFlpConfigurationRequest.CreatedBy, insertFlpConfigurationRequest.dataSource);
                        if (result < 1)
                        {
                            _logger.LogError("Error: ConfigurationTableMapping failed to insert");
                        }
                    }

                    scope.Complete();
                }
                catch (Exception ex)
                {

                    _logger.LogError(ex, "Error inserting configuration: {Message}", ex.Message);
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "Error inserting configuration." },
                        Result = null
                    };
                }

            }
            return new APIResponse<string>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = insertFlpConfigurationRequest.FlpConfigurationId // Return the row count
            };
        }


        // Add this private method to extract applicationId from your encrypted token
        private string ExtractApplicationIdFromToken()
        {
            try
            {
                var authorizationHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();

                if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Authorization header not found or invalid format");
                    return null;
                }

                // The token is already decrypted by TokenDecryptionMiddleware
                var decryptedToken = authorizationHeader.Substring("Bearer ".Length).Trim();

                // Parse the JWT token
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadJwtToken(decryptedToken);

                // Try to get application ID from different claims in order of preference
                var applicationId =
                    // First try the custom client_id claim
                    jsonToken.Claims?.FirstOrDefault(c => c.Type == "client_id")?.Value ??
                    // Fall back to the name claim
                    jsonToken.Claims?.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value ??
                    // Last resort - direct "name" string
                    jsonToken.Claims?.FirstOrDefault(c => c.Type == "name")?.Value;

                if (!string.IsNullOrWhiteSpace(applicationId))
                {
                    _logger.LogInformation($"Successfully extracted ApplicationId: '{applicationId}'");
                }
                else
                {
                    _logger.LogWarning("Application ID not found in token claims");

                    // Debug: Log all available claims
                    _logger.LogDebug("Available claims:");
                    foreach (var claim in jsonToken.Claims)
                    {
                        _logger.LogDebug($"  - Type: '{claim.Type}', Value: '{claim.Value}'");
                    }
                }

                return applicationId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting application ID from token");
                return null;
            }
        }



        #endregion
    }
}