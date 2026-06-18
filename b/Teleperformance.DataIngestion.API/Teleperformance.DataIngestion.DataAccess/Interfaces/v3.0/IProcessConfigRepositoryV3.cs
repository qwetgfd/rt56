using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v2._0.Authentication;
using Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0
{
    public interface IProcessConfigRepositoryV3
    {
        Task<List<EnglishCharactersEquivalents>> GetEnglishEquivalents(string language);
        Task<IEnumerable<SecurityGroupMapping>> GetSecurityGroupMappingsAsync();
        Task<UpdateLoginResponseDto> UpdateLogin(string loginId);

        Task<RevokedTokenEntity> RevokeAsync(string jti, DateTimeOffset expiresAt, CancellationToken ct = default);
        Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default);

        Task<List<DIDatabaseNames>> GetDIDeltaDatabaseNames(int regionId, string subRegionId, int clientNameId, string securityGroupId);

        Task<EnableDisableProcessByFlpConfigurationIdResponseDto> EnableDisableProcessByConfigurationId(string flpConfigurationIds, string userName, string created_by, bool activeStatus);
    }
}
