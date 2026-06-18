using AutoMapper;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Account;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.Account;

namespace Teleperformance.DataIngestion.API.Helpers
{
    public class AutoMapperProfiles:Profile
    {
        public AutoMapperProfiles()
        {
            CreateMap<ApplicationRegistrationRequestDto, ApplicationRegistrationDto>();
            CreateMap<DatabaseResponse, DatabaseResponseDto>();
            CreateMap<ApplicationKeyDto, DatabaseResponseDto>();
            CreateMap<ApplicationKeyDto, ApplicationKey>();
        }
    }
}
