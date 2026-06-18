using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.Models.Models.v4._1
{
    public class LandingLayerDetails:DatabaseResponse
    {
        public int id { get; set; }
        public string process_name { get; set; }
        public string Description { get; set; }
        public string flpConfigurationId { get; set; }
        public int locationTypeId { get; set; }
        public int destinationLocationTypeId { get; set; }
        public bool is_active { get; set; }
        public string sourcePath { get; set; }
        public string destinationPath { get; set; }
        public string sender_communication_email { get; set; }
        public string support_communication_email { get; set; }
        public string loginid { get; set; }
        public int processTypeId { get; set; }

        public int RegionId { get; set; }
        public string SubRegionId { get; set; }
        public int ClientId { get; set; }

        public string region { get; set; }
        public string subRegion { get; set; }
        public string clientName { get; set; }

        public int dataSource { get; set; }   // fileProcessingServerTypeId AS dataSource

        public bool multisheet { get; set; }
        public bool sheetReferenceByIndex { get; set; }

        public string sourceStorageAccount { get; set; }
        public string sourceContainerName { get; set; }
        public string sourceStorageAccountKey { get; set; }

        public string serverName { get; set; }
        public string sharedLocationServerInfoId { get; set; }

        public string userName { get; set; }      // decrypted
        public string password { get; set; }      // decrypted
        public string domain { get; set; }
        public string folderName { get; set; }

        public string uploadedFileName { get; set; }

        public int processModified { get; set; }  // CASE output (0/1)
        public string search_string_in_file_name { get; set; }
        public string sasKey { get; set; }
        public bool sasKeyToken { get; set; }
        public  string securityGroupId { get; set; }
    }
}
