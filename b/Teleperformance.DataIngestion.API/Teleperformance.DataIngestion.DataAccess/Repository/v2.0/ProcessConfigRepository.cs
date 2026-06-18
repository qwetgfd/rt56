using Dapper;
using Microsoft.Extensions.Logging;
using NPOI.SS.Formula.Functions;
using System.Data;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v2._0.Authentication;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v2._0
{
    public class ProcessConfigRepository : IProcessConfigRepository
    {

        private readonly ILogger<ProcessConfigRepository> _logger;
        private readonly IDapperService _dapperService;

        public ProcessConfigRepository(ILogger<ProcessConfigRepository> logger, IDapperService dapperService)
        {
            _logger = logger;
            _dapperService = dapperService;
        }


        public async Task<IEnumerable<SecurityGroup>> GetSecurityGroups(string loginId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@loginId", loginId);
                

                var dbResponse = await _dapperService.GetMultipleRowsAsync<SecurityGroup>("[sel_SecurityGroupes]", dynamicParameters, commandType: CommandType.StoredProcedure);

                return dbResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<DatabaseResponse> CommitUserSecurityGroup(UserSelectedSecurityGroupDto userSelectedSecurityGroupDto)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@securityGroupId", userSelectedSecurityGroupDto.SecurityGroupId);
                dynamicParameters.Add("@loginId", userSelectedSecurityGroupDto.LoginId);
                dynamicParameters.Add("@userName", userSelectedSecurityGroupDto.UserName);
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_userSelectedSecurityGroup]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString());
                throw;
            }

        }

        public async Task<List<FileConfigurationEntity>> GetAllProcessNamesByLoginId(string securityGroupId, int fileProcessingServerTypeId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@securityGroupId", securityGroupId);
                dynamicParameters.Add("@fileProcessingServerTypeId", fileProcessingServerTypeId);

               // var dbResponse = await _dapperService.GetMultipleRowsAsync<FileConfigurationEntity>("[sel_flpConfiguration_ByLoginId]", dynamicParameters, commandType: CommandType.StoredProcedure);
                var dbResponse = await _dapperService.GetMultipleRowsAsync<FileConfigurationEntity>("[sel_ProcessNameDescription]", dynamicParameters, commandType: CommandType.StoredProcedure);

                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<List<FileConfigurationEntity>> GetAllProcessNamesByLoginIdByTerm(string queryTerm, string securityGroupId, int fileProcessingServerTypeId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@securityGroupId", securityGroupId);
                dynamicParameters.Add("@fileProcessingServerTypeId", fileProcessingServerTypeId);
                dynamicParameters.Add("@queryTerm", queryTerm);

                var dbResponse = await _dapperService.GetMultipleRowsAsync<FileConfigurationEntity>("[sel_flpConfiguration_ByLoginId_ByTerm]", dynamicParameters, commandType: CommandType.StoredProcedure);

                return dbResponse.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }
        public async Task<DSConfig?> GetDSConfiguration(int id)
        {
            var dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@id", id);
            using var con = _dapperService.CreateConnection();
            return await con.QueryFirstOrDefaultAsync<DSConfig?>("sel_dataSliceAPIConfig", dynamicParameters, commandType: CommandType.StoredProcedure);
        }
        public async Task<IEnumerable<SecurityGroupRegion>> GetRegionBySecurityGroupId(string securityGroupId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@securityGroupId", securityGroupId);

                var dbResponse = await _dapperService.GetMultipleRowsAsync<SecurityGroupRegion>("[sel_securityGroupRegion]", dynamicParameters, commandType: CommandType.StoredProcedure);
                
                return dbResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<IEnumerable<SGPageAccess>> GetUserRoles(string securityGroupId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@securityGroupId", securityGroupId);

                var dbResponse = await _dapperService.GetMultipleRowsAsync<SGPageAccess>("[sel_SGPageAccess]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<RefreshTokenEntity> CommitRefreshToken(RefreshToken refreshToken)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@id", refreshToken.Id);
                dynamicParameters.Add("@userName", refreshToken.userName);
                dynamicParameters.Add("@tokenHash", refreshToken.TokenHash);
                dynamicParameters.Add("@createdUtc", refreshToken.CreatedUtc);
                dynamicParameters.Add("@expiresUtc", refreshToken.ExpiresUtc);
                dynamicParameters.Add("@revoked", refreshToken.Revoked);
                dynamicParameters.Add("@revocationReason", refreshToken.RevocationReason);
                dynamicParameters.Add("@replacedById", refreshToken.ReplacedById);              
                
                var dbResponse = await _dapperService.GetSingleRowAsync<RefreshTokenEntity>("[commit_AddRefreshToken]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<string> GetRefreshToken(RefreshToken refreshToken)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@tokenHash", refreshToken.TokenHash);
                var dbResponse = await _dapperService.GetSingleRowAsync<string>("[sel_RefreshToken]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw; 
            }
        }

       
    }
}

