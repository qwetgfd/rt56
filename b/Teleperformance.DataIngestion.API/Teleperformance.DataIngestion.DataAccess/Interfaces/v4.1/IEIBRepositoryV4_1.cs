using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.EIB;
using Teleperformance.DataIngestion.Models.Entities.v4._1.EIB;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1
{
    public interface IEIBRepositoryV4_1
    {
        Task<List<DBViewsResponseDto>> GetAllViews();
        Task<List<PDMProflingSPNames>> GetAllProfilingSP();
        
        Task<List<EIBCountryDto>> GetCountries();
        
        Task<EIBConfigurationList> GetAllEIBs(EIBListRequestDto request);
        Task<EIBConfigurationRequest> GetEIBByEIBId(string eibId);
        
        Task<bool> CheckActiveEIBConfiguration(string EIBName);

        Task<bool> GenerateEIB(string eibId);
        Task<string> RegisterPDMProfilingSPRun(int procedureNameId, string processedBy);
        Task<ProfilingSPConfiguration> GetConfigurationProfilingSP(int procedureNameId);
        Task<string> UpdateRunSatusAsync(string runId, string status);
        
        Task<string> InsertEIBConfigurationDetails(EIBConfigurationRequest request);
        Task<int> InsertBusinessProcessName(BusinessProcessNames request, string EIBId);
        Task<int> InsertBusinessProcessDBViewMapping(BusinessProcessDBViewMapping request);
        Task<List<EIBGenerationStatus>> getEIBGenerationStatus(DateTime generationStartDateTime, DateTime generationEndDateTime);
        Task<List<ProfilingLogs>> GetProfilingLogs(string runid, CancellationToken ct);
        

    }
}
