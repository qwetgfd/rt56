using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.EIB;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v4._1.EIB;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1
{
    public interface IEIBServiceV4_1
    {
        Task<APIResponse<string>> GetEIBRequiredBPKeyword();
        Task<APIResponse<List<DBViewsResponseDto>>> GetAllViews();
        Task<APIResponse<List<DBSPsResponseDto>>> GetAllProfilingSP();
        Task<APIResponse<DBSPsResponseDto>> GetCurrentStatusOfPDMSP(int procedureNameId);        

        Task<APIResponse<bool>> InsertEIBDetails(string jsonRequest, IFormFile? file, Stream? stream, string userName, string ntID);

        Task<APIResponse<EIBResponseDto>> GetAllEIBs(EIBListRequestDto request);
        Task<APIResponse<List<EIBCountry>>> GetCountries();
        
        Task<APIResponse<EIBConfigurationRequest>> GetEIBByEIBId(string eibId);


        Task<APIResponse<bool>> CheckActiveEIBConfiguration(string EIBName);

        Task<APIResponse<bool>> GenerateEIB(string eibId);

        Task<APIResponse<List<EIBGenerationStatusDto>>> getEIBGenerationStatus(DateTime generationStartDateTime, DateTime generationEndDateTime
            ,CancellationToken cancellationToken);

        Task<APIResponse<List<ProfilingLogs>>> SendPDMProflingSatusBySPId(int procedureNameId, string runId, CancellationToken cancellationToken, int lastSeendId = 0);

        //Task<APIResponse<UpdateLoginResponseDto>> UpdateLogin(UpdateLoginDto updateLoginDto, ClaimsPrincipal user, CancellationToken ct);
        Task<APIResponse<string>> RegisterPDMRProfilingSPRun(int procedureNameId, string processedBy, CancellationToken ct);

        //
    }
}
