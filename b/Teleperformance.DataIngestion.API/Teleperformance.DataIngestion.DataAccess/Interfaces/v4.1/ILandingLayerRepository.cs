using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v4._1;
using Teleperformance.DataIngestion.Models.Models.v4._1;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1
{
    public interface ILandingLayerRepository
    {
         Task<DatabaseResponse> AddActivityLog(string source, string message, string details, int moduleTypeId, string flpConfigurationId, string uploadFileId);
        Task<FlpValidationDetails?> GetValidationDetailsAsync(string flpConfigurationId);
        Task<DatabaseResponse> AddLandingLayerFileDetails(string flpConfigurationId, string uploadFileId, string fileName, string changedFileName, bool successFile, string fileURL, string message,bool landingLayerFolder);
        Task<LandingLayerDetails?> GetLandingLayerDetailsAsync(string flpConfigurationId);

    }
}
