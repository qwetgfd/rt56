using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Models.v4._1;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1
{
    public interface IBlobStorageServiceV4_1
    {
        Task<bool> DeleteBlobAsync(BlobClient blobClient);
        BlobClient GetBlobClientDetails(string blobName, string blobConnectionString, string containerName);
        Task<(bool ret, FlpProcessTempFileModel?)> CopyFileSourceBlobToDestinationBlobAsync(BlobClient sourceBlobClient, BlobClient destinationBlobClient);
        Task<bool> DeleteTempFileBlobAsync(string blobName, string connectionstring, string containerName);
        Task<(bool ret, FlpProcessTempFileModel)> CopyBlobUsingStreamAsync(BlobClient sourceBlobClient, BlobClient destinationBlobClient);
        BlobClient GetBlobClientDetailsBySasToken(string blobName, string blobServiceUriWithSas);
        Task<bool> UploadColumnJsonAsync(IEnumerable<dynamic> configurationFileColumns, string blobSasUrl);
    }
}
