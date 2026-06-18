using Teleperformance.DataIngestion.DataAccess.Repository.v2._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v2._0;
using ProcessNamesDto = Teleperformance.DataIngestion.Models.DTOs.v2._0.ProcessConfiguration.ProcessNamesDto;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0
{
    public interface IProcessConfigService
    {
        Task<APIResponse<IEnumerable<SecurityGroupDto>>> GetSecurityGroups(string loginId);
        Task<APIResponse<List<ProcessNamesDto>>> GetAllProcessNamesByLoginId(int fileProcessingServerTypeId);
        Task<APIResponse<List<ProcessNamesDto>>> GetAllProcessNamesByLoginIdByTerm(string queryTerm, int fileProcessingServerTypeId);
        Task<APIResponse<string>> SaveUserSecurityGroup(UserSelectedSecurityGroupDto userSelectedSecurityGroupDto);
        Task<APIResponse<DSConfigDTO?>> GetDSConfiguration(int id);
        Task<APIResponse<IEnumerable<SecurityGroupRegionDto>>> GetRegionBySecurityGroupId(string securityGroupId);
    }
}
