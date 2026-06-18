using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v1._0
{
    public class AdminRepository : IAdminRepository
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<AdminRepository> logger;
        private readonly IDapperService dapperService;

        public AdminRepository(IConfiguration configuration, ILogger<AdminRepository> logger, IDapperService dapperService)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.dapperService = dapperService;
        }
        public async Task<string> GetContainerName()
        {
            try
            {
                var dbResponse = await dapperService.GetSingleRowAsync<string>("[sel_containername]", null, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<int> InsertEIBFile(EIBFileValueRequest request)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();

                dynamicParameters.Add("@uploadFileId", request.UploadFileId);                
                dynamicParameters.Add("@fileName", request.FileName);
                dynamicParameters.Add("@EIBId", request.EIBId);
                dynamicParameters.Add("@creationDateTime", request.DateTimeUploaded);
                dynamicParameters.Add("@uploadedBy", request.AddedBy);
                dynamicParameters.Add("@uploadedByName", request.AddedByName);
                dynamicParameters.Add("@isActive", true);

                // Add return value parameter
                dynamicParameters.Add("ReturnValue", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);

                await dapperService.InsertDataAsync<int>("[add_EIBFile]", dynamicParameters, commandType: CommandType.StoredProcedure);
                int dbResponse = dynamicParameters.Get<int>("ReturnValue");
                return dbResponse;
            }
            catch (Exception ex)
            {

                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<int> InsertFile(FileValueRequest fileValueAPIRequest)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();

                dynamicParameters.Add("@fileName", fileValueAPIRequest.FileName);
                dynamicParameters.Add("@backupFileName", "");
                dynamicParameters.Add("@flpConfigurationId", fileValueAPIRequest.FlpConfigurationId);
                dynamicParameters.Add("@CreationDateTime", fileValueAPIRequest.DateTimeUploaded);
                dynamicParameters.Add("@UploadedBy", fileValueAPIRequest.AddedBy);
                dynamicParameters.Add("@FlpProcessStatusId", fileValueAPIRequest.FlpProcessStatusId);
                dynamicParameters.Add("@FlpProceeAttempt", fileValueAPIRequest.FlpProceeAttempt);
                dynamicParameters.Add("@uploadFileId", fileValueAPIRequest.UploadFileId);

                var dbResponse = await dapperService.InsertDataAsync<int>("[add_File]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                logger.LogError(ex, ex.Message);
                throw; 
            }
        }
    }
}
