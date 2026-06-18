using AutoMapper;
using DocumentFormat.OpenXml.Office2016.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using NPOI.OpenXmlFormats.Vml;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Common.Crypto;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.DataAccess.Repository;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Account;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard;
using Teleperformance.DataIngestion.Models.DTOs.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.Account;
using Teleperformance.DataIngestion.Models.Entities.v1._0.Authentication;

namespace Teleperformance.DataIngestion.DataAccess.Services.v1._0
{
    public class AccountService : IAccountService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ILogger<AccountService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMapper _mapper;
        private readonly IProcessConfigRepository _processConfigRepository;
        public AccountService(IAccountRepository accountRepository, ILogger<AccountService> logger, IMapper mapper, IHttpContextAccessor httpContextAccessor, IProcessConfigRepository processConfigRepository)
        {
            _accountRepository = accountRepository;
            _logger = logger;
            _mapper = mapper;
            _httpContextAccessor = httpContextAccessor;
            _processConfigRepository = processConfigRepository;
        }
        public async Task<APIResponse<ApplicationKeyResponseDto>> RegisterApplication(ApplicationRegistrationRequestDto applicationRegistrationRequestDto)
        {

            var applicationRegistrationDto = _mapper.Map<ApplicationRegistrationDto>(applicationRegistrationRequestDto);
            applicationRegistrationDto.ApplicationId = Convert.ToString(Guid.NewGuid());
            applicationRegistrationDto.Active = true;
            var encryptionKey = Guid.NewGuid().ToString("N") + DateTime.UtcNow.ToString("yyyyMMdd");
            applicationRegistrationDto.ApplicationPassword = Guid.NewGuid().ToString("N");
            using var hmac = new HMACSHA512();
            applicationRegistrationDto.Password = hmac.ComputeHash(Encoding.UTF8.GetBytes(applicationRegistrationDto.ApplicationPassword));
            applicationRegistrationDto.Salt = hmac.Key;
            applicationRegistrationDto.EncryptionKey = encryptionKey;
            var databaseResponse = await _accountRepository.RegisterApplicationAsync(applicationRegistrationDto);
            if (databaseResponse == null)
            {
                return await Task.FromResult(new APIResponse<ApplicationKeyResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Internal server error" },
                    Result = null
                });
            }
            var databaseResponseDto = _mapper.Map<DatabaseResponseDto>(databaseResponse);
            if (string.Compare(databaseResponse.Result, "Failure", true) == 0)
                return await Task.FromResult(new APIResponse<ApplicationKeyResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { databaseResponseDto.Message },
                    Result = null
                });
            if (string.Compare(databaseResponse.Result, "Error", true) == 0)
                return await Task.FromResult(new APIResponse<ApplicationKeyResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { databaseResponseDto.Message },
                    Result = null
                });
            var clientSecret = Crypto.Encrypt(applicationRegistrationDto.ApplicationPassword, applicationRegistrationDto.EncryptionKey);
            var applicationKeyResponse = new ApplicationKeyResponseDto();
            applicationKeyResponse.ClientId = applicationRegistrationDto.ApplicationId ?? "";
            applicationKeyResponse.ClientSecret = clientSecret ?? "";
            return await Task.FromResult(new APIResponse<ApplicationKeyResponseDto>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { $"Success:{databaseResponseDto.Message}" },
                Result = applicationKeyResponse
            });
        }



        public async Task<APIResponse<ApplicationDto>> Authenticate()
        {
            DateTime requestDateTime = DateTime.UtcNow;
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                string message = $"Error: Context null";
                _logger.LogError(message);
                return await Task.FromResult(new APIResponse<ApplicationDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Headers Required" },
                    Result = null
                });
            }

            if (!context.Request.Headers.TryGetValue("ClientId", out var applicationClientId))
            {
                string message = $"Error: Application ClientId not found in header";
                _logger.LogError(message);
                return await Task.FromResult(new APIResponse<ApplicationDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { message },
                    Result = null
                });
            }

            if (!context.Request.Headers.TryGetValue("ClientSecret", out var applicationClientSecret))
            {
                string message = $"Error:ClientSecret not found in header";
                _logger.LogError(message);
                return await Task.FromResult(new APIResponse<ApplicationDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { message },
                    Result = null
                });
            }

            if (string.IsNullOrWhiteSpace(applicationClientId))
            {
                string message = $"Error:Invalid ClientId";
                _logger.LogError(message);
                return await Task.FromResult(new APIResponse<ApplicationDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { message },
                    Result = null
                });
            }

            if (string.IsNullOrWhiteSpace(applicationClientSecret))
            {
                string message = $"Error:Invalid ClientSecret";
                _logger.LogError(message);
                return await Task.FromResult(new APIResponse<ApplicationDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { message },
                    Result = null
                });
            }
            context.Request.Headers.TryGetValue("userId", out var userId);
            var applicationResponse = await _accountRepository.ApplicationKeyByApplicationIDAsync(applicationClientId, requestDateTime);
            var databaseResponseDto = _mapper.Map<DatabaseResponseDto>(applicationResponse);
            var applicationKey = _mapper.Map<ApplicationKey>(applicationResponse);

            if (string.Compare(applicationResponse.Result, "Failure", true) == 0)
                return await Task.FromResult(new APIResponse<ApplicationDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { databaseResponseDto.Message },
                    Result = null
                });
            if (string.Compare(applicationResponse.Result, "Error", true) == 0)
                return await Task.FromResult(new APIResponse<ApplicationDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { databaseResponseDto.Message },
                    Result = null
                });

            if (applicationResponse == null || applicationResponse.ApplicationPassword == null || string.IsNullOrWhiteSpace(applicationResponse.ApplicationId))
                return await Task.FromResult(new APIResponse<ApplicationDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Invalid Information" },
                    Result = null
                });

            string clientSecret = Crypto.Decrypt(applicationClientSecret, applicationKey.EncryptionKey);
            using var hmac = new HMACSHA512(applicationKey.ApplicationSalt);
            var ComputedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(clientSecret));
            for (int i = 0; i < ComputedHash.Length; i++)
            {
                if (ComputedHash[i] != applicationKey.ApplicationPassword[i])
                {
                    _logger.LogError($"Invalid ClientSecret for ClientId {applicationClientId}");
                    return await Task.FromResult(new APIResponse<ApplicationDto>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Invalid Credentials" },
                        Result = null
                    });
                }
            }
            //will only update/insert a record in di_appuser for token versioning
            var tokenVersion = await _accountRepository.CommitAppUserLogin(userId);

            var token = await _accountRepository.GenerateBearerToken(applicationClientId, tokenVersion, userId);
            var rawRefresh = RefreshTokenHelper.GenerateRawToken(64);

            var hash = RefreshTokenHelper.Hash(rawRefresh);
            var now = DateTime.UtcNow;
            var refreshTtlDays = 14; //todo: configure in keyvault

            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogError($"Token not generated for ClientId {applicationClientId}");
                return await Task.FromResult(new APIResponse<ApplicationDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Invalid ClientSecret" },
                    Result = null
                });
            }
            var applicationDto = new ApplicationDto();
            applicationDto.ApplicationId = applicationClientId;
            applicationDto.Token = token;
            if (!string.IsNullOrWhiteSpace(userId) && applicationResponse.ShowSecurityGroup)
            {
                var userSecurityGroups = await _processConfigRepository.GetSecurityGroups(userId);
                var groupes = userSecurityGroups.Select(sg => new SecurityGroupDto
                {
                    SecurityGroupId = sg.securityGroupId,
                    SecurityGroupName = sg.securityGroupName,
                    UserSelectedGroup = sg.userSelectedGroup
                });
                applicationDto.securityGroups = groupes;

                var securityGroupId = groupes.Where(sg => sg.UserSelectedGroup).Select(sg => sg.SecurityGroupId).FirstOrDefault();
                if (securityGroupId != null)
                {
                    var sgPageAccess = await _processConfigRepository.GetUserRoles(securityGroupId);

                    if (sgPageAccess != null)
                    {
                        applicationDto.pageAccess = sgPageAccess;
                    }
                }


                var refresh = new RefreshToken
                {
                    Id = Guid.NewGuid(),                    
                    userName = userId,
                    TokenHash = hash,                  
                    CreatedUtc = now,
                    ExpiresUtc = now.AddDays(refreshTtlDays),
                    Revoked = false
                };

                //save refreshtoken in db
                var refreshToken =  await _processConfigRepository.CommitRefreshToken(refresh);
                if (string.Compare(refreshToken.Result, "Success", true) == 0)
                {
                    applicationDto.RefreshToken = rawRefresh;
                    applicationDto.ExpiresUtc = refresh.ExpiresUtc;
                }
                else if (string.Compare(refreshToken.Result, "Error", true) == 0)
                {
                    return await Task.FromResult(new APIResponse<ApplicationDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { refreshToken.Message },
                        Result = null
                    });
                }


            }
            return await Task.FromResult(new APIResponse<ApplicationDto>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = applicationDto
            });
        }

        public async Task<APIResponse<string>> VerifyToken()
        {
            DateTime requestDateTime = DateTime.UtcNow;
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                string message = $"Error: Context null";
                _logger.LogError(message);
                return await Task.FromResult(new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Invalid Headers" },
                    Result = null
                });
            }           

            return await Task.FromResult(new APIResponse<string>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = "Valid Token"
            });
        }

        public async Task<APIResponse<UserDetailData>> GetToken(AuthCredentials AuthCredentials)
        {
            var Username = AuthCredentials.NTID;
            if (string.IsNullOrWhiteSpace(Username))
            {
                _logger.LogError("Error: Missing Username in request parameter.");
                return new APIResponse<UserDetailData>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Missing Username" },
                    Result = null
                };
            }
            UserDetailData result = _accountRepository.Token(Username);
            if (result == null || string.IsNullOrEmpty(result.token))
            {
                _logger.LogError("Error: User information not found.");

                return new APIResponse<UserDetailData>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "User information not found." },
                    Result = null
                };
            }

            return new APIResponse<UserDetailData>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = result
            };
        }



        public async Task<APIResponse<UserDetailData>> GetUserDetail(AuthCredentials authCredentials)
        {

            var Username = authCredentials.NTID;
            try
            {
                if (string.IsNullOrWhiteSpace(Username))
                {
                    _logger.LogError("Error: Missing Username in request parameter.");
                    return new APIResponse<UserDetailData>
                    {
                        ResultStatus = APIResultStatus.InvalidParameters,
                        ResponseMessage = new List<string> { "Missing Username" },
                        Result = null
                    };
                }

                UserDetailData result = await _accountRepository.UserDetails(Username);

                if (result == null || result.userDetail == null)
                {
                    _logger.LogError("Error: User information not found.");

                    return new APIResponse<UserDetailData>
                    {
                        ResultStatus = APIResultStatus.Failed,
                        ResponseMessage = new List<string> { "User information not found." },
                        Result = null
                    };
                }

                return new APIResponse<UserDetailData>
                {
                    ResultStatus = APIResultStatus.Completed,
                    ResponseMessage = new List<string> { "Success" },
                    Result = result
                };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return new APIResponse<UserDetailData>
                {
                    Result = null,
                    ResponseMessage = new List<string> { ex.Message },
                    ResultStatus = APIResultStatus.Failed
                };
            }
        }
    }
}
