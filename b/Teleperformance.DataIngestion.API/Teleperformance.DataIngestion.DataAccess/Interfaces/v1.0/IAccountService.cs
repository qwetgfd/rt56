using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0.Authentication;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Account;
using System.Security.Claims;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IAccountService
    {
        Task<APIResponse<UserDetailData>> GetToken(AuthCredentials AuthCredentials);

        Task<APIResponse<UserDetailData>> GetUserDetail(AuthCredentials authCredentials);
        Task<APIResponse<ApplicationKeyResponseDto>> RegisterApplication(ApplicationRegistrationRequestDto applicationRegistrationRequestDto);
        Task<APIResponse<ApplicationDto>> Authenticate();
        Task<APIResponse<string>> VerifyToken();
    }
}