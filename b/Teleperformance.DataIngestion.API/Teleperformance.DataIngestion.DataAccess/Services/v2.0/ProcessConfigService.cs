using Microsoft.Extensions.Logging;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v2._0;
using Teleperformance.DataIngestion.Models.DTOs.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v2._0;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using ProcessNamesDto = Teleperformance.DataIngestion.Models.DTOs.v2._0.ProcessConfiguration.ProcessNamesDto;

namespace Teleperformance.DataIngestion.DataAccess.Services.v2._0
{
    public class ProcessConfigService : IProcessConfigService
    {
        private readonly IProcessConfigRepository _processConfigRepository;
        private readonly ILogger<ProcessConfigService> _logger;
        private readonly IHeaderService _headerService;

        public ProcessConfigService(IProcessConfigRepository processConfigRepository, ILogger<ProcessConfigService> logger, IHeaderService headerService)
        {
            _processConfigRepository = processConfigRepository;
            _logger = logger;
            _headerService = headerService;
        }

        public async Task<APIResponse<IEnumerable<SecurityGroupDto>>> GetSecurityGroups(string loginId)
        {
            string errorMessage = string.Empty;
            var SecurityGroups = await _processConfigRepository.GetSecurityGroups(loginId);

            if (SecurityGroups == null)
            {
                errorMessage = "No security group found.";
                _logger.LogInformation(errorMessage);
                return await Task.FromResult(new APIResponse<IEnumerable<SecurityGroupDto>>
                {
                    ResultStatus = APIResultStatus.NoContent,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = null
                });
            }

            if (!SecurityGroups.Any())
            {

                errorMessage = "No security group found.";
                _logger.LogInformation(errorMessage);
                return await Task.FromResult(new APIResponse<IEnumerable<SecurityGroupDto>>
                {
                    ResultStatus = APIResultStatus.NoContent,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = null
                });
            }
            if (SecurityGroups.Any() && string.Compare(SecurityGroups.FirstOrDefault()?.Result, "Failure", true) == 0)
            {
                errorMessage = "return failure from database for security group.";
                _logger.LogInformation(errorMessage);
                return await Task.FromResult(new APIResponse<IEnumerable<SecurityGroupDto>>
                {
                    ResultStatus = APIResultStatus.NoContent,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = null
                });
            }

            var groupes = SecurityGroups.Select(sg => new SecurityGroupDto
            {
                SecurityGroupId = sg.securityGroupId,
                SecurityGroupName = sg.securityGroupName,
                UserSelectedGroup = sg.userSelectedGroup
            });
            //var flpConfigurationArray = await Task.WhenAll(flpConfigurationList);
            return new APIResponse<IEnumerable<SecurityGroupDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = groupes
            };


        }


        public async Task<APIResponse<string>> SaveUserSecurityGroup(UserSelectedSecurityGroupDto userSelectedSecurityGroupDto)
        {
            string errorMessage = string.Empty;
            var databaseResponse = await _processConfigRepository.CommitUserSecurityGroup(userSelectedSecurityGroupDto);

            if (databaseResponse == null)
            {
                errorMessage = "Error: Not updated security group. for login " + userSelectedSecurityGroupDto.LoginId;
                _logger.LogError(errorMessage);
                return await Task.FromResult(new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = null
                });
            }


