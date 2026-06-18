using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Models.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IBlobStorageService
    {
        Task<List<BlobClient>> FindBlobsInFoldersAsync(string storageAccountName, string storageAccountKey, string containerName, string sourcePath, string searchStringInFileName);
        Task<FlpProcessTempFile> MoveBlobAndDeletFileAsync(string sourceBlobUrl, string sourceBlobName, string sourceBlobConnectionString, string sourceBlobContainer, string destinationBlobConnectionString, string destinationContainerName, string destinationBlobPath);
        Task<(FlpProcessTempFile, BlobClient)> MovedFileToTempAsync(string sourceBlobUrl, string sourceBlobName, string sourceBlobConnectionString, string sourceBlobContainer, string destinationBlobConnectionString, string destinationContainerName, string destinationBlobPath);

        Task<bool> MoveParquetFileToArchiveAsync(BlobClient blParquetFileLocation, string destinationBlobConnectionString, string destinationContainerName, string destinationBlobPath);

        Task<Stream> ReadParquetFromBlobAsync(BlobClient parquetBlobClient);
        Task<bool> DeleteBlobAsync(BlobClient blobClient);
        Task<bool> DeleteTempFileBlobAsync(string blobName, string connectionstring, string containerName);

        BlobClient GetBlobClientDetails(string blobName, string blobConnectionString, string containerName);

        Task<(bool ret, FlpProcessTempFile)> CopyFileOnPremToDestinationBlobAsync(string onPremFilePath, BlobClient destinationBlobClient);

        Task<(bool ret, FlpProcessTempFile)> CopyFileSourceBlobToDestinationBlobAsync(BlobClient sourceBlobClient, BlobClient destinationBlobClient);

        Task<(bool, FlpProcessTempFile)> CopyFileBlobToOnPremAsync(string onPremFilePath, BlobClient blobClient);
        Task<(bool ret, FlpProcessTempFile)> CreateCsvFileFromSourceFile(BlobClient sourceBlobClient, BlobClient destinationBlobClient,
           string destinationContainerName, string destinationConnectionString);
        Task<bool> DeleteAllBlobsAsync(string connectionString, string containerName);

        BlobClient GetBlobClientDetailsBySasToken(string blobName, string blobServiceUriWithSas);
        Task<(bool, string)> UploadColumnJsonAsync(IEnumerable<dynamic> configurationFileColumns, string blobSasUrl);
    }
}
