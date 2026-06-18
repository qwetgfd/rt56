using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.Models.Entities.v2._0.EmailNotification
{
    public class EmailNotification:DatabaseResponse
    {
        public string flpConfigurationId { get; set; }
        public string uploadFileId { get; set; }
        public string uploadFileName { get; set; }
        public string clientName { get; set; }
        public string subRegion { get; set; }
        public string region { get; set; }
        public string configurationName { get; set; }
        public string startTime { get; set; }
        public string endTime { get; set; }
        public string totalRecords { get; set; }
        public string processedRecords { get; set; }
        public string duplicateRecords { get; set; }
        public string statusName { get; set; }
        public string durationInSeconds { get; set; }
        public int flpProcessStatusId { get; set; }
        public bool isEmailSent { get; set; }
        public string errorMsg { get; set; }
        public string emailAddress { get; set; }
        public string sourceStorageAccount { get; set; }
        public string? sourceContainerName { get; set; }
        public string? sourceStorageAccountKey { get; set; }
        public string? blobName { get; set; }
        public string? stageName { get; set; }
        public string? backUpFileDetailsId { get; set; }
        public string Description { get; set; }
        public string tabName { get; set; }
        public bool multisheet { get; set; }
        public bool successProcess { get; set; }
        public int successCount { get; set; }
        public int failureCount { get; set; }
        public int totalFileCount { get; set; }
        public int fileProcessingServerTypeId { get; set; }


    }


}