            if (string.Compare(databaseResponse.Result, "Failure", true) == 0)
            {
                errorMessage = "return failure from database for security group.";
                _logger.LogError(errorMessage);
                return await Task.FromResult(new APIResponse<string>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = null
                });
            }

            return new APIResponse<string>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success." },
                Result = databaseResponse?.Message ?? ""
            };


        }
        public async Task<APIResponse<List<ProcessNamesDto>>> GetAllProcessNamesByLoginId(int fileProcessingServerTypeId)
        {

            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
            if (string.IsNullOrWhiteSpace(securityGroupId))
            {
                _logger.LogError("Error: Missing securityGroupId in request parameter.");
                return new APIResponse<List<ProcessNamesDto>>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Missing securityGroupId" },
                    Result = null
                };
            }
            var csvConfigurations = await _processConfigRepository.GetAllProcessNamesByLoginId(securityGroupId, fileProcessingServerTypeId);// await _cSVConfigRepo.ListAsync(spec);
            if (csvConfigurations == null)
            {
                _logger.LogError("Error: Information not found.");

                return new APIResponse<List<ProcessNamesDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Information not found." },
                    Result = null
                };
            }

            List<ProcessNamesDto> listProcessNames = new List<ProcessNamesDto>();

            foreach (var item in csvConfigurations)
            {
                var processNames = new ProcessNamesDto { Id = item.Id, FLPConfigurationId = item.flpConfigurationId, ProcessNamesMore = item.process_name, ProcessNames = item.process_name, Description = item.Description };
                listProcessNames.Add(processNames);
            }

            return new APIResponse<List<ProcessNamesDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = listProcessNames
            };
        }

        public async Task<APIResponse<List<ProcessNamesDto>>> GetAllProcessNamesByLoginIdByTerm(string queryTerm, int fileProcessingServerTypeId)
        {

            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
            if (string.IsNullOrWhiteSpace(securityGroupId))
            {
                _logger.LogError("Error: Missing securityGroupId in request parameter.");
                return new APIResponse<List<ProcessNamesDto>>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Missing securityGroupId" },
                    Result = null
                };
                //return new List<ProcessNamesDto>();
            }
            var csvConfigurations = await _processConfigRepository.GetAllProcessNamesByLoginIdByTerm(queryTerm, securityGroupId, fileProcessingServerTypeId);// await _cSVConfigRepo.ListAsync(spec);
            if (csvConfigurations == null)
            {
                _logger.LogError("Error: Information not found.");

                return new APIResponse<List<ProcessNamesDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { "Information not found." },
                    Result = null
                };

            }

            List<ProcessNamesDto> listProcessNames = new List<ProcessNamesDto>();
            listProcessNames.Add(new ProcessNamesDto() { Id = 0, FLPConfigurationId = "", ProcessNames = "New", Description = "New" });
            foreach (var item in csvConfigurations)
            {
                var processNames = new ProcessNamesDto { Id = item.Id, FLPConfigurationId = item.flpConfigurationId, ProcessNames = item.process_name, Description = item.Description };
                listProcessNames.Add(processNames);
            }

            return new APIResponse<List<ProcessNamesDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = new List<string> { "Success" },
                Result = listProcessNames
            };
        }

        public async Task<APIResponse<DSConfigDTO?>> GetDSConfiguration(int id)
        {
            string errorMessage = string.Empty;
            var dsConfigDetails = await _processConfigRepository.GetDSConfiguration(id);

            if (dsConfigDetails == null)
            {
                errorMessage = "Error: Unable to get Data Slice configuration ";
                _logger.LogError(errorMessage);
                return await Task.FromResult(new APIResponse<DSConfigDTO?>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = new List<string> { errorMessage },
                    Result = null
                });
            }

            return new APIResponse<DSConfigDTO?>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success."],
                Result = new DSConfigDTO() { consumerApplicationId = dsConfigDetails.consumerApplicationId, sourceDataObjId = dsConfigDetails.sourceDataObjId }
            };


        }
        public async Task<APIResponse<IEnumerable<SecurityGroupRegionDto>>> GetRegionBySecurityGroupId(string securityGroupId)
        {
            string errorMessage = string.Empty;
            var sgRegions = await _processConfigRepository.GetRegionBySecurityGroupId(securityGroupId);

            if (!sgRegions.Any())
            {
                errorMessage = "Error: Unable to get security group regions ";
                _logger.LogError(errorMessage);
                return await Task.FromResult(new APIResponse<IEnumerable<SecurityGroupRegionDto>>
                {
                    ResultStatus = APIResultStatus.Failed,
                    ResponseMessage = [errorMessage],
                    Result = null
                });
            }
            var regions = sgRegions.Select(sgr => new SecurityGroupRegionDto
            {
                regionId = sgr.regionId,
                subRegionId = sgr.subRegionId,
                clientId = sgr.clientId
            });
            return new APIResponse<IEnumerable<SecurityGroupRegionDto>>
            {
                ResultStatus = APIResultStatus.Completed,
                ResponseMessage = ["Success"],
                Result = regions
            };


        }


    }
}
