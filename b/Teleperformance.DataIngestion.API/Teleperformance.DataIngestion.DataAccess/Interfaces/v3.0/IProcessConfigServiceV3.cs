using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v2._0;
using Teleperformance.DataIngestion.Models.Entities.v3._0;
using Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0
{
    public interface IProcessConfigServiceV3
    {
        Task<APIResponse<string>> ConvertEnglishCharactersOnly(string wordToConvert, string language);
        Task<APIResponse<List<EnglishCharactersEquivalents>>> GetAllEnglishCharactersOnly(string language);
        Task<APIResponse<UpdateLoginResponseDto>> UpdateLogin(UpdateLoginDto updateLoginDto, ClaimsPrincipal user, CancellationToken ct);
        Task<APIResponse<List<DIDatabaseNameDto>>> GetDeltaDatabaseNames(int regionId, string subRegionId, int clientNameId);
        Task<APIResponse<EnableDisableProcessByFlpConfigurationIdResponseDto>> EnableDisableProcessByConfigurationId(string flpConfigurationIds, string userName, string created_by, bool activeStatus);

    }
}
