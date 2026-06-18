using Dapper;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.EIB;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v4._1;
using Teleperformance.DataIngestion.Models.Entities.v4._1.DataProfilling;
using Teleperformance.DataIngestion.Models.Entities.v4._1.EIB;
using static Dapper.SqlMapper;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v4._1
{
    public class EIBRepositoryV4_1 : IEIBRepositoryV4_1
    {
        private readonly ILogger<EIBRepositoryV4_1> logger;
        private readonly IDapperService dapperService;

        public EIBRepositoryV4_1(
            ILogger<EIBRepositoryV4_1> logger,
            IDapperService dapperService)
        {
            this.logger = logger;
            this.dapperService = dapperService;
        }

        public async Task<bool> CheckActiveEIBConfiguration(string EIBName)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@EIBName", EIBName);
                dynamicParameters.Add("ReturnValue", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);

                await dapperService.InsertDataAsync<bool>("[sel_CheckActiveEIBConfiguration]", dynamicParameters, commandType: CommandType.StoredProcedure);
                int dbResponse = dynamicParameters.Get<int>("ReturnValue");
                bool isActiveExists = dbResponse == 1;
                return isActiveExists;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<bool> GenerateEIB(string eibId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@EIBId", eibId);
                

                // Add return value parameter
                dynamicParameters.Add("ReturnValue", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);

                await dapperService.InsertDataAsync<int>("[commit_GenerateEIB]", dynamicParameters, commandType: CommandType.StoredProcedure);
                int dbResponse = dynamicParameters.Get<int>("ReturnValue");
                return dbResponse > 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<EIBConfigurationList> GetAllEIBs(EIBListRequestDto request)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@pageNumber", request.pageNumber);
                dynamicParameters.Add("@pageSize", request.pageSize);
                //dynamicParameters.Add("@processName",request.ProcessName);
                dynamicParameters.Add("@searchColumnName", request.searchOnColumn == "null" ? null : request.searchOnColumn);
                dynamicParameters.Add("@searchColumnValue", request.searchValue == "null" ? null : request.searchValue);
                dynamicParameters.Add("@fromDate", request.fromDate == "null" ? null : request.fromDate);
                dynamicParameters.Add("@toDate", request.toDate == "null" ? null : request.toDate);
                dynamicParameters.Add("@createdBy", request.createdBy);
                
                
                //dynamicParameters.Add("@securityGroupId", securityGroupId);
                dynamicParameters.Add("@totalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

                var dbResponse = await dapperService.GetMultipleRowsAsync<EIBConfigurationEntity>("[sel_EIB]", dynamicParameters, commandType: CommandType.StoredProcedure);
                int? totalCount = dynamicParameters.Get<int?>("@totalCount");

                return new EIBConfigurationList
                {
                    Response = dbResponse.ToList(),
                    TotalCount = totalCount
                };

               


            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<PDMProflingSPNames>> GetAllProfilingSP()
        {
            try
            {

                var dbResponse = await dapperService.GetMultipleRowsAsync<PDMProflingSPNames>("[sel_PDMProfilingSPNames]", null, commandType: CommandType.StoredProcedure);

                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<DBViewsResponseDto>> GetAllViews()
        {
            try
            {
                                
                var dbResponse = await dapperService.GetMultipleRowsAsync<DBViewsResponseDto>("[sel_views]", null, commandType: CommandType.StoredProcedure);
                
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<ProfilingSPConfiguration> GetConfigurationProfilingSP(int procedureNameId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@procedureNameId", procedureNameId);


                using var con = dapperService.CreateConnection();

                var dbResponse = await con.QueryMultipleAsync("[sel_ConfigurationProfilingSP]", dynamicParameters, commandType: CommandType.StoredProcedure);
                var sPConfiguration = dbResponse.Read<ProfilingSPConfiguration>().FirstOrDefault();
                if (sPConfiguration != null)
                {
                    sPConfiguration.proflingRunHistoryLogs = dbResponse.Read<ProflingRunHistoryLog>().ToList();
                    //fileConfig.FileColumnMapping = dbResponse.Read<FileColumnMapping>().ToList();      
                }


                return sPConfiguration;



            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<EIBCountryDto>> GetCountries()
        {
            try
            {

                var dbResponse = await dapperService.GetMultipleRowsAsync<EIBCountryDto>("[sel_EIBCountries]", null, commandType: CommandType.StoredProcedure);

                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<EIBConfigurationRequest> GetEIBByEIBId(string eibId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@eibId", eibId);


                using var con = dapperService.CreateConnection();

                var dbResponse = await con.QueryMultipleAsync("[sel_EIBByEIBId]", dynamicParameters, commandType: CommandType.StoredProcedure);
                var eibConfiguration = dbResponse.Read<EIBConfigurationRequest>().FirstOrDefault();

                if (eibConfiguration != null)
                {
                    eibConfiguration.businessProcessNames = dbResponse.Read<BusinessProcessNames>().ToList();
                    eibConfiguration.businessProcessDBViewMapping = dbResponse.Read<BusinessProcessDBViewMapping>().ToList();
                    eibConfiguration.businessProcessFileUrlMapping = dbResponse.Read<BusinessProcessFileUrlMapping>().ToList();
                    var nameMap = eibConfiguration.businessProcessNames.ToDictionary(x => x.businessProcessNameId, x => x.businessProcessName);
                    if(eibConfiguration.businessProcessDBViewMapping.Count > 0)
                    {
                        foreach (var item in eibConfiguration.businessProcessDBViewMapping)
                        {
                            if (nameMap.TryGetValue(item.businessProcessNameId, out var name))
                                item.businessProcessName = name;
                        }
                    }
                }

                return eibConfiguration;



            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<EIBGenerationStatus>> getEIBGenerationStatus(DateTime generationStartDateTime, DateTime generationEndDateTime)
        {
            try
            {

                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@generationStartDateTime", generationStartDateTime);
                dynamicParameters.Add("@generationEndDateTime", generationEndDateTime);

                var dbResponse = await dapperService.GetMultipleRowsAsync<EIBGenerationStatus>("[sel_EIBGenerationStatus]", dynamicParameters, commandType: CommandType.StoredProcedure);

                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<ProfilingLogs>> GetProfilingLogs(string runid, CancellationToken ct)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@runid", runid);                

                var dbResponse = await dapperService.GetMultipleRowsAsync<ProfilingLogs>("[sel_EIBGenerationStatus]", dynamicParameters, commandType: CommandType.StoredProcedure);

                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<int> InsertBusinessProcessDBViewMapping(BusinessProcessDBViewMapping request)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@BPNViewId", request.bpnViewId);
                dynamicParameters.Add("@businessProcessName", request.businessProcessName);
                dynamicParameters.Add("@businessProcessNameId", request.businessProcessNameId);
                dynamicParameters.Add("@columnCount", request.columnCount) ;                
                dynamicParameters.Add("@fromColumn", request.fromColumn);
                dynamicParameters.Add("@toColumn", request.toColumn);
                dynamicParameters.Add("@isActive", request.isActive);
                dynamicParameters.Add("@viewNameId", request.viewNameId);
                dynamicParameters.Add("@viewName", request.viewName);
                dynamicParameters.Add("@updatedBy", request.updatedBy);

                // Add return value parameter
                dynamicParameters.Add("ReturnValue", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);

                await dapperService.InsertDataAsync<int>("[commit_BusinessProcessNameDBViewMapping]", dynamicParameters, commandType: CommandType.StoredProcedure);
                int dbResponse = dynamicParameters.Get<int>("ReturnValue");
                return dbResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<int> InsertBusinessProcessName(BusinessProcessNames request, string EIBId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@EIBId", EIBId);
                dynamicParameters.Add("@businessProcessName", request.businessProcessName);
                dynamicParameters.Add("@isActive", request.isActive);
                dynamicParameters.Add("@isRequired", request.isRequired);
                dynamicParameters.Add("@createdBy", request.createdBy);
                dynamicParameters.Add("@updatedBy", request.updatedBy);
                dynamicParameters.Add("@businessProcessNameId", request.businessProcessNameId);
                dynamicParameters.Add("@fieldCount", request.fieldCount);

                // Add return value parameter
                dynamicParameters.Add("ReturnValue", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);

                await dapperService.InsertDataAsync<int>("[commit_BusinessProcessName]", dynamicParameters, commandType: CommandType.StoredProcedure);
                int dbResponse = dynamicParameters.Get<int>("ReturnValue");
                return dbResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<string> InsertEIBConfigurationDetails(EIBConfigurationRequest request)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@EIBId", "", direction : ParameterDirection.Output);
                dynamicParameters.Add("@EIBName", request.eibName);
                dynamicParameters.Add("@description", request.description);
                dynamicParameters.Add("@noOfBusinessProcess ", request.noOfBusinessProcess);
                if(request.updatedDateTime.Trim().Length > 0) dynamicParameters.Add("@updatedDateTime", request.updatedDateTime);

                dynamicParameters.Add("@createdBy", request.createdBy);
                dynamicParameters.Add("@updatedBy", request.updatedBy);
                dynamicParameters.Add("@isActive", request.isActive);
                dynamicParameters.Add("@configId", request.EIBId);
                dynamicParameters.Add("@countryId", request.countryId);
                var dbResponse = await dapperService.InsertDataAsync<string>("[commit_EIBConfiguration]", dynamicParameters, commandType: CommandType.StoredProcedure);
                                
                var newEIBID = dynamicParameters.Get<string>("@EIBId");

                return newEIBID; // Return the main response (if needed)
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<string> RegisterPDMProfilingSPRun(int procedureNameId, string processedBy)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@ProcessedBy", processedBy);
                dynamicParameters.Add("@ProcedureNameID", procedureNameId);


                // Add return value parameter
                //dynamicParameters.Add("ReturnValue", dbType: DbType.String, direction: ParameterDirection.ReturnValue);

                Guid runId = await dapperService.GetSingleRowAsync<Guid>("[commit_RegisterPDMProflingSPRun]", dynamicParameters, commandType: CommandType.StoredProcedure);
                //string dbResponse = dynamicParameters.Get<string>("ReturnValue");
                return runId.ToString();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<string> UpdateRunSatusAsync(string runId, string status)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@runId", runId);
                dynamicParameters.Add("@status", status);


                // Add return value parameter
                //dynamicParameters.Add("ReturnValue", dbType: DbType.String, direction: ParameterDirection.ReturnValue);

                string retVal = await dapperService.GetSingleRowAsync<string>("[commit_UpdatePDMProflingSPRunSatusAsync]", dynamicParameters, commandType: CommandType.StoredProcedure);
                //string dbResponse = dynamicParameters.Get<string>("ReturnValue");
                return retVal;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }
        }
    }
}
