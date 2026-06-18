using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v4._1.DataAssists;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1.DataAssists
{
    public interface IDataValidationRepositoryV4_1
    {
        Task<List<ProjectPrompts>> getPrompt(int flowId, int projectId);

        Task<bool> commitDataAssistGeneratedJsonResponse(string response, int flpRuleSetId);
    }
}
