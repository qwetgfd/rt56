using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0.Authentication;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Account;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IAccountRepository
    {
        UserDetailData Token(string NTID);
        Task<UserDetailData> UserDetails(string NTID);        
        Task<int> CommitAppUserLogin(string loginId);
        Task<int> GetAppUserLogin(string loginId);
        Task<APIResponse<FunctionAppAuth>> Authenticate();

        Task<DatabaseResponse?> RegisterApplicationAsync(ApplicationRegistrationDto applicationRegistrationDto);
        Task<ApplicationKeyDto?> ApplicationKeyByApplicationIDAsync(string applicationId, DateTime requestDateTime);
        Task<string?> GenerateBearerToken(string applicationClientId, int tokenVersion, string userId);


    }
}
