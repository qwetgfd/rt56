using Azure.Storage.Blobs;
using Dapper;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using NPOI.SS.UserModel;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService;
using Teleperformance.DataIngestion.Models.DTOs.v4._1;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.EIB;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v4._1.EIB;
//using Teleperformance.DataIngestion.Models.Entities.v4._1.EIB;

namespace Teleperformance.DataIngestion.DataAccess.Services.v4._1
{
    public class EIBServiceV4_1 : IEIBServiceV4_1
    {
        private readonly ILogger<EIBServiceV4_1> logger;
        private readonly IHeaderService headerService;
        private readonly IEIBRepositoryV4_1 EIBRepository;
        private readonly IAdminRepository adminRepository;
        private readonly IBlobStorageService blobStorageService;
        private readonly IFlpProcessingServiceV4_1 flpProcessingServiceV4_1;
        private readonly IBackgroundTaskQueue backgroundTaskQueue;

        public EIBServiceV4_1(
            ILogger<EIBServiceV4_1> logger,
            IHeaderService headerService,
            IEIBRepositoryV4_1 EIBRepository,
            IAdminRepository adminRepository,
            IBlobStorageService blobStorageService, IFlpProcessingServiceV4_1 flpProcessingServiceV4_1,
            IBackgroundTaskQueue backgroundTaskQueue)
        {
            this.logger = logger;
            this.headerService = headerService;
            this.EIBRepository = EIBRepository;
            this.adminRepository = adminRepository;
            this.blobStorageService = blobStorageService;
            this.flpProcessingServiceV4_1 = flpProcessingServiceV4_1;
            this.backgroundTaskQueue = backgroundTaskQueue;
        }

        public async Task<APIResponse<bool>> CheckActiveEIBConfiguration(string EIBName)
        {
            try
            {
                string securityGroupId = headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    logger.LogError("Error: Security group not found.");
                    return new APIResponse<bool>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = false
                    };
                }

                var retFromDb = await EIBRepository.CheckActiveEIBConfiguration(EIBName);

                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Check Active EIB Configuration" },
                    Result = retFromDb
                };
            }
            catch (Exception ex)
            {
                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Unable to Check Active EIB Configuration" },
                    Result = false
                };
                throw;
            }
        }

        //todo: create a strongly typed return dto
        public async Task<APIResponse<bool>> GenerateEIB(string eibId)
        {
            try
            {
                string securityGroupId = headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    logger.LogError("Error: Security group not found.");
                    return new APIResponse<bool>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = false
                    };
                }

                var retFromDb = await EIBRepository.GenerateEIB(eibId);
                if (retFromDb)
                {
                    var token = flpProcessingServiceV4_1.GetBearerToken();
                    var obEibTemplateServiceURL = new EIBTemplateServiceHelpers(logger);
                    var res = obEibTemplateServiceURL.GenerateEIBTemplate(eibId, token);
                    //var response = await EIBTemplateServiceHelpers.GenerateEIBTemplate(eibId, token);
                    //if(response?.ResultStatus.Code != APIResultStatus.Completed.Code)
                    //{
                    //    return new APIResponse<bool>
                    //    {
                    //        ResultStatus = APIResultStatus.Error,
                    //        ResponseMessage = new List<string> { "something went wrong" },
                    //        Result = retFromDb
                    //    };
                    //}
                }


                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Started EIB Generation" },
                    Result = retFromDb
                };

            }
            catch (Exception ex)
            {
                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Unable to Generate EIB" },
                    Result = false
                };
                throw;
            }
        }

        public async Task<APIResponse<EIBResponseDto>> GetAllEIBs(EIBListRequestDto request)
        {
            try
            {
                string securityGroupId = headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    logger.LogError("Error: Security group not found.");
                    return new APIResponse<EIBResponseDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = null
                    };
                }

                var retFromDb = await EIBRepository.GetAllEIBs(request);

                var list = retFromDb.Response.Select(c => new EIBListResponse
                {
                    EIBId = c.EIBId,
                    EIBName = c.EIBName,
                    description = c.description,
                    createdBy = c.createdBy,
                    creationDateTime = c.creationDateTime,
                    modifiedDateTime = c.modifiedDateTime,
                    generationEndDateTime = c.generationEndDateTime,
                    generationStartDateTime = c.generationStartDateTime,
                    updatedBy = c.updatedBy,
                    mappedCount = c.mappedCount,
                    status = c.status,
                    fileUrl = c.fileURL
                }).ToList();

                var response = new EIBResponseDto
                {
                    Response = list,
                    TotalCount = retFromDb.TotalCount

                };

                return new APIResponse<EIBResponseDto>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "No found." },
                    Result = response
                };
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<DBSPsResponseDto>> GetCurrentStatusOfPDMSP(int procedureNameId)
        {
            try
            {
                //basic validation
                if (procedureNameId <= 0)
                {
                    return new APIResponse<DBSPsResponseDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid parameters." },
                        Result = null
                    };
                }

                string securityGroupId = headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    logger.LogError("Error: Security group not found.");
                    return new APIResponse<DBSPsResponseDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = null
                    };
                }

                //load profiling sp configuration
                var profilingSPConfiguration = await EIBRepository.GetConfigurationProfilingSP(procedureNameId);

                if (profilingSPConfiguration is null ||
                            string.IsNullOrWhiteSpace(profilingSPConfiguration.spName))
                {
                    logger.LogError("Profiling SP configuration not found for procedureNameId={ProcedureNameId}.", procedureNameId);
                    return new APIResponse<DBSPsResponseDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Profiling SP configuration not found." },
                        Result = null
                    };
                }

                return new APIResponse<DBSPsResponseDto>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Security group not found." },
                    Result = new DBSPsResponseDto
                    {
                        id = procedureNameId,
                        runId = profilingSPConfiguration.runId,
                        sPName = profilingSPConfiguration.spName,
                        description = profilingSPConfiguration.description,
                        LatestStatus = profilingSPConfiguration.latestStatus,
                        profilingRunHistoryLog = profilingSPConfiguration.proflingRunHistoryLogs
                    }
                };

            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<List<DBSPsResponseDto>>> GetAllProfilingSP()
        {
            try
            {
                string securityGroupId = headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    logger.LogError("Error: Security group not found.");
                    return new APIResponse<List<DBSPsResponseDto>>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = null
                    };
                }

                var retFromDb = await EIBRepository.GetAllProfilingSP();

                return new APIResponse<List<DBSPsResponseDto>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "PDM SP found." },
                    Result = retFromDb.Select(r => new DBSPsResponseDto
                    {
                        id = r.id,
                        sPName = r.sPName,
                        description = r.description,
                        LatestStatus = r.LatestStatus,
                        createdBy = r.createdBy,
                        insertedAt = r.insertedAt
                    }).ToList()
                };

            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<List<DBViewsResponseDto>>> GetAllViews()
        {
            try
            {
                string securityGroupId = headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    logger.LogError("Error: Security group not found.");
                    return new APIResponse<List<DBViewsResponseDto>>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = null
                    };
                }

                var retFromDb = await EIBRepository.GetAllViews();

                return new APIResponse<List<DBViewsResponseDto>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "DB Views found." },
                    Result = retFromDb.Select(r => new DBViewsResponseDto
                    {
                        viewId = r.viewId,
                        viewName = r.viewName,
                        columnCount = r.columnCount,
                    }).ToList()
                };

            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<List<EIBCountry>>> GetCountries()
        {
            try
            {
                string securityGroupId = headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    logger.LogError("Error: Security group not found.");
                    return new APIResponse<List<EIBCountry>>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = null
                    };
                }

                var retFromDb = await EIBRepository.GetCountries();

                return new APIResponse<List<EIBCountry>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "DB Views found." },
                    Result = retFromDb.Select(r => new EIBCountry
                    {
                        id = r.id,
                        countryName = r.countryName,
                        description = r.description,
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<EIBConfigurationRequest>> GetEIBByEIBId(string eibId)
        {
            try
            {
                string securityGroupId = headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    logger.LogError("Error: Security group not found.");
                    return new APIResponse<EIBConfigurationRequest>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = null
                    };
                }

                var retFromDb = await EIBRepository.GetEIBByEIBId(eibId);

                if (retFromDb != null)
                {
                    return new APIResponse<EIBConfigurationRequest>
                    {
                        ResultStatus = APIResultStatus.Completed,
                        ResponseMessage = new List<string> { "EIB Configuration Found." },
                        Result = retFromDb
                    };
                }

                return new APIResponse<EIBConfigurationRequest>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "EIB Configuration Not Found." },
                    Result = null
                };

            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<List<EIBGenerationStatusDto>>> getEIBGenerationStatus(DateTime generationStartDateTime, DateTime generationEndDateTime, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(1000, cancellationToken);

                string securityGroupId = headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    logger.LogError("Error: Security group not found.");
                    return new APIResponse<List<EIBGenerationStatusDto>>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = null
                    };
                }

                var retFromDb = await EIBRepository.getEIBGenerationStatus(generationStartDateTime, generationEndDateTime);

                return new APIResponse<List<EIBGenerationStatusDto>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = retFromDb.Select(r => new EIBGenerationStatusDto
                    {
                        EIBId = r.EIBId,
                        status = r.status,
                        fileURL = r.fileURL,
                        hasActiveFileURL = r.hasActiveFileURL,
                        errorMessage = r.errorMessage,
                        generationStartDateTime = r.generationStartDateTime
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<string>> GetEIBRequiredBPKeyword()
        {
            try
            {
                var EIBRequiredStringIdentifier = KeyVault.GetKeyVaultValue("EIBRequiredString").Result;
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = EIBRequiredStringIdentifier
                };
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<bool>> InsertEIBDetails(string jsonRequest, IFormFile? file, Stream? stream, string userName, string ntID)
        {
            try
            {
                string securityGroupId = headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    logger.LogError("Error: Security group not found.");
                    return new APIResponse<bool>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = false
                    };
                }

                //deserialize the json
                var jsonData = JsonSerializer.Deserialize<EIBConfigurationRequest>(jsonRequest);

                if (!jsonData.businessProcessNames.Any() || !jsonData.businessProcessDBViewMapping.Any())
                {
                    return new APIResponse<bool>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "No Business Process and Mapping found." },
                        Result = false
                    };
                }
                //insert and fetch eib
                string newEIBId = null;
                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {

                        var EIBConfigurationDetails = new EIBConfigurationRequest
                        {
                            EIBId = jsonData.configurationId,
                            description = jsonData.description,
                            noOfBusinessProcess = jsonData.noOfBusinessProcess,
                            updatedDateTime = jsonData.updatedDateTime,
                            createdBy = jsonData.createdBy,
                            updatedBy = userName,
                            isActive = jsonData.isActive,
                            eibName = jsonData.eibName,
                            countryId = jsonData.countryId,
                        };

                        newEIBId = await EIBRepository.InsertEIBConfigurationDetails(EIBConfigurationDetails);

                        if (newEIBId is null)
                        {
                            this.logger.LogError("Error: EIBConfigurationId is null");

                            return new APIResponse<bool>
                            {
                                ResultStatus = APIResultStatus.Failed,
                                ResponseMessage = new List<string> { "EIB Configuration ID Is null" },
                                Result = false
                            };
                        }

                        foreach (var bp in jsonData.businessProcessNames)
                        {
                            bp.updatedBy = userName;
                            int result = await EIBRepository.InsertBusinessProcessName(bp, newEIBId);
                            if (result < 1)
                            {
                                this.logger.LogError("Error: Business process Name Details failed to insert");
                            }
                        }

                        foreach (var bpvw in jsonData.businessProcessDBViewMapping)
                        {
                            int result = await EIBRepository.InsertBusinessProcessDBViewMapping(bpvw);
                            if (result < 1)
                            {
                                this.logger.LogError("Error: Business process name and DB View Mapping failed to insert");
                            }
                        }

                        scope.Complete();

                    }
                    catch (Exception ex)
                    {

                        this.logger.LogError(ex, "Error inserting EIB configuration: {Message}", ex.Message);
                        return new APIResponse<bool>
                        {
                            ResultStatus = APIResultStatus.Failed,
                            ResponseMessage = new List<string> { "Error inserting EIB configuration." },
                            Result = false
                        };
                    }
                }

                //insert the file in azure storage
                if (file?.Length > 0)
                {
                    string destinationContainerName = "";
                    string fileName = string.Empty;
                    bool isCopiedFile = false;
                    int retUploadFileId = 0;

                    FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();
                    if (file.FileName.EndsWith("xlsx"))
                    {
                        fileName = $"{newEIBId}/eib_files/{file.FileName.Trim()}";
                    }

                    string destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(KeyVault.GetKeyVaultValue("TPTempAzureStorageAccountNameFileUpload").Result, KeyVault.GetKeyVaultValue("TPTempAzureStorageAccountKeyFileUpload").Result);
                    destinationContainerName = await adminRepository.GetContainerName();
                    string destinationBlobUrl = fileName;

                    BlobClient destinationBlobClient = blobStorageService.GetBlobClientDetails(destinationBlobUrl, destinationBlobConnectionString, destinationContainerName);

                    (isCopiedFile, flpProcessTempFile) = await new BlobStorageService().CopyFileStreamToDestinationBlobAsync(stream, destinationBlobClient);//sourceBlobClient, destinationBlobClient);

                    var EIBFileValueRequest = new EIBFileValueRequest
                    {
                        UploadFileId = Guid.NewGuid().ToString(),
                        AddedBy = ntID,
                        AddedByName = userName,
                        DateTimeUploaded = DateTime.UtcNow.ToString(),
                        EIBId = newEIBId,
                        FileName = file.FileName.Trim()
                    };

                    retUploadFileId = await adminRepository.InsertEIBFile(EIBFileValueRequest);

                    //todo, return is -1
                    if (retUploadFileId < 1)
                    {

                        logger.LogError("Error: Unable to upload EIB file.");
                        return new APIResponse<bool>
                        {
                            ResultStatus = APIResultStatus.Failed,
                            ResponseMessage = new List<string> { "Unable to upload eib file." },
                            Result = false
                        };
                    }
                }

                return new APIResponse<bool>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "EIB Configuration Details Added/Updated" },
                    Result = true
                };
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<APIResponse<string>> RegisterPDMRProfilingSPRun(int procedureNameId, string processedBy, CancellationToken ct)
        {
            try
            {
                //basic validation
                if (procedureNameId <= 0 || string.IsNullOrWhiteSpace(processedBy))
                {
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid parameters." },
                        Result = ""
                    };
                }


                // header validation
                string securityGroupId = headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    logger.LogError("Error: Security group not found.");
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = ""
                    };
                }

                //register the PDM Profiling SP Run and return the runId                
                var runIdString = await EIBRepository.RegisterPDMProfilingSPRun(procedureNameId, processedBy);


                if (string.IsNullOrWhiteSpace(runIdString))
                {
                    logger.LogError("RegisterPDMProfilingSPRun returned an empty runId.");
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "Unable to create profiling run (empty runId)." },
                        Result = ""
                    };
                }

                if (!Guid.TryParse(runIdString, out Guid runId))
                {
                    logger.LogError("Invalid runId returned from DB. Value: {RunIdString}", runIdString);
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "Invalid run identifier returned from DB." },
                        Result = ""
                    };
                }


                //load profiling sp configuration
                var profilingSPConfiguration = await EIBRepository.GetConfigurationProfilingSP(procedureNameId);


                if (profilingSPConfiguration is null ||
                            string.IsNullOrWhiteSpace(profilingSPConfiguration.spName))
                {
                    logger.LogError("Profiling SP configuration not found for procedureNameId={ProcedureNameId}.", procedureNameId);
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Profiling SP configuration not found." },
                        Result = ""
                    };
                }

                //get DB configuration of the SP
                string secretName = profilingSPConfiguration.databaseConnectionSecret;
                if (string.IsNullOrWhiteSpace(secretName))
                {

                    logger.LogError("databaseConnectionSecret is missing for procedureNameId={ProcedureNameId}.", procedureNameId);
                    return new APIResponse<string>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Database connection secret is missing." },
                        Result = ""
                    };

                }
                string connectionString = !string.IsNullOrWhiteSpace(profilingSPConfiguration.databaseConnectionSecret) ?
                        KeyVault.GetKeyVaultValue(profilingSPConfiguration.databaseConnectionSecret)?.Result ?? "" : string.Empty;

                await backgroundTaskQueue.QueueAsync(async (ct) =>
                {
                    var status = await ExecutePDMProfilingSP(connectionString, runIdString, profilingSPConfiguration.spName, ct);

                    await UpdateRunSatusAsync(runIdString, status);
                });

                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "PDM Profiling SP Run registered" },
                    Result = runIdString
                };

            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, ex.Message);
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Unable to register/run PDM Profiling SP Run" },
                    Result = ""
                };
            }
        }

        public async Task<APIResponse<List<ProfilingLogs>>> SendPDMProflingSatusBySPId(int procedureNameId, string runId, CancellationToken cancellationToken, int lastSeenId = 0)
        {
            try
            {

                //basic validation
                if (procedureNameId <= 0 || string.IsNullOrWhiteSpace(runId))
                {
                    return new APIResponse<List<ProfilingLogs>>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid Parameters" },
                        Result = null
                    };
                }

                // header validation
                string securityGroupId = headerService.GetHeaderValue("x-tpdi-api-sg");

                if (string.IsNullOrWhiteSpace(securityGroupId))
                {
                    logger.LogError("Error: Security group not found.");
                    return new APIResponse<List<ProfilingLogs>>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Security group not found." },
                        Result = null
                    };
                }

                //load profiling sp configuration
                var profilingSPConfiguration = await EIBRepository.GetConfigurationProfilingSP(procedureNameId);

                if (string.IsNullOrWhiteSpace(profilingSPConfiguration.databaseConnectionSecret))
                {

                    logger.LogError("databaseConnectionSecret is missing for procedureNameId={ProcedureNameId}.", procedureNameId);
                    return new APIResponse<List<ProfilingLogs>>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Database connection secret is missing." },
                        Result = null
                    };

                }
                string connectionString = !string.IsNullOrWhiteSpace(profilingSPConfiguration.databaseConnectionSecret) ?
                        KeyVault.GetKeyVaultValue(profilingSPConfiguration.databaseConnectionSecret)?.Result ?? "" : string.Empty;

                var retFromDb = await GetProfilingLogs(connectionString, runId, cancellationToken, lastSeenId);

                return new APIResponse<List<ProfilingLogs>>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = retFromDb.Any() ? retFromDb.Select(r => new ProfilingLogs
                    {
                        Id = r.Id,
                        RunId = r.RunId,
                        runningStatus = "running", //always send running
                        //ProcessedId = r.ProcessedId,
                        InsertedAt = r.InsertedAt,
                        EndedAt = r.EndedAt,
                        rule = r.rule,
                        reason = r.reason,
                        status = r.status,
                        Total = r.Total
                        //Description = r.Description
                    }).ToList() : new List<ProfilingLogs> {
                        new ProfilingLogs { runningStatus = profilingSPConfiguration.latestStatus}
                    }

                };
            }
            catch (Exception ex)
            {

                throw;
            }
        }


        private static async Task<string?> ExecutePDMProfilingSP(string connectionString, string runId, string spName, CancellationToken ct)
        {

            if (!Guid.TryParse(runId, out var runGuid))
                throw new ArgumentException("Parameter 'runId' must be a valid GUID.", nameof(runId));


            try
            {

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(spName, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandTimeout = 60 * 15; //15 minutes
                        // Add parameters
                        command.Parameters.Add(new SqlParameter("@runId", SqlDbType.UniqueIdentifier)
                        {
                            Value = runGuid
                        });

                        // Execute the SP (no return expected)
                        var rows = await command.ExecuteScalarAsync(ct);

                        return rows?.ToString();
                    }
                }
            }
            catch (Exception)
            {
                // Preserve the original exception stack trace
                return "ended";
                
            }
        }

        private async Task<APIResponse<string>> UpdateRunSatusAsync(string runId, string status)
        {
            try
            {
                var retVal = await EIBRepository.UpdateRunSatusAsync(runId, status);

                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Successfully updated run status" },
                    Result = retVal
                };


            }
            catch (Exception ex)
            {

                this.logger.LogError(ex, ex.Message);
                return new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Unable to update run status" },
                    Result = ""
                };
            }
        }


        private async Task<List<ProfilingLogs>> GetProfilingLogs(
            string connectionString,
            string runId,            
            CancellationToken cancellationToken,
            int? lastSeendId)
        {
            if (!Guid.TryParse(runId, out var runGuid))
                throw new ArgumentException("runId must be a valid GUID.", nameof(runId));

            #region old code
            //        // Choose UTC or local to match your InsertedAt semantics.
            //        var startAt = DateTime.UtcNow;
            //        var endAt = startAt.AddMinutes(5);

            //        const string sql = @"



            //    SELECT TOP (10)
            //           id,
            //           RunId,
            //           ProcessedId,
            //           InsertedAt,
            //           EndedAt,
            //           Description
            //    FROM dbo.di_ProfilingLogs WITH (READPAST) -- optional hint to skip locked rows
            //    WHERE RunId = @RunId
            //      AND InsertedAt <= DATEADD(MINUTE, 10, GETUTCDATE()) --get records past 10 the current time

            //    ORDER BY InsertedAt DESC, id DESC; -- deterministic ordering
            //";

            //        await using var connection = new SqlConnection(connectionString);
            //        await connection.OpenAsync(cancellationToken);

            //        var command = new CommandDefinition(
            //            sql,
            //            new { RunId = runGuid},
            //            commandTimeout: 30,
            //            cancellationToken: cancellationToken);

            //        var rows = await connection.QueryAsync<ProfilingLogs>(command);
            //        return rows.AsList(); 
            #endregion
            try
            {
                await using var connection = new SqlConnection(connectionString);
                var command = new CommandDefinition(
                    commandText: "dbo.sel_proflingLogs",
                    parameters: new { RunId = runGuid, lastSeenId = lastSeendId },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30,
                    cancellationToken: cancellationToken);

                var rows = await connection.QueryAsync<ProfilingLogs>(command);
                return rows.AsList();
            }
            catch (Exception ex)
            {

                throw;
            }
            


        }


    }
}
