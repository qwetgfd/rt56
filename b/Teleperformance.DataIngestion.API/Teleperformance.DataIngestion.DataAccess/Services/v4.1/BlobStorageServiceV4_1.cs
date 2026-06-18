using AngleSharp.Dom;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using DocumentFormat.OpenXml.Bibliography;
using Microsoft.Extensions.Logging;
using NPOI.SS.Formula.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Models.v4._1;

namespace Teleperformance.DataIngestion.DataAccess.Services.v4._1
{
    public class BlobStorageServiceV4_1 : IBlobStorageServiceV4_1
    {
        ILogger<BlobStorageServiceV4_1> _logger;
        public BlobStorageServiceV4_1(ILogger<BlobStorageServiceV4_1> logger)
        {
              _logger = logger;  
        }
        public async Task<(bool ret, FlpProcessTempFileModel)> CopyBlobUsingStreamAsync(BlobClient sourceBlobClient, BlobClient destinationBlobClient)
        {
            FlpProcessTempFileModel flpProcessTempFile = new FlpProcessTempFileModel();
            bool ret = false;
            try
            {
                using var sourceStream = await sourceBlobClient.OpenReadAsync(); // read blob from source

                var response = await destinationBlobClient.UploadAsync(sourceStream, overwrite: true);
                ret = response != null && response.GetRawResponse().Status == 201;
                if (ret)
                {
                    flpProcessTempFile.CanGenerateSasUri = destinationBlobClient.CanGenerateSasUri;
                    flpProcessTempFile.Name = destinationBlobClient.Name;
                    flpProcessTempFile.Uri = destinationBlobClient.Uri.ToString();
                    flpProcessTempFile.CanGenerateSasUri = destinationBlobClient.CanGenerateSasUri;
                }

            }
            catch (Exception ex)
            {

                throw new Exception($"Error : CopyBlobUsingStreamAsync(): {ex.Message}");
            }
            return (ret, flpProcessTempFile);
        }

