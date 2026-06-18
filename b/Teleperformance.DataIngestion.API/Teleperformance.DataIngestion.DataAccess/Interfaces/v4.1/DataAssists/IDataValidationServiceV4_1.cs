using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.DataAssists;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1.DataAssists
{
    public interface IDataValidationServiceV4_1
    {
        //Task<APIResponse<Dictionary<string, List<Dictionary<string, string>>>>> GenerateResponse(ProjectQueryFormRequest request);
        Task<string> GenerateResponse(ProjectQueryFormRequest request);
        Task<bool> GenerateResponse2(ProjectQueryFormRequest request);


    }
}
