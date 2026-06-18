using Aspose.Cells;
using Azure.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Dapper;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NLog;
using NPOI.HSSF.UserModel;
using NPOI.POIFS.NIO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v2._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Enums.v3._0;

namespace Teleperformance.DataIngestion.DataAccess.Services.v1._0
{
    public class ProcessConfigurationService : IProcessConfigurationService
    {
        private readonly IProcessConfigurationRepository _processConfigurationRepository;
        private readonly IProcessConfigRepositoryV4 processConfigRepositoryV4;
        private readonly IAdminRepository _adminRepository;
        private readonly IBlobStorageService _blobStorageService;
        private readonly ILogger<ProcessConfigurationService> _logger;
        private readonly IHeaderService _headerService;
        private Dictionary<string, ICellStyle> styleCache = new();

        public ProcessConfigurationService(
            IProcessConfigurationRepository processConfigurationRepository, 
            IProcessConfigRepositoryV4 processConfigRepositoryV4,
            ILogger<ProcessConfigurationService> logger, IAdminRepository adminRepository, IBlobStorageService blobStorageService, 
            IHeaderService headerService)
        {
            _processConfigurationRepository = processConfigurationRepository;
            this.processConfigRepositoryV4 = processConfigRepositoryV4;
            _logger = logger;
            _adminRepository = adminRepository;
            _blobStorageService = blobStorageService;
            _headerService = headerService;
        }

        public async Task<APIResponse<List<ProcessNamesDto>>> GetAllProcessNamesByLoginId(string loginid)
        {
            if (string.IsNullOrWhiteSpace(loginid))
            {
                _logger.LogError("Error: Missing loginId in request parameter.");
                return new APIResponse<List<ProcessNamesDto>>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Missing loginId" },
                    Result = null
                };
            }
            var csvConfigurations = await _processConfigurationRepository.GetAllProcessNamesByLoginId(loginid);// await _cSVConfigRepo.ListAsync(spec);
            if (csvConfigurations == null)
            {
                _logger.LogError("Error: Information not found.");

                return new APIResponse<List<ProcessNamesDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Information not found." },
                    Result = null
                };
            }

            List<ProcessNamesDto> listProcessNames = new List<ProcessNamesDto>();

            foreach (var item in csvConfigurations)
            {
                var processNames = new ProcessNamesDto { Id = item.Id, FLPConfigurationId = item.flpConfigurationId, ProcessNamesMore = item.process_name, ProcessNames = item.process_name };
                listProcessNames.Add(processNames);
            }