        public BlobClient GetBlobClientDetails(string blobName, string blobConnectionString, string containerName)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(blobConnectionString);
            try
            {
                BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
                BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
                return blobClient;
            }
            
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message.ToString()} for container name {containerName}");
            }
           
        }

        public BlobClient GetBlobClientDetailsBySasToken(string blobName, string blobServiceUriWithSas)
        {
            // blobServiceUriWithSas should be like:
            // "https://<storage-account-name>.blob.core.windows.net/<container-name>?<sas-token>"

            // Convert the string to a Uri object
            Uri containerUri = new Uri(blobServiceUriWithSas);

            // Pass the Uri to the BlobContainerClient constructor
            BlobContainerClient blobContainerClient = new BlobContainerClient(containerUri);

            BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
            return blobClient;

        }
        public async  Task<(bool ret, FlpProcessTempFileModel?)> CopyFileSourceBlobToDestinationBlobAsync(BlobClient sourceBlobClient, BlobClient destinationBlobClient)
        {
            FlpProcessTempFileModel flpProcessTempFile = new FlpProcessTempFileModel();
            bool ret = false;
            try
            {
               
                // Start the copy operation
                await destinationBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);

                // Optionally, wait for the copy operation to complete
                BlobProperties properties;
                do
                {
                    properties = await destinationBlobClient.GetPropertiesAsync();
                    await Task.Delay(100); // Wait a short period before checking again
                }
                while (properties.CopyStatus == CopyStatus.Pending);

                // If the copy is successful, mark the result as true and delete the original blob
                if (properties.CopyStatus == CopyStatus.Success)
                {
                    flpProcessTempFile.CanGenerateSasUri = destinationBlobClient.CanGenerateSasUri;
                    flpProcessTempFile.Name = destinationBlobClient.Name;
                    flpProcessTempFile.Uri = destinationBlobClient.Uri.ToString();
                    flpProcessTempFile.CanGenerateSasUri = destinationBlobClient.CanGenerateSasUri;
                    ret = true;
                }

            }
            catch (Exception ex)
            {
                ret = false;
                flpProcessTempFile = null;
                _logger.LogError(ex.Message.ToString());
            }
            return (ret, flpProcessTempFile);

        }


      
        public async Task<(bool ret, FlpProcessTempFile)> CreateCsvFileFromSourceFile(BlobClient sourceBlobClient, BlobClient destinationBlobClient,
           string destinationContainerName, string destinationConnectionString)
        {
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();
            bool ret = false;
            try
            {
                // 1. Download Excel file from Blob
                using var excelStream = await sourceBlobClient.OpenReadAsync();

                // 2. Convert Excel to CSV
                string csvContent = ExcelHelper.ConvertExcelStreamToCsv(excelStream);
                //// 3. Upload CSV to destination Blob
                string csvBlobName = Path.ChangeExtension(destinationBlobClient.Name, ".csv");
                BlobClient csvBlobClient = GetBlobClientDetails(csvBlobName, destinationConnectionString, destinationContainerName);

                using var csvStream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
                var response = await csvBlobClient.UploadAsync(csvStream, overwrite: true);
                ret = response != null && response.GetRawResponse().Status == 201;





                if (ret)
                {
                    // 4. Optionally delete the original Excel file
                    //   await sourceBlobClient.DeleteIfExistsAsync();

                    // Set FlpProcessTempFile properties to reference the CSV blob
                    flpProcessTempFile.CsvFile = new BlobLocationCsvFile
                    {
                        CsvName = csvBlobClient.Name,
                        CsvUri = csvBlobClient.Uri.ToString(),
                        CsvBlobContainerName = destinationContainerName,
                        CsvCanGenerateSasUri = csvBlobClient.CanGenerateSasUri
                    };

                }

            }
            catch (Exception ex)
            {
                ret = false;
                throw new Exception($"Error in CreateCsvFileFromSourceFile: {ex.Message}", ex);
            }
            return (ret, flpProcessTempFile);
        }

        public async Task<bool> DeleteBlobAsync(BlobClient blobClient)
        {
            // Delete the blob if it exists
            bool deleted = await blobClient.DeleteIfExistsAsync();
            return deleted;
        }

        public async Task<bool> DeleteTempFileBlobAsync(string blobName, string connectionstring, string containerName)
        {
            // Initialize the BlobServiceClient for the destination
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionstring);
            // Get a reference to the destination blob
            BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
            // Get a reference to the destination blob
            BlobClient destinationBlob = blobContainerClient.GetBlobClient(blobName);
            // Delete the blob if it exists
            bool deleted = await destinationBlob.DeleteIfExistsAsync();
            return deleted;
        }

        public async Task<bool> DeleteTempFileBlobAsync(string blobName, string sasUrl)
        {
            // Create container client using SAS URL
            BlobContainerClient containerClient = new BlobContainerClient(new Uri(sasUrl));

            // Get reference to the blob
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            // Delete blob if exists
            bool deleted = await blobClient.DeleteIfExistsAsync();

            return deleted;
        }

        /// <summary>
        /// Builds the JSON and uploads it to Azure Blob Storage using a SAS URL.
        /// </summary>
        /// <param name="configurationFileColumns">Source rows containing DbColumn and dataType.</param>
        /// <param name="blobSasUrl">Full blob URL including SAS token (e.g., https://account.blob.core.windows.net/container/path/file.json?sv=...)</param>
        /// <returns>Task</returns>
        /// 
        public async Task<bool> UploadColumnJsonAsync(IEnumerable<dynamic> configurationFileColumns,string blobSasUrl)
        {
            try
            {
             
                var uri = new Uri(blobSasUrl);
                if (string.IsNullOrWhiteSpace(uri.Query) || !uri.Query.Contains("sv=", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("Blob SAS URL must include query parameters (?sv=...).");
                    return false;
                }

                // Build payload
                var payload = new
                {
                    columnWithDataTypeMessage = configurationFileColumns
                        .Select(c => new { column = (string)c.DbColumn, dataType = (string)c.dataType })
                        .ToList()
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                var bytes = Encoding.UTF8.GetBytes(json);

                // Upload
                var blobClient = new BlobClient(uri);
                using var ms = new MemoryStream(bytes);
                var headers = new BlobHttpHeaders { ContentType = "application/json; charset=utf-8" };

                await blobClient.UploadAsync(ms, new BlobUploadOptions { HttpHeaders = headers });
                _logger.LogInformation("Uploaded JSON to: {Url}", blobSasUrl);
                return true;
            }
            catch (RequestFailedException rfe)
            {
                _logger.LogError(rfe, "Azure error. Status: {Status} Code: {Code} Message: {Msg}", rfe.Status, rfe.ErrorCode, rfe.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed: {Msg}", ex.Message);
                return false;
            }
        }


    
    }
}
