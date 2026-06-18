using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v4._1;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v2._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v4._1.LandingLayer;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration
{
    public class InsertFlpConfigurationRequest
    {
        public string FlpConfigurationId { get; set; } 
        public string ProcessName { get; set; }
        public int LocationTypeId { get; set; }
        public string? SenderCommunicationEmail { get; set; }
        public string CreatedBy { get; set; }
        public string UserName { get; set; }
        public string Description { get; set; }
        public int ProcessTypeId { get; set; }
        public int RegionId { get; set; }
        public string SubRegionId { get; set; }
        public int ClientId { get; set; }
        public string? SearchStringInFileName { get; set; }
        public int ServerLocationId { get; set; }
        public string BaseFolderName { get; set; }
        public string SourceFolderLocation { get; set; }
        public int ScheduledId { get; set; }
        public string? scheduleValue { get; set; }
        public string? ScheduledDate { get; set; }
        public string? ScheduledTime { get; set; }
        public int BlobStorageAccount { get; set; }
        public string blobContainerName { get; set; }
        public string blobSourcePath { get; set; }
        public string? configurationId { get; set; }
        public string? ScheduledEndDate { get; set; }
        public string? ScheduledEndTime { get; set; }
        public int hourFrequency { get; set; }
        public int[] weekDays { get; set; }
        public bool updateSchedular { get; set; }
        public string region { get; set; }
        public string subRegion { get; set; }
        public string clientName { get; set; }

        public int dataSource { get; set; } //this is the fileProcessingServerTypeId
        public string deltaSource { get; set; }
        public string deltaStorageAccountId { get; set; }
        public string deltaContainerName { get; set; }

        public string? campaignId { get; set; }
        public string? internalCampaignId { get; set; }

        public string? SharePointApplicationId { get; set; }
        public string? SharePointApplicationSiteId { get; set; }
        public string? SharePointLibraryName { get; set; }
        public string? SharePointFolderPath { get; set; }

        // List for FlpFileConfiguration (second stored procedure)
        public List<FlpFileConfigurationRequest> FileConfigurations { get; set; }

        // List for ConfigurationTableMapping (third stored procedure)
        public List<ConfigurationTableMappingRequest> ConfigurationTableMappings { get; set; }
        //public string? SecurityGroupId { get; set; }
        public SecurityGroup[] securityGroups { get; set; }        

    }

    public class FlpFileConfigurationRequest
    {
        public string FlpConfigurationId { get; set; } 
        public string Delimiter { get; set; }
        public string QuoteCharacter { get; set; }
        public bool IsHeaderProvided { get; set; }
        public int SkipRows { get; set; }
        public bool SkipEmptyLines { get; set; }
        public int SkipFooterRows { get; set; }
        public string KeyColumnList { get; set; }
        public string ColumnNameList { get; set; }
        public string ConvertDatatypesColumnList { get; set; }
        public string? dedup { get; set; }
        public bool IgnoreDuplicateRows { get; set; }
        public bool DoNotArchiveFile { get; set; }
        public bool SpanishToEnglish { get; set; }
        public bool RomanNumeralsOnly { get; set; }
        public bool KeepFirstRow { get; set; }
        public string? db_file_column_name_list { get; set; }
        public string? tabName { get; set; }        
        public List<int?>? landingLayerFileExtension { get; set; } = new List<int?>();
        public List<LandingLayerRegex> landingLayerRegex { get; set; } = new List<LandingLayerRegex>();
        public string? landingLayerPrefix { get; set; }
        public int? dateFormatId { get; set; }
        public int? timeFormatId { get; set; }

    }

    public class ConfigurationTableMappingRequest
    {
        public string FlpConfigurationId { get; set; } 
        public string TableName { get; set; }
        public int DatabaseConfigurationId { get; set; }
        public bool DropMainTable { get; set; }
        public bool DropHistoryTable { get; set; }
        public bool ValidateFileSchema { get; set; }

        public bool MergeData { get; set; }

        public bool CreateHistoryTable { get; set; }

        public string DeltaJobId { get; set; }
        public string? tabName { get; set; }
        public string deltaSource { get; set; }
        public string? landingLayerAcceptedPath { get; set; }
        public string? landingLayerRejectedPath { get; set; }

    }
}