            return new APIResponse<List<ProcessNamesDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = listProcessNames
            };
        }

        public async Task<APIResponse<string>> CheckProcessNameExists(string processName, string configId)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                _logger.LogError("Error: Missing processName in request parameter.");
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Missing processName" },
                    Result = string.Empty
                };
            }
            var flpProcessName = await _processConfigurationRepository.VerifyProcessNameUnique(processName, configId); // _cSVConfigRepo.GetEntityWithSpec(spec);            

            if (string.IsNullOrEmpty(flpProcessName))
            {
                _logger.LogError("Error: Missing processName in request parameter.");
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "No response." },
                    Result = string.Empty
                };
            }

            return new APIResponse<string>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = flpProcessName
            };
        }

        public async Task<APIResponse<List<DataTypesDto>>> GetAllDataTypeNames()
        {
            try
            {
                var list = _processConfigurationRepository.GetAllDbDataTypes().GetAwaiter().GetResult().Select(x => new DataTypesDto() { DatatypeName = x.dataTypeName, DatatypeId = x.Id }).ToList();

                if (list == null)
                {
                    _logger.LogError("Error: DataType list not found.");
                    return new APIResponse<List<DataTypesDto>>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "DataType list not found." },
                        Result = null
                    };
                }
                return new APIResponse<List<DataTypesDto>>
                {
                    Result = list,
                    ResponseMessage = new List<string> { "Success" },
                    ResultStatus = APIResultStatus.Completed
                };


            }
            catch (Exception ex)
            {
                //TODO:
                this._logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<List<DateTimeFormatDto>>> GetDateTimeFormats(bool displayOnLandingLayer)
        {
            try
            {
                var list = _processConfigurationRepository.GetAllDbDateTimeFormats(displayOnLandingLayer).GetAwaiter().GetResult().Select(x => new DateTimeFormatDto() { FormatId = x.FormatId, DataTypeName = x.DataTypeName, Format = x.Format }).ToList();

                if (list == null)
                {
                    _logger.LogError("Error: DataType list not found.");
                    return new APIResponse<List<DateTimeFormatDto>>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "DateTime Format list not found." },
                        Result = null
                    };
                }
                return new APIResponse<List<DateTimeFormatDto>>
                {
                    Result = list,
                    ResponseMessage = new List<string> { "Success" },
                    ResultStatus = APIResultStatus.Completed
                };
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<List<DIRegionDto>>> GetAllDIRegions()
        {
            try
            {
                var retFromDb = await _processConfigurationRepository.GetAllDIRegionsAsync();

                if (retFromDb == null)
                {
                    _logger.LogError("Error: Region list not found.");
                    return new APIResponse<List<DIRegionDto>>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "Region list not found." },
                        Result = null
                    };
                }
                var regions =
                    retFromDb.Select(x => new DIRegionDto()
                    {
                        Id = x.Id,
                        Name = x.Name
                    }).ToList();

                return new APIResponse<List<DIRegionDto>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = regions
                };

            }
            catch (Exception ex)
            {
                //TODO:
                this._logger.LogError(ex, ex.Message);

                throw;
            }
        }

        public async Task<APIResponse<List<DISubRegionDto>>> GetAllDISubRegions()
        {
            try
            {
                var retFromDb = await _processConfigurationRepository.GetAllDISubRegionsAsync();
                if (retFromDb == null)
                {
                    _logger.LogError("Error: SubRegion list not found.");
                    return new APIResponse<List<DISubRegionDto>>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "SubRegion list not found." },
                        Result = null
                    };
                }
                var subRegions =
                    retFromDb.Select(x => new DISubRegionDto()
                    {
                        Id = x.Id,
                        Name = x.Name
                    }).ToList();

                return new APIResponse<List<DISubRegionDto>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = subRegions
                };

            }
            catch (Exception ex)
            {
                //TODO:
                this._logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<List<DIClientnamesDto>>> GetAllDIClientnames()
        {
            try
            {
                var retFromDb = await _processConfigurationRepository.GetAllDIClientnames();
                if (retFromDb == null)
                {
                    _logger.LogError("Error: Clients list not found.");
                    return new APIResponse<List<DIClientnamesDto>>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "Clients list not found." },
                        Result = null
                    };
                }
                var clientnames =
                    retFromDb.Select(x => new DIClientnamesDto()
                    {
                        Id = x.Id,
                        Name = x.Name
                    }).ToList();

                return new APIResponse<List<DIClientnamesDto>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = clientnames
                };

            }
            catch (Exception ex)
            {
                //TODO:
                this._logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<List<DIDatabaseNameDto>>> GetDatabaseNames(int regionId, string subRegionId, int clientNameId, int fileProcessingServerTypeId)
        {
            try
            {
                string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    _logger.LogError("Error: Security group not found.");
                    return new APIResponse<List<DIDatabaseNameDto>>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = null
                    };
                }
                var retFromDb = await _processConfigurationRepository.GetDIDatabaseNames(regionId, subRegionId, clientNameId, securityGroupId, fileProcessingServerTypeId);
                if (retFromDb == null)
                {
                    _logger.LogError("Error: Database list not found.");
                    return new APIResponse<List<DIDatabaseNameDto>>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "Database list not found." },
                        Result = null
                    };
                }
                var databaseNames =
                    retFromDb.Select(x => new DIDatabaseNameDto()
                    {
                        Id = x.Id,
                        DatabaseName = x.DatabaseName,
                        DatabaseServer = x.DatabaseServer,
                        DefaultDB = x.defaultDB
                    }).ToList();
                return new APIResponse<List<DIDatabaseNameDto>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = databaseNames
                };
            }
            catch (Exception ex)
            {
                //TODO:
                this._logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<FileConfigurationEntity>> GetConfigurationById(string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    _logger.LogError("Error: Missing configurationId in request parameter.");
                    return new APIResponse<FileConfigurationEntity>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Missing configurationId" },
                        Result = null
                    };
                }
                var retFromDb = await _processConfigurationRepository.GetFileConfigurationByIdAsync(id);
                if (retFromDb == null)
                {
                    _logger.LogError("Error: Configuration not found.");
                    return new APIResponse<FileConfigurationEntity>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "Configuration not found." },
                        Result = null
                    };
                }
                return new APIResponse<FileConfigurationEntity>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = retFromDb
                };
            }
            catch (Exception ex)
            {
                //TODO:
                this._logger.LogError(ex, ex.Message);
                throw;
            }
        }
        public async Task<APIResponse<IEnumerable<GetProcessTypeResponse?>>> GetProcessType()
        {


            // Fetch the row count from the repository (assumed to return an integer)
            var DIFrameworkUtilizationResponse = await _processConfigurationRepository.GetProcessType();

            // Check if the result is valid (shouldn't be less than 0)
            if (DIFrameworkUtilizationResponse is null)
            {
                _logger.LogError("Error: Process Type Response is null ");

                return new APIResponse<IEnumerable<GetProcessTypeResponse?>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Process Type Response is null" },
                    Result = null  // Return 0 as row count if no valid data is found
                };
            }

            // Log the successful execution
            _logger.LogInformation($"Process Type Response is null");

            // Return success response with the row count as the result
            return new APIResponse<IEnumerable<GetProcessTypeResponse?>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = DIFrameworkUtilizationResponse // Return the row count
            };

        }

        public async Task<APIResponse<FlpConvertParquetRequestDto>> InsertConfiguration(string json, IFormFile file, Stream stream, string loggedInUser, string userName)
        {
            FileValueRequest pagePartValueAPIRequest = new FileValueRequest();
           

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogError("Error: Configuration json not found.");
                return new APIResponse<FlpConvertParquetRequestDto>
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
                return new APIResponse<FlpConvertParquetRequestDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Security group not found." },
                    Result = null
                };
            }

            var jsonData = JsonSerializer.Deserialize<FileConfigurationDto>(json);

            if (jsonData.additionalSettings != null && jsonData.columnNameDatatypeNames.Count() > 0)
            {

                if (jsonData.additionalSettings.ignore_duplicate_rows == false)
                {

                }
                var csvConfiguration = new FileConfigurationEntity
                {
                    flpConfigurationId = jsonData.additionalSettings.flpConfigurationId,
                    process_name = jsonData.additionalSettings.processName,
                    Description = jsonData.additionalSettings.description,

                    delimiter = jsonData.additionalSettings.delimiter,
                    is_header_provided = jsonData.additionalSettings.flexCheckHasHeaders,
                    //TODO: flexCheckSkipEmptyLines
                    //TODO: txtEscapeCharacter
                    column_name_list = jsonData.additionalSettings.column_name_list, // String.Join(",", jsonData.columnNameDatatypeNames.Select(x => x.ColumnName)),
                    db_file_columnName_list = CreateFileDBColumnDataTypeMapping(jsonData.columnNameDatatypeNames),                    
                    quote_character = jsonData.additionalSettings.txtQuoteCharacter,
                    order_by_column_list_for_dedup = jsonData.additionalSettings.order_by_column_list_for_dedup,
                    convert_datatypes_column_list = CreateColumns(jsonData.columnNameDatatypeNames), //String.Join(",", jsonData.columnNameDatatypeNames.Select(x => x.DatatypeName)),
                    is_active = jsonData.additionalSettings.is_active,
                    do_not_archive_file = jsonData.additionalSettings.do_not_archive_file,
                    spanish_to_english=jsonData.additionalSettings.spanish_to_english,
                    roman_numerals_only = jsonData.additionalSettings.roman_numerals_only,
                    ignore_duplicate_rows = jsonData.additionalSettings.ignore_duplicate_rows,
                    key_column_list = String.Join(",", jsonData.columnNameDatatypeNames.Where(w => w.ColumnKey == true).Select(x => x.ColumnName)),
                    keep_first_row = jsonData.additionalSettings.keep_first_row,
                    skip_rows = jsonData.additionalSettings.skip_header_rows,
                    skip_footer_rows = jsonData.additionalSettings.skip_footer_rows,
                    skip_empty_lines = jsonData.additionalSettings.flexCheckSkipEmptyLines,
                    database_name = jsonData.additionalSettings.databaseName,
                    table_name = jsonData.additionalSettings.tableName, 
                    validate_fileschema = jsonData.additionalSettings.validate_fileschema,
                    drop_history_table = jsonData.additionalSettings.drop_history_table,
                    drop_main_table = jsonData.additionalSettings.drop_main_table,
                    created_date = DateTime.UtcNow,
                    current_timestamp = DateTime.UtcNow.ToString(),
                    loginid = loggedInUser,
                    created_by = loggedInUser,
                    userName = userName,

                    RegionId = jsonData.additionalSettings.RegionId,
                    SubRegionId = jsonData.additionalSettings.SubRegionId,
                    ClientId = jsonData.additionalSettings.ClientId,
                    databaseConfigurationId = jsonData.additionalSettings.databaseConfigurationId,
                    securityGroupId = securityGroupId,
                    sender_communication_email = jsonData.additionalSettings.sender_communication_email,
                    region = jsonData.additionalSettings.region,
                    subRegion = jsonData.additionalSettings.subRegion,
                    clientName = jsonData.additionalSettings.clientName,
                    mergeData = jsonData.additionalSettings.mergeData,
                    createHistoryTable = jsonData.additionalSettings.createHistoryTable,

                    deltaJobId = jsonData.additionalSettings.deltaJobId,
                    dataSource = jsonData.additionalSettings.dataSource,
                    deltaStorageAccountId = jsonData.additionalSettings.deltaStorageAccountId,
                    deltaContainerName = jsonData.additionalSettings.deltaContainerName,
                    deltaSource = jsonData.additionalSettings.deltaSource, 
                    securityGroups = jsonData.additionalSettings.securityGroups
                };

                //upsert security group table
                foreach(SecurityGroup securityGroup in jsonData.additionalSettings.securityGroups)
                {
                    var retVal = await processConfigRepositoryV4.AddSecurityGroup(securityGroup);
                }

                if (string.IsNullOrEmpty(jsonData.additionalSettings.flpConfigurationId))
                {
                    csvConfiguration.flpConfigurationId = Guid.NewGuid().ToString();
                    var retVal = await _processConfigurationRepository.InsertFileConfiguration(csvConfiguration);
                    csvConfiguration.flpConfigurationId = csvConfiguration.flpConfigurationId;

                    //var configurationRegionMapping = new ConfigurationRegionMapping
                    //{
                    //    flpConfigurationId = csvConfiguration.flpConfigurationId,
                    //    RegionId = Convert.ToInt32(csvConfiguration.RegionId),
                    //    SubRegionId = Convert.ToInt32(csvConfiguration.SubRegionId),
                    //    ClientId = Convert.ToInt32(csvConfiguration.ClientId),
                    //    databaseConfigurationId = csvConfiguration.databaseConfigurationId,
                    //    CreatedBy = csvConfiguration.loginid,
                    //    CreationDateTime = DateTime.UtcNow,
                    //    ModificationDateTime = null,
                    //    IsActive = true
                    //};

                    //var retValFromInsertConfigurationRegionMapping = await _processConfigurationRepository.InsertConfigurationRegionMapping(configurationRegionMapping);
                }
                else
                {
                    //update the convert_datatypes_column_list and column_name_list
                    var retFromDb = await _processConfigurationRepository.UpdateColumnNameList(jsonData.additionalSettings.flpConfigurationId, "", csvConfiguration.column_name_list);
                    retFromDb = await _processConfigurationRepository.UpdateConvertDatatypesColumnList(jsonData.additionalSettings.flpConfigurationId, "", csvConfiguration.convert_datatypes_column_list);
                    retFromDb = await _processConfigurationRepository.UpdateFlpFileColumnMapping(jsonData.additionalSettings.flpConfigurationId, "", csvConfiguration.db_file_columnName_list);

                }

                string fileName = "";
                string fileURL = "";
                bool status = false;
                string containerName = "";
                string message = "Unable to store the file.";
                int retUploadFileId = 0;

                bool isCopiedFile = false;
                FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();

                if (file.Length > 0)
                {
                    if (file.FileName.EndsWith("parquet"))
                    {
                        fileName = $"{csvConfiguration.process_name}/parquet/{file.FileName.Trim()}";
                    }
                    else if (file.FileName.EndsWith("csv") || file.FileName.EndsWith("txt") || file.FileName.EndsWith("xls") || file.FileName.EndsWith("xlsx") || file.FileName.EndsWith("xlsb"))
                    {
                        fileName = $"{csvConfiguration.process_name}/csv_files/{file.FileName.Trim()}";
                    }

                    //containerName = await adminRepository.GetContainerName();

                    string destinationContainerName = "";
                    try
                    {
                        //var stream = file.OpenReadStream();

                        string ext = System.IO.Path.GetExtension(fileName).ToLower();

                        //TODO: this should be dynamic
                        //KeyVault.GetKeyVaultValue("BlobConnectionString").Result;

                        string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(KeyVault.GetKeyVaultValue("TPTempAzureStorageAccountNameFileUpload").Result, KeyVault.GetKeyVaultValue("TPTempAzureStorageAccountKeyFileUpload").Result);

                        destinationContainerName = await _adminRepository.GetContainerName(); //destinationStorageAccountDto.StorageContainerName;
                        string destinationBlobUrl = fileName;// $"{flpConfigurationRequestDto.DestinationPath}temp/{currentTimeString}/";
                                                             //string destinationBlobName = $"{destinationBlobUrl}{backUpFileName}";

                        BlobClient destinationBlobClient = _blobStorageService.GetBlobClientDetails(destinationBlobUrl, destinationBlobConnectionString, destinationContainerName);

                        (isCopiedFile, flpProcessTempFile) = await new BlobStorageService().CopyFileStreamToDestinationBlobAsync(stream, destinationBlobClient);//sourceBlobClient, destinationBlobClient);

                        pagePartValueAPIRequest = new FileValueRequest
                        {
                            UploadFileId = Guid.NewGuid().ToString(),
                            FileName = file.FileName.Trim(),
                            AddedBy = csvConfiguration.loginid,
                            DateTimeUploaded = DateTime.UtcNow.ToString(),
                            FlpConfigurationId = csvConfiguration.flpConfigurationId,
                            FlpProceeAttempt = "0",
                            FlpProcessStatusId = "0"
                        };

                        retUploadFileId = await _adminRepository.InsertFile(pagePartValueAPIRequest);

                    }
                    catch (Exception ex)
                    {
                        this._logger.LogError(ex, ex.Message);
                        //if there's an upload
                        return new APIResponse<FlpConvertParquetRequestDto>
                        {
                            ResultStatus = APIResultStatus.Failed,
                            ResponseMessage = new List<string> { "Invalid File." },
                            Result = new FlpConvertParquetRequestDto()
                            {
                                ProcessName = csvConfiguration.process_name,
                                FlpConfigurationId = csvConfiguration.flpConfigurationId,
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

                    return new APIResponse<FlpConvertParquetRequestDto>
                    {
                        Result = new FlpConvertParquetRequestDto()
                        {
                            ProcessName = csvConfiguration.process_name,
                            FlpConfigurationId = csvConfiguration.flpConfigurationId,
                            BlobClients = new BlobClientDetails()
                            {
                                Uri = flpProcessTempFile.Uri,
                                AccountName = "tpusadevelopmenttest", //todo: hardcode as of the moment
                                BlobContainerName = destinationContainerName,
                                CanGenerateSasUri = true,
                                Name = flpProcessTempFile.Uri.Substring(flpProcessTempFile.Uri.IndexOf(csvConfiguration.process_name)),
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
                    return new APIResponse<FlpConvertParquetRequestDto>
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

                return new APIResponse<FlpConvertParquetRequestDto>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Additional settings not provided." },
                    Result = null
                };
            }
        }

        public async Task<APIResponse<FlpProcessConfigurationResponse>> GetFlpProcessConfigurationList(FlpProcessConfigurationListRequest request)
        {
            try
            {
                request.securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");

                var databaseResponse = await _processConfigurationRepository.GetFlpProcessConfigurationListAsync(request);

                if (databaseResponse != null)
                {
                    return await Task.FromResult(new APIResponse<FlpProcessConfigurationResponse>
                    {
                        ResultStatus = APIResultStatus.Completed,
                        ResponseMessage = new List<string> { "Success" },
                        Result = databaseResponse
                    });
                }
                else
                {
                    _logger.LogError("No Data Found");
                    return await Task.FromResult(new APIResponse<FlpProcessConfigurationResponse>
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
                return await Task.FromResult(new APIResponse<FlpProcessConfigurationResponse>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Internal Server Error" },
                    Result = null
                });
            }
        }

        public async Task<APIResponse<IEnumerable<FileServerDetailsResponse?>>> GetfileServerDetails()
        {


            // Fetch the row count from the repository (assumed to return an integer)
            var DIFrameworkUtilizationResponse = await _processConfigurationRepository.GetfileServerDetails();

            // Check if the result is valid (shouldn't be less than 0)
            if (DIFrameworkUtilizationResponse is null)
            {
                _logger.LogError("Error: Server Details Response is null ");

                return new APIResponse<IEnumerable<FileServerDetailsResponse?>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Server Details Response is null" },
                    Result = null  // Return 0 as row count if no valid data is found
                };
            }

            // Log the successful execution
            _logger.LogInformation($"Server Details Response is null");

            // Return success response with the row count as the result
            return new APIResponse<IEnumerable<FileServerDetailsResponse?>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = DIFrameworkUtilizationResponse // Return the row count
            };

        }

        public async Task<APIResponse<IEnumerable<StorageAccountDetailsResponse?>>> GetstorageAccountDetails(int fileProcessingServerTypeId)
        {


            // Fetch the row count from the repository (assumed to return an integer)
            var DIFrameworkUtilizationResponse = await _processConfigurationRepository.GetstorageAccountDetails(fileProcessingServerTypeId);

            // Check if the result is valid (shouldn't be less than 0)
            if (DIFrameworkUtilizationResponse is null)
            {
                _logger.LogError("Error: Storage Account Details Response is null ");

                return new APIResponse<IEnumerable<StorageAccountDetailsResponse?>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Storage Account Details Response is null" },
                    Result = null  // Return 0 as row count if no valid data is found
                };
            }

            // Log the successful execution
            _logger.LogInformation($"Storage Account Details Response is null");

            // Return success response with the row count as the result
            return new APIResponse<IEnumerable<StorageAccountDetailsResponse?>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = DIFrameworkUtilizationResponse // Return the row count
            };

        }

        public async Task<APIResponse<string>> InsertFlpConfigurationDetails(InsertFlpConfigurationRequest insertFlpConfigurationRequest)
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

            //upsert security group table
            foreach (SecurityGroup securityGroup in insertFlpConfigurationRequest.securityGroups)
            {
                var retVal = await processConfigRepositoryV4.AddSecurityGroup(securityGroup);
            }

            //insertFlpConfigurationRequest.SecurityGroupId = securityGroupId;
            // Fetch the row count from the repository (assumed to return an integer)
            insertFlpConfigurationRequest.FlpConfigurationId = await _processConfigurationRepository.InsertFlpConfigurationDetails(insertFlpConfigurationRequest);

            // Check if the result is valid (shouldn't be less than 0)
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

                int result = await _processConfigurationRepository.InsertConfigurationTableMapping(tableMapping, insertFlpConfigurationRequest.FlpConfigurationId, insertFlpConfigurationRequest.CreatedBy);
                if (result < 1)
                {
                    _logger.LogError("Error: ConfigurationTableMapping failed to insert");
                }
            }
            /*foreach (var day in insertFlpConfigurationRequest.weekDays)
            {
                int result = await _processConfigurationRepository.InsertCustomSchedulerDetails(insertFlpConfigurationRequest.FlpConfigurationId, insertFlpConfigurationRequest.hourFrequency, day);
                if (result < 1)
                {
                    _logger.LogError("Error: CustomSchedulerDetails failed to insert");
                }
            }*/

            // Return success response with the row count as the result
            return new APIResponse<string>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = insertFlpConfigurationRequest.FlpConfigurationId // Return the row count
            };

        }
        public async Task<APIResponse<ProcessConfigDetail?>> GetConfigurationDetailsById(string flpConfigurationId,string? tabName)
        {
            var response = await _processConfigurationRepository.GetConfigurationDetailsById(flpConfigurationId, tabName);
            if (response == null)
            {
                return new APIResponse<ProcessConfigDetail?>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = ["Process Configuration Details Response is null"],
                    Result = null
                };
            }
            else
            {
                return new APIResponse<ProcessConfigDetail?>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = ["Success"],
                    Result = response
                };
            }

        }
        public async Task<APIResponse<IEnumerable<ScheduerType?>>> GetSchedulerType()
        {
            var schedulerTypes = await _processConfigurationRepository.GetScheduerType();
            if (schedulerTypes == null)
            {
                return new APIResponse<IEnumerable<ScheduerType?>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = ["Scheduler Type Response is null"],
                    Result = null
                };
            }
            return new APIResponse<IEnumerable<ScheduerType?>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = schedulerTypes
            };
        }
        public async Task<APIResponse<IEnumerable<WeekDayName?>>> GetWeekDayName()
        {
            var weekDays = await _processConfigurationRepository.GetWeekDayName();
            if (weekDays == null)
            {
                return new APIResponse<IEnumerable<WeekDayName?>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = ["Week Days Response is null"]
                };
            }
            return new APIResponse<IEnumerable<WeekDayName?>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = weekDays
            };
        }
        public async Task<APIResponse<IEnumerable<FrequencyHour?>>> GetFrequencyHour()
        {
            var frequencyHours = await _processConfigurationRepository.GetFrequencyHour();
            if (frequencyHours == null)
            {
                return new APIResponse<IEnumerable<FrequencyHour?>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = ["Frequency Hours Response is null"]
                };
            }
            return new APIResponse<IEnumerable<FrequencyHour?>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = frequencyHours
            };
        }

        public async Task<APIResponse<string>> GenerateSasKey()
        {
            var blobSasBuilder = new BlobSasBuilder
            {
                BlobContainerName = "",
                BlobName = "",
                Resource = "b", //b for blob, c for container
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(10)
            };

            blobSasBuilder.SetPermissions(BlobAccountSasPermissions.Write | BlobAccountSasPermissions.Create);

            var sasToken = blobSasBuilder.ToSasQueryParameters(
                new Azure.Storage.StorageSharedKeyCredential(
                    KeyVault.GetKeyVaultValue("TPTempAzureStorageAccountNameFileUpload").Result,
                    KeyVault.GetKeyVaultValue("TPTempAzureStorageAccountKeyFileUpload").Result
                    )
                ).ToString();

            var containerName = await _adminRepository.GetContainerName();


            var sasUrl = $"https://{KeyVault.GetKeyVaultValue("TPTempAzureStorageAccountNameFileUpload").Result}.blob.core.windows.net/{containerName}/temp/{sasToken}";

            return new APIResponse<string>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = sasUrl
            };
        }

        public async Task<APIResponse<string>> UploadToBlobTemp(Stream stream, string fileName)
        {
            var stopWatch = Stopwatch.StartNew();
            string destinationContainerName = "";
            bool isCopiedFile = false;
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();
            try
            {
                //generate a unique filename in blob so we can delete later
                //but how
                fileName = "online_temp/" + Guid.NewGuid().ToString() + "_" + fileName;
                string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(KeyVault.GetKeyVaultValue("TPTempAzureStorageAccountNameFileUpload").Result, KeyVault.GetKeyVaultValue("TPTempAzureStorageAccountKeyFileUpload").Result);

                destinationContainerName = await _adminRepository.GetContainerName(); //destinationStorageAccountDto.StorageContainerName;
                string destinationBlobUrl = fileName;// $"{flpConfigurationRequestDto.DestinationPath}temp/{currentTimeString}/";
                                                     //string destinationBlobName = $"{destinationBlobUrl}{backUpFileName}";


                // 1. Configure BlobClientOptions with extended NetworkTimeout
                var blobClientOptions = new BlobClientOptions
                {
                    Retry =
                        {
                            NetworkTimeout = TimeSpan.FromMinutes(10), // ⬅️ Increase timeout here
                            MaxRetries = 8,
                            Delay = TimeSpan.FromSeconds(2),
                            MaxDelay = TimeSpan.FromSeconds(30),
                            Mode = RetryMode.Exponential
                    }
                };

                // 2. Create BlobServiceClient using the options
                var blobServiceClient = new BlobServiceClient(destinationBlobConnectionString, blobClientOptions);

                // 3. Get the container and blob client
                var containerClient = blobServiceClient.GetBlobContainerClient(destinationContainerName);
                var destinationBlobClient = containerClient.GetBlobClient(fileName);

                // 4. Configure upload transfer options
                var uploadOptions = new BlobUploadOptions
                {
                    TransferOptions = new StorageTransferOptions
                    {
                        MaximumConcurrency = 16,
                        InitialTransferSize = 32 * 1024 * 1024, // 16 MiB
                        MaximumTransferSize = 32 * 1024 * 1024   // 8 MiB
                    }
                };
                this._logger.LogInformation($"upload has starts");

                // 5. Upload the file stream with both options in play
                await destinationBlobClient.UploadAsync(stream, uploadOptions);
                stopWatch.Stop();
                Console.WriteLine($"upload completed in {stopWatch.Elapsed.TotalSeconds} seconds");
                this._logger.LogInformation($"upload completed in {stopWatch.Elapsed.TotalSeconds} seconds");
                //BlobClient destinationBlobClient = blobStorageService.GetBlobClientDetails(destinationBlobUrl, destinationBlobConnectionString, destinationContainerName);               

                //(isCopiedFile, flpProcessTempFile) = await new BlobStorageService().CopyFileStreamToDestinationBlobAsync(stream, destinationBlobClient);//sourceBlobClient, destinationBlobClient);

                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "File uploaded" },
                    Result = fileName
                };

            }
            catch (Exception ex)
            {
                //this.logger.LogError(ex, ex.Message);
                //if there's an upload
                this._logger.LogError(ex.Message);
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "File not uploaded" },
                    Result = "File not uploaded"
                };
                throw;
            }
        }

        public async Task<APIResponse<ConvertToXLSXDto>> GetConvertedXLSX()
        {
            var stopWatch = Stopwatch.StartNew();
            try
            {
                string fileName = _headerService.GetHeaderValue("fileName");
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    //logger.LogError("Error: File not found not found.");
                    return new APIResponse<ConvertToXLSXDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "File not found" },
                        Result = null
                    };
                }

                string fileExt = _headerService.GetHeaderValue("fileExt");
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    //logger.LogError("Error: \"File Extension not found.");
                    return new APIResponse<ConvertToXLSXDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "File Extension not found" },
                        Result = null
                    };
                }

                string fileLocationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(KeyVault.GetKeyVaultValue("TPTempAzureStorageAccountNameFileUpload").Result, KeyVault.GetKeyVaultValue("TPTempAzureStorageAccountKeyFileUpload").Result);
                string containerName = await _adminRepository.GetContainerName();
                BlobServiceClient blobServiceClient = new BlobServiceClient(fileLocationBlobConnectionString);
                BlobContainerClient csvContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
                BlobClient txtBlobClient = csvContainerClient.GetBlobClient(fileName);

                using (var txtStream = await txtBlobClient.OpenReadAsync())
                {
                    stopWatch.Stop();
                    Console.WriteLine($"file downloaded from blob in {stopWatch.Elapsed.TotalSeconds} seconds");
                    stopWatch.Start();
                    var res = ConvertToXLSX(null, txtStream, fileExt);
                    Console.WriteLine($"file converted to XLSX in {stopWatch.Elapsed.TotalSeconds} seconds");
                    stopWatch.Stop();
                    return new APIResponse<ConvertToXLSXDto>
                    {
                        ResultStatus = APIResultStatus.Completed,
                        ResponseMessage = new List<string> { "File Converted" },
                        Result = new ConvertToXLSXDto
                        {
                            fileData = res.Result.Result.fileData,
                            rowCount = res.Result.Result.rowCount
                        }
                    };
                }

            }
            catch (Exception)
            {
                return new APIResponse<ConvertToXLSXDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "File not converted" },
                    Result = null
                };

            }
        }

        public async Task<APIResponse<ConvertToXLSXDto>> ConvertToXLSX(IFormFile file, Stream stream, string fileExt)
        {
            using var outputStream = new MemoryStream();
            int maxRowsPerSheet = 10000;
            int totalRowsInTheSheet = 0;

            try
            {


                if (fileExt == "xls")
                {

                    var hssfWorkbook = new HSSFWorkbook(stream); // Read .xls                    
                    var xssfWorkbook = new XSSFWorkbook();       // Create .xlsx            

                    int sheetIndex = 0;

                    for (int sheetNum = 0; sheetNum < hssfWorkbook.NumberOfSheets; sheetNum++)
                    {
                        var sheet = hssfWorkbook.GetSheetAt(sheetNum);
                        int totalRows = Math.Min(sheet.LastRowNum + 1, maxRowsPerSheet); // Cap at 10,000
                        var newSheet = xssfWorkbook.CreateSheet(sheet.SheetName); // Keep original sheet name

                        totalRowsInTheSheet = hssfWorkbook.GetSheetAt(0).LastRowNum + 1;
                        //apply dummy style to force styles.xml creation
                        var dummyStyle = xssfWorkbook.CreateCellStyle();
                        dummyStyle.WrapText = false;

                        for (int i = 0; i < totalRows; i++)
                        {
                            var oldRow = sheet.GetRow(i);
                            if (oldRow == null) continue;

                            var newRow = newSheet.CreateRow(i);
                            foreach (var oldCell in oldRow.Cells)
                            {
                                var newCell = newRow.CreateCell(oldCell.ColumnIndex);
                                newCell.CellStyle = dummyStyle;
                                CopyCellValue(oldCell, newCell);
                            }
                        }
                    }

                    //using var outputStream = new MemoryStream();
                    xssfWorkbook.Write(outputStream);


                    //return await Task.FromResult(new APIResponse<byte[]>
                    //{
                    //    ResultStatus = APIResultStatus.Completed,
                    //    ResponseMessage = new List<string>(),
                    //    Result = outputStream.ToArray() // Single workbook as byte array
                    //});
                }
                else
                {
                    DateTime startTime = DateTime.UtcNow;
                    var stopwatch = Stopwatch.StartNew();
                    this._logger.LogInformation($"ConvertToXLSX has started {startTime:HH:mm:ss.ffff}");
                    // Aspose.Cells logic for .xlsb
                    var sourceWorkbook = new Aspose.Cells.Workbook(stream); // Load .xlsb from stream
                    DateTime endTime = DateTime.UtcNow;
                    stopwatch.Stop();
                    this._logger.LogInformation($"ConvertToXLSX var sourceWorkbook = new Aspose.Cells.Workbook(stream); {endTime:HH:mm:ss.ffff}");
                    this._logger.LogInformation($"ConvertToXLSX Aspose.Cells.Workbook(stream) elapsed:{stopwatch.Elapsed.TotalMilliseconds}");
                    var targetWorkbook = new Aspose.Cells.Workbook(); // Create new .xlsx workbook
                    targetWorkbook.Worksheets.Clear(); // remove default worksheet

                    //get the total number of rows
                    var sheet1 = sourceWorkbook.Worksheets[0];
                    totalRowsInTheSheet = sheet1.Cells.MaxDataRow + 1;

                    //using var outputStream = new MemoryStream();
                    //sourceWorkbook.Save(outputStream, Aspose.Cells.SaveFormat.Xlsx); // Save as .xlsx
                    //convertedBytes = outputStream.ToArray();

                    stopwatch = Stopwatch.StartNew();
                    startTime = DateTime.UtcNow;
                    this._logger.LogInformation($"ConvertToXLSX var sourceWorkbook = new Aspose.Cells.Workbook(stream); {startTime:HH:mm:ss.ffff}");
                    foreach (Aspose.Cells.Worksheet sourceSheet in sourceWorkbook.Worksheets)
                    {
                        var targetSheet = targetWorkbook.Worksheets.Add(sourceSheet.Name);

                        int rowCount = Math.Min(sourceSheet.Cells.MaxDataRow + 1, maxRowsPerSheet);
                        int colCount = sourceSheet.Cells.MaxDataColumn + 1;

                        for (int row = 0; row < rowCount; row++)
                        {
                            for (int col = 0; col < colCount; col++)
                            {
                                var cell = sourceSheet.Cells[row, col];
                                if (cell != null && cell.Type != CellValueType.IsNull)
                                {
                                    if (cell.Type == CellValueType.IsDateTime || IsDateFormatted(cell))
                                    {
                                        DateTime dateValue = cell.DateTimeValue;
                                        Style sourceStyle = cell.GetStyle();
                                        Style targetStyle = targetSheet.Cells[row, col].GetStyle();
                                        targetStyle.Copy(sourceStyle);
                                        targetSheet.Cells[row, col].SetStyle(targetStyle);
                                        targetSheet.Cells[row, col].PutValue(dateValue);
                                    }
                                    else
                                    {
                                        targetSheet.Cells[row, col].PutValue(cell.Value);
                                    }

                                }
                            }
                        }
                    }
                    endTime = DateTime.UtcNow;
                    stopwatch.Stop();
                    this._logger.LogInformation($"ConvertToXLSX done loop all worksheet; {endTime:HH:mm:ss.ffff}");
                    this._logger.LogInformation($"ConvertToXLSX done loop all worksheet elapsed; {stopwatch.Elapsed.TotalMilliseconds}");
                    stopwatch.Start();
                    targetWorkbook.Save(outputStream, Aspose.Cells.SaveFormat.Xlsx);
                    stopwatch.Stop();
                    this._logger.LogInformation($"ConvertToXLSX done writing to stream; {stopwatch.Elapsed.TotalMilliseconds}");
                    

                }
                var ret = new ConvertToXLSXDto
                {
                    fileData = Convert.ToBase64String(outputStream.ToArray()),
                    rowCount = totalRowsInTheSheet
                };
                return await Task.FromResult(new APIResponse<ConvertToXLSXDto>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string>(),
                    Result = ret // Single workbook as byte array
                });
            }
            catch (Exception ex)
            {
                this._logger.LogInformation("ConvertToXLXS:" + ex.Message);
                throw ex;
            }


        }

        

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
                        columns.Add($"{column.DbColumnName}#{column.ColumnName}={column.DatatypeName}|{column.dateTimeFormatId}");
                        break;
                    default:
                        columns.Add($"{column.DbColumnName}#{column.ColumnName}={column.DatatypeName}");
                        break;
                }
            }

            return String.Join(",", columns);
        }



        private static bool IsDateFormatted(Cell cell)
        {
            Style style = cell.GetStyle();
            return style.Number >= 14 && style.Number <= 22; // Excel's built-in date formats
        }

        private void CopyCellValue(ICell oldCell, ICell newCell)
        {



            try
            {
                switch (oldCell.CellType)
                {
                    case CellType.String:
                        newCell.SetCellValue(oldCell.StringCellValue);
                        break;
                    case CellType.Numeric:
                        if (HSSFDateUtil.IsCellDateFormatted(oldCell))
                        {
                            var dateValue = oldCell.DateCellValue;
                            ((XSSFCell)newCell).SetCellValue(dateValue);

                            var originalFormatIndex = oldCell.CellStyle.DataFormat;
                            var originalFormatString = oldCell.Sheet.Workbook.CreateDataFormat().GetFormat(originalFormatIndex);
                            var formatString = oldCell.CellStyle.GetDataFormatString();

                            var workbook = newCell.Sheet.Workbook as XSSFWorkbook;
                            if (!styleCache.TryGetValue(originalFormatString, out var cachedStyle))
                            {
                                var dateStyle = workbook.CreateCellStyle();
                                var format = workbook.CreateDataFormat();
                                dateStyle.DataFormat = format.GetFormat(originalFormatString);

                                styleCache[originalFormatString] = dateStyle;
                                cachedStyle = dateStyle;
                            }

                            newCell.CellStyle = cachedStyle;


                        }
                        else
                        {
                            newCell.SetCellValue(oldCell.NumericCellValue);
                        }
                        break;
                    case CellType.Boolean:
                        newCell.SetCellValue(oldCell.BooleanCellValue);
                        break;
                    case CellType.Formula:
                        newCell.SetCellFormula(oldCell.CellFormula);
                        break;
                    case CellType.Error:
                        newCell.SetCellErrorValue(oldCell.ErrorCellValue);
                        break;
                    case CellType.Blank:
                        newCell.SetCellType(CellType.Blank);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }

        }

        




        #endregion
    }
}
