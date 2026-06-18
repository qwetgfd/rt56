using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0
{
    public interface IDataSliceAPIService
    {
        Task<APIResponse<FillDataCacheResponse>> FillDataInCache();
        Task<APIResponse<string>> ClearCache();
        Task<APIResponse<IEnumerable<DsRegionResponseDto>>> GetRegionBySecurityGroup();
        Task<APIResponse<IEnumerable<DsSubRegionResponseDto>>> GetSubRegion();
        Task<APIResponse<IEnumerable<DsClientResponseDto>>> GetClient();
    }
}
