using Teleperformance.DataIngestion.Models.DTOs.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v2._0.Authentication;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0
{
    public interface IProcessConfigRepository
    {
        Task<IEnumerable<SecurityGroup>> GetSecurityGroups(string loginId);
        Task<IEnumerable<SGPageAccess>> GetUserRoles(string securityGroupIdgId);
        Task<List<FileConfigurationEntity>> GetAllProcessNamesByLoginId(string securityGroupId, int fileProcessingServerTypeId);
        Task<List<FileConfigurationEntity>> GetAllProcessNamesByLoginIdByTerm(string queryTerm, string securityGroupId, int fileProcessingServerTypeId);
        Task<DatabaseResponse> CommitUserSecurityGroup(UserSelectedSecurityGroupDto userSelectedSecurityGroupDto);
        Task<DSConfig?> GetDSConfiguration(int id);
        Task<IEnumerable<SecurityGroupRegion>> GetRegionBySecurityGroupId(string securityGroupId);
        Task<RefreshTokenEntity> CommitRefreshToken(RefreshToken refreshToken);

        
    }
}
