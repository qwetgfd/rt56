using Dapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Account;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v2._0.Authentication;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v3._0
{
        
    public class ProcessConfigRepositoryV3 : IProcessConfigRepositoryV3
    {
        private readonly ILogger<ProcessConfigRepositoryV3> logger;
        private readonly IDapperService dapperService;

        public ProcessConfigRepositoryV3(ILogger<ProcessConfigRepositoryV3> logger, IDapperService dapperService)
        {
            this.logger = logger;
            this.dapperService = dapperService;
        }

        public async Task<List<EnglishCharactersEquivalents>> GetEnglishEquivalents(string language)
        {
        
            try
            {
                //var dynamicParameters = new DynamicParameters();
                //dynamicParameters.Add("@spanishWordToConvert", spanishWordToConvert);

                //var dbResponse = await dapperService.GetSingleRowAsync<string>("[sel_SecurityGroupes]", dynamicParameters, commandType: CommandType.StoredProcedure);
                //return dbResponse;

                //if (string.IsNullOrEmpty(spanishWordToConvert))
                //{
                //    return spanishWordToConvert;
                //}

              

                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@language", "Spanish");

                var dbResponse = await dapperService.GetMultipleRowsAsync<EnglishCharactersEquivalents>("[sel_EnglishCharEquivalents]", dynamicParameters, commandType: CommandType.StoredProcedure);

                return dbResponse.ToList();
                //return result.Normalize(NormalizationForm.FormC);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }

        }



        public async Task<IEnumerable<SecurityGroupMapping>> GetSecurityGroupMappingsAsync()
        {
            try
            {
                var dbResponse = await dapperService.GetMultipleRowsAsync<SecurityGroupMapping>("[sel_securityGroupesMapping]", null, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }

        }


        public async Task<UpdateLoginResponseDto> UpdateLogin(string loginId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@loginId", loginId);
                var dbResponse = await dapperService.GetSingleRowAsync<UpdateLoginResponseDto>("[commit_updateLogin]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
                throw;
            }

        }
        //TODO: DataBricks
        //comment: create new SP and tables and entries
        public async Task<List<DIDatabaseNames>> GetDIDeltaDatabaseNames(int regionId, string subRegionId, int clientNameId, string securityGroupId)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@regionId", regionId);
                dynamicParameters.Add("@subRegionId", subRegionId);
                dynamicParameters.Add("@clientNameId", clientNameId);
                dynamicParameters.Add("@securityGroupId", securityGroupId);

                var dbResponse = await dapperService.GetMultipleRowsAsync<DIDatabaseNames>("[sel_DatabaseName]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse.ToList();
            }
            catch (Exception ex)
            {

                logger.LogError(ex, ex.Message);
                throw;
            }

        }

        public async Task<EnableDisableProcessByFlpConfigurationIdResponseDto> EnableDisableProcessByConfigurationId(string flpConfigurationIds, string userName, string created_by, bool activeStatus)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationIds", flpConfigurationIds);
                dynamicParameters.Add("@userName", userName);
                dynamicParameters.Add("@created_by", created_by);
                dynamicParameters.Add("@is_active", activeStatus);                

                
                var dbResponse = await dapperService.GetSingleRowAsync<EnableDisableProcessByFlpConfigurationIdResponseDto>("[commit_EnableDisableProcessByFlpConfigurationId]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {

                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<RevokedTokenEntity> RevokeAsync(string jti, DateTimeOffset expiresAt, CancellationToken ct = default)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@jti", jti);
                dynamicParameters.Add("@expiresAt", expiresAt);

                var dbResponse = await dapperService.GetSingleRowAsync<RevokedTokenEntity>("[commit_RevokedToken_Revoke]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {

                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public async Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@jti", jti);

                var dbResponse = await dapperService.GetSingleRowAsync<RevokedTokenEntity>("[commit_RevokedToken_IsRevoked]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse.Result.ToLower() == "true" ? true : false;
            }
            catch (Exception ex)
            {

                logger.LogError(ex, ex.Message);
                throw;
            }
        }
    }
}
