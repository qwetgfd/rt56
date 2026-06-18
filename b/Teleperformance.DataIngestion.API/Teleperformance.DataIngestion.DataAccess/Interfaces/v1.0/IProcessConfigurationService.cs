using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IProcessConfigurationService
    {
        Task<APIResponse<List<ProcessNamesDto>>> GetAllProcessNamesByLoginId(string loginid);
        Task<APIResponse<string>> CheckProcessNameExists(string processName, string configId);
        Task<APIResponse<List<DataTypesDto>>> GetAllDataTypeNames();
        Task<APIResponse<List<DateTimeFormatDto>>> GetDateTimeFormats(bool displayOnLandingLayer);
        Task<APIResponse<List<DIRegionDto>>> GetAllDIRegions();
        Task<APIResponse<List<DISubRegionDto>>> GetAllDISubRegions();
        Task<APIResponse<List<DIClientnamesDto>>> GetAllDIClientnames();
        Task<APIResponse<List<DIDatabaseNameDto>>> GetDatabaseNames(int regionId, string subRegionId, int clientNameId, int fileProcessingServerTypeId);
        Task<APIResponse<FileConfigurationEntity>> GetConfigurationById(string id);
        Task<APIResponse<IEnumerable<GetProcessTypeResponse?>>> GetProcessType();
        Task<APIResponse<FlpConvertParquetRequestDto>> InsertConfiguration(string json, IFormFile file, Stream stream, string loggedInUser, string userName);
        Task<APIResponse<FlpProcessConfigurationResponse>> GetFlpProcessConfigurationList(FlpProcessConfigurationListRequest request);
        Task<APIResponse<IEnumerable<FileServerDetailsResponse?>>> GetfileServerDetails();
        Task<APIResponse<IEnumerable<StorageAccountDetailsResponse?>>> GetstorageAccountDetails(int fileProcessingServerTypeId);
        Task<APIResponse<string>> InsertFlpConfigurationDetails(InsertFlpConfigurationRequest insertFlpConfigurationRequest);
        Task<APIResponse<ProcessConfigDetail?>> GetConfigurationDetailsById(string flpConfigurationId, string? tabName);
        Task<APIResponse<IEnumerable<ScheduerType?>>> GetSchedulerType();
        Task<APIResponse<IEnumerable<WeekDayName?>>> GetWeekDayName();
        Task<APIResponse<IEnumerable<FrequencyHour?>>> GetFrequencyHour();

        Task<APIResponse<string>> GenerateSasKey();
        Task<APIResponse<string>> UploadToBlobTemp(Stream stream, string fileName);
        Task<APIResponse<ConvertToXLSXDto>> GetConvertedXLSX();
        Task<APIResponse<ConvertToXLSXDto>> ConvertToXLSX(IFormFile file, Stream stream, string processName);

        

        
        





    }
}
