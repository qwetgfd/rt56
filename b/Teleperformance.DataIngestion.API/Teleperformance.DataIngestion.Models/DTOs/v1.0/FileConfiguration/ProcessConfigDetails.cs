using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.DataProfilling;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v4._0;
using Teleperformance.DataIngestion.Models.Entities.v4._1.DataProfilling;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration
{
    public class ProcessConfigDetail
    {
        public string flpConfigurationId { get; set; }
        public string SharedServerName { get; set; }
        public string process_name { get; set; }
        public int locationTypeId { get; set; }
        public string LocationName { get; set; }
        public string ProcessType { get; set; }
        public string sender_communication_email { get; set; }
        public string userName { get; set; }
        public string updatedByName { get; set; }
        public string loginid { get; set; }
        public string Description { get; set; }
        public int processTypeId { get; set; }
        public int regionId { get; set; }
        public string RegionName { get; set; }
        public string SubRegionId { get; set; }
        public string SubRegionName { get; set; }
        public int ClientId { get; set; }
        public string ClientName { get; set; }
        public string search_string_in_file_name { get; set; }
        public string databaseName { get; set; }
        public string tableName { get; set; }
        public string sourcePath { get; set; }
        public string destinationPath { get; set; }
        public string deltaSource { get; set; }
        public int storageAccountId { get; set; }
        public string storageContainerName { get; set; }
        public int fileServerId { get; set; }
        public string folderName { get; set; }
        public string sharedLocationServerInfoId { get; set; }
        public int blobStorageAccountId { get; set; }
        public string blobStorageContainerName { get; set; }
        public int scheduleTypeId { get; set; }
        public string schedulerType { get; set; }
        public string scheduleValue { get; set; }
        public string scheduleStartDate { get; set; }
        public string scheduleStartTime { get; set; }
        public string scheduleEndDate { get; set; }
        public string scheduleEndTime { get; set; }
        public int BlobStorageAccount { get; set; }
        public string blobStorageAccountName { get; set; }
        public string BlobSourcePath { get; set; }
        public string dedup { get; set; }
        public int hourFrequency { get; set; }
        public int[] weekDays { get; set; }
        public string parquetColMappingId { get; set; }
        public string fileColumn { get; set; }
        public string dbColumn { get; set; }
        public string formatId { get; set; }
        public string dataType { get; set; }
        public bool active { get; set; }
        public int dataSource { get; set; } //fileProcessingServerTypeId
        public string deltaStorageAccountId { get; set; }
        public string deltaStorageAccountName { get; set; }
        public string deltaContainerName { get; set; }
        public string campaignId { get; set; }
        public string internalCampaignId { get; set; }
        public string? sharePointApplicationId { get; set; }
        public string? sharePointApplicationSiteId { get; set; }
        public string? sharePointLibraryName { get; set; }
        public string? sharePointFolderPath { get; set; }
        public string? sharePointApplicationName { get; set; }
        public string? sharePointSiteName { get; set; }
        public List<FileConfigurationDetail>? FileConfigurationDetails { get; set; }
        public List<ConfigurationTableMappingDetail>? ConfigurationTableMappingDetails { get; set; }
        public List<CustomSchedulerDetail>? CustomSchedulerDetails { get; set; }

        public List<FileColumnMapping>? FileColumnMapping { get; set; }
        public List<ConfigurationSecurityGroupMapping>? ConfigurationSecurityGroupMappingList { get; set; }

        public List<RuleSetDto>? FlpRuleSet { get; set; }
    }
    public class FileConfigurationDetail
    {
        public string FlpConfigurationId { get; set; }
        public string Delimiter { get; set; }
        public string QuoteCharacter { get; set; }
        public bool IsHeaderProvided { get; set; }
        public int SkipRows { get; set; }
        public int SkipFooterRows { get; set; }
        public string KeyColumnList { get; set; }
        public string ColumnNameList { get; set; }
        public string ConvertDatatypesColumnList { get; set; }
        public string dedup { get; set; }
        public bool IgnoreDuplicateRows { get; set; }
        public bool DoNotArchiveFile { get; set; }
        public bool KeepFirstRow { get; set; }
        public bool SpanishToEnglish { get; set; }
        public bool RomanNumeralsOnly { get; set; }
        public bool SkipEmptyLines { get; set; }
        public string landingLayerPrefix { get; set; }
        public int dateFormatId { get; set; }
        public int timeFormatId { get; set; }

        public string landingLayerFileExtension { get; set; }
        public string landingLayerRegex { get; set; }

    }
    public class ConfigurationTableMappingDetail
    {
        public string FlpConfigurationId { get; set; }
        public string TableName { get; set; }
        public string? DatabaseName { get; set; }
        public int DatabaseConfigurationId { get; set; }
        public bool DropMainTable { get; set; }
        public bool DropHistoryTable { get; set; }
        public bool ValidateFileSchema { get; set; }

        public bool MergeData { get; set; }

        public bool CreateHistoryTable { get; set; }

        public string DeltaJobId { get; set; }
        public string landingLayerAcceptedPath { get; set; }
        public string landingLayerRejectedPath { get; set; }

    }
    public class ScheduerType
    {
        public int schedulerTypeId { get; set; }
        public string schedulerType { get; set; }
    }
    public class WeekDayName
    {
        public int id { get; set; }
        public string weekDayName { get; set; }
    }
    public class FrequencyHour
    {
        public int id { get; set; }
        public string frequencyHour { get; set; }
    }
    public class CustomSchedulerDetail
    {
        public int frequencyHoursId { get; set; }
        public int weekDaysId { get; set; }
    }
}
