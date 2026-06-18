using Dapper;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.Common.Crypto;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Account;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.Account;
using Teleperformance.DataIngestion.Models.Entities.v1._0.Authentication;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v1._0
{
    public class AccountRepository : IAccountRepository
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<AccountRepository> _logger;
        private readonly IDapperService _dapperService;

        public AccountRepository(IConfiguration configuration, ILogger<AccountRepository> logger, IDapperService dapperService)
        {
            this.configuration = configuration;
            _logger = logger;
            _dapperService = dapperService;
        }
        public Task<APIResponse<FunctionAppAuth>> Authenticate()
        {
            throw new NotImplementedException();
        }


        public async Task<DatabaseResponse?> RegisterApplicationAsync(ApplicationRegistrationDto applicationRegistrationDto)
        {
            var dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@applicationId", applicationRegistrationDto.ApplicationId);
            dynamicParameters.Add("@applicationName", applicationRegistrationDto.ApplicationName);
            dynamicParameters.Add("@applicationDescription", applicationRegistrationDto.ApplicationDescription);
            dynamicParameters.Add("@applicationOwnerName", applicationRegistrationDto.ApplicationOwnerName);
            dynamicParameters.Add("@applicationOwnerEmail", applicationRegistrationDto.ApplicationOwnerEmail);
            dynamicParameters.Add("@incidentNumber", applicationRegistrationDto.IncidentNumber);
            dynamicParameters.Add("@applicationPassword", applicationRegistrationDto.Password);
            dynamicParameters.Add("@applicationSalt", applicationRegistrationDto.Salt);
            dynamicParameters.Add("@encryptionKey", applicationRegistrationDto.EncryptionKey);
            dynamicParameters.Add("@active", applicationRegistrationDto.Active);

            return await _dapperService.GetSingleRowAsync<DatabaseResponse>("[dbo].[commit_registerApplication]",
                        dynamicParameters, commandType: CommandType.StoredProcedure);
        }


        public async Task<ApplicationKeyDto?> ApplicationKeyByApplicationIDAsync(string applicationId, DateTime requestDateTime)
        {

            var parameters = new DynamicParameters();
            parameters.Add("@applicationId", applicationId);
            parameters.Add("@requestDateTime", requestDateTime);
            return await _dapperService.GetSingleRowAsync<ApplicationKeyDto>("[dbo].[sel_applicationByApplicationId]",
                        parameters, commandType: CommandType.StoredProcedure);
        }





        public async Task<string?> GenerateBearerToken(string applicationClientId, int tokenVersion, string userId)
        {
            try
            {
                
                var claims = new List<Claim>
                {
                    new Claim(JwtRegisteredClaimNames.Sub, userId),
                    new Claim(JwtRegisteredClaimNames.Name, applicationClientId),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim("ver", tokenVersion.ToString()), //serssion/version claim
                                                               // // Add ONLY ONE custom claim for application ID
                    new Claim("client_id", applicationClientId)
                };

                string token = await GenerateAccessTokenAsync(claims, 120); // Await the asynchronous method
                string tokenEncryptionKey = KeyVault.GetKeyVaultValue("TPDataIngestionEncryptionKey").Result;
                //Will used encrypted token
                var ecryptedToken = Crypto.Encrypt(token, tokenEncryptionKey);
                return ecryptedToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString());
                return null;
            }
        }

        private async Task<string> GenerateAccessTokenAsync(IEnumerable<Claim> claims, int tokenTime)
        {

            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("TPDataIngestionClientSecret"))); // Await the asynchronous method
            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(tokenTime); //480/60 = ??? 
            var token = new JwtSecurityToken(
                issuer: Environment.GetEnvironmentVariable("TPDataIngestionClientId"), // Await the asynchronous method
                audience: Environment.GetEnvironmentVariable("TPDataIngestionTenantId"), // Await the asynchronous method
                notBefore: now,
                expires: expires,
                claims: claims,
                signingCredentials: new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256)
            );

            var tokenHandler = new JwtSecurityTokenHandler();

            return tokenHandler.WriteToken(token);
        }

        public UserDetailData Token(string NTID)
        {
            UserDetailData _UserDetailData = new UserDetailData();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, NTID),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            _UserDetailData.token = generateAccessToken(claims);

            return _UserDetailData;
        }

        public async Task<UserDetailData> UserDetails(string NTID)
        {
            UserDetailData _UserDetailData = new UserDetailData();
            var parameters = new
            {
                LoginId = NTID
            };


            //TODO:
            //if (_configuration["TPAuthentication:AllowTPAuthenticationLog"] == "true")
            //{
            //    string hostname = Dns.GetHostName();
            //    string IP = Dns.GetHostEntry(hostname).AddressList[0].ToString();
            //    //await LogAuthenticationActivity(NTID, true, IP);
            //}

            //using var conn = new SqlConnection(DBConnectionString);


            //_UserDetailData.userDetail = new UserResponseDetails();
            //var UserDetailData = await conn.QueryFirstOrDefaultAsync<int>("[sel_UseDetail]", parameters, commandType: CommandType.StoredProcedure);
            //_UserDetailData.userDetail.IsAdmin = UserDetailData > 0 ? true : false;

            _UserDetailData.userDetail = new UserResponseDetails();
            _UserDetailData.userDetail.IsAdmin = true;
            return _UserDetailData;
        }

        private string generateAccessToken(IEnumerable<Claim> claims)
        {

            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("TPDataIngestionClientSecret")));// KeyVault.GetKeyVaultValue("AppKey").Result););// _configuration["TPAuthentication:AppKey"]));;

            var creds = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);
            //var encryptingCredentials = new EncryptingCredentials(secretKey, JwtConstants.DirectKeyUseAlg, SecurityAlgorithms.Aes256CbcHmacSha512);
            var encryptingCredentials = new EncryptingCredentials(secretKey, JwtConstants.DirectKeyUseAlg, SecurityAlgorithms.Aes128CbcHmacSha256);

            //var token = new JwtSecurityToken(
            //        issuer: _configuration["Token:ClientId"],
            //        audience: _configuration["Token:TenantId"],
            //        //expires: DateTime.Now.AddHours(3),
            //        expires: DateTime.Now.AddMinutes(15), //set to 15
            //        claims: claims,
            //        //signingCredentials: new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256),
            //        signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["TPAuthentication:AppKey"])),
            //        SecurityAlgorithms.HmacSha512Signature)


            //        );

            var jwtSecurityToken = new JwtSecurityTokenHandler().CreateJwtSecurityToken(
                    issuer: Environment.GetEnvironmentVariable("TPDataIngestionClientId"), // KeyVault.GetKeyVaultValue("ClientId").Result, //_configuration["Token:ClientId"],
                    audience: Environment.GetEnvironmentVariable("TPDataIngestionTenantId"), //KeyVault.GetKeyVaultValue("TenantId").Result, //_configuration["Token:TenantId"],
                    new ClaimsIdentity(claims),
                    DateTime.Now,
                    expires: DateTime.Now.AddMinutes(40),
                     DateTime.Now,
                    signingCredentials: creds,
                    encryptingCredentials: encryptingCredentials
                ); ;

            var tokenHandler = new JwtSecurityTokenHandler();

            return tokenHandler.WriteToken(jwtSecurityToken);

        }

        public async Task<int> CommitAppUserLogin(string LoginId)
        {
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@loginId", LoginId);
                int found = await _dapperService.GetSingleRowAsync <int>("[dbo].[commit_updateAppUser]",
                            parameters, commandType: CommandType.StoredProcedure);

                return found;
            }
            catch (Exception ex)
            {

                throw;
            }
            
        }

        public async Task<int> GetAppUserLogin(string LoginId)
        {

            var parameters = new DynamicParameters();
            parameters.Add("@loginId", LoginId);
            int found = await _dapperService.GetSingleRowAsync<int>("[dbo].[set_AppUser]",
                        parameters, commandType: CommandType.StoredProcedure);

            return found;
        }

    }
}
