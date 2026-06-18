using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess
{
    public class FlpConfiguration : BaseSharedLocation
    {
        public string flpConfigurationId { get; set; }
        public int locationTypeId { get; set; }
        public int destinationLocationTypeId { get; set; }
        public string process_name { get; set; }        
        public string sender_communication_email { get; set; }
        public string loginid { get; set; }
        public string sourcePath { get; set; }
        public string destinationPath { get; set; }
        public string support_communication_email { get; set; }
        public string billing_client_name { get; set; }
        public int processTypeId { get; set; }
        public string sourceStorageAccount { get; set; }
        public string sourceContainerName { get; set; }
        public string sourceStorageAccountKey { get; set; }
        public string sourceServerName { get; set; }
        public string search_string_in_file_name { get; set; }
        public string uploadedFileName { get; set; }
        public string datalakeStorageAccountPath { get; set; }
        public string databricksAPIToken { get; set; }
        public string databricksInstance { get; set; }
        public string databricksAPIVersion { get; set; }
        public bool processModified { get; set; }
        public string jobId { get; set; }
        public int? fileProcessingServerTypeId { get; set; }
        public string sasKey { get; set; }
        public bool sasKeyToken { get; set; }
        public bool UIValidation { get; set; }
        public bool BEValidation { get; set; }
        public string silverTableName { get; set; }
        public string goldTableName { get; set; }
        public string securityGroupId { get; set; }
        public string? campaignId { get; set; }
        #region SharePoint Workspace - AY
        public Guid? sharePointApplicationId { get; set; }
        public Guid? sharePointApplicationSiteId { get; set; }
        public string? sharePointLibraryName { get; set; }
        public string? sharePointFolderPath { get; set; }
        #endregion
    }
}
