using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Models.v4._1;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IProcessConfigurationRepository
    {
        Task<List<DataTypeName>> GetAllDbDataTypes();

        Task<List<DateTimeFormat>> GetAllDbDateTimeFormats(bool displayOnLandingLayer);

        Task<List<DIRegions>> GetAllDIRegionsAsync();

        Task<List<DISubRegions>> GetAllDISubRegionsAsync();

        Task<List<DIClientnames>> GetAllDIClientnames();

        Task<bool> InsertConfigurationRegionMapping(ConfigurationRegionMapping configurationRegionMapping);

        Task<List<DIDatabaseNames>> GetDIDatabaseNames(int regionId, string subRegionId, int clientNameId, string securityGroupId, int fileProcessingServerTypeId);

        Task<int> InsertFileConfiguration(FileConfigurationEntity fileConfiguration);

        Task<List<FileConfigurationEntity>> GetAllProcessNamesByLoginId(string loginId);

        Task<FileConfigurationEntity> GetFileConfigurationByIdAsync(string id);

        Task<string> VerifyProcessNameUnique(string processName, string configId);

        Task<IEnumerable<GetProcessTypeResponse?>> GetProcessType();

        Task<FlpProcessConfigurationResponse> GetFlpProcessConfigurationListAsync(FlpProcessConfigurationListRequest request);
        Task<IEnumerable<FileServerDetailsResponse?>> GetfileServerDetails();
        Task<IEnumerable<StorageAccountDetailsResponse?>> GetstorageAccountDetails(int fileProcessingServerTypeId);
        Task<string> InsertFlpConfigurationDetails(InsertFlpConfigurationRequest insertFlpConfigurationRequest);
        Task<int> InsertFlpFileConfigurationDetails(FlpFileConfigurationRequest fileConfig, string flpConfigurationId, string createdBy);

        Task<int> InsertConfigurationTableMapping(ConfigurationTableMappingRequest tableMapping, string flpConfigurationId, string createdBy);

        Task<DatabaseResponse> UpdateColumnNameList(string flpConfigurationId, string tabName, string columnNameList);

        Task<DatabaseResponse> UpdateConvertDatatypesColumnList(string flpConfigurationId, string tabName, string convertDatatypesColumnList);
        Task<DatabaseResponse> UpdateFlpFileColumnMapping(string flpConfigurationId, string tabName, string fileColumnMapping);
        Task<ProcessConfigDetail?> GetConfigurationDetailsById(string flpConfigurationId,string tabName);
        Task<IEnumerable<ScheduerType?>> GetScheduerType();
        Task<IEnumerable<DateTimeFormats?>> GetDateTimeFormatList();
        Task<IEnumerable<WeekDayName?>> GetWeekDayName();
        Task<IEnumerable<FrequencyHour?>> GetFrequencyHour();
        Task<int> InsertCustomSchedulerDetails(string flpConfigurationId, int frequencyHoursId, int weekDaysId);

              

    }
}
