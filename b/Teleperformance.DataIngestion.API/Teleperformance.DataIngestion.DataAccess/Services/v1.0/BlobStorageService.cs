using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ExcelDataReader;
using System.Data;
using System.Text;
using System.Text.Json;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Models.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Services.v1._0
{
    public class BlobStorageService: IBlobStorageService
    {

        public async Task<List<BlobClient>> FindBlobsInFoldersAsync(string storageAccountName, string storageAccountKey, string containerName, string sourcePath, string searchStringInFileName)
        {
            List<BlobClient> matchingBlobs = new List<BlobClient>();
            string blobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(storageAccountName, storageAccountKey);
            BlobServiceClient _blobServiceClient = new BlobServiceClient(blobConnectionString);
            // Get the blob container client
            BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            string blobStorageSourcePath = !string.IsNullOrWhiteSpace(sourcePath) ? sourcePath.Substring(0, sourcePath.LastIndexOf('/')) : null;
            // Iterate through all blobs in the container
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {

                //Check if the blob's name ends with the file name you're searching for
                if (!string.IsNullOrWhiteSpace(blobStorageSourcePath))
                {
                    //check path
                    // Extract folder path by removing the file name
                    string folderPath = blobItem.Name.Contains("/") ? blobItem.Name?.Substring(0, blobItem.Name.LastIndexOf('/')) : null;

                    if (!string.IsNullOrWhiteSpace(folderPath) && folderPath.Contains(blobStorageSourcePath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(searchStringInFileName))
                        {
                            if (blobItem.Name.Contains(searchStringInFileName, StringComparison.OrdinalIgnoreCase))
                            {
                                matchingBlobs.Add(containerClient.GetBlobClient(blobItem.Name));
                            }

                        }
                        else
                        {
                            matchingBlobs.Add(containerClient.GetBlobClient(blobItem.Name));
                        }

                    }

                }
                else if (!string.IsNullOrWhiteSpace(searchStringInFileName))
                {
                    if (blobItem.Name.Contains(searchStringInFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingBlobs.Add(containerClient.GetBlobClient(blobItem.Name));
                    }
                }
                else
                {
                    matchingBlobs.Add(containerClient.GetBlobClient(blobItem.Name));
                }


            }

            return matchingBlobs;
        }
        public async Task<FlpProcessTempFile> MoveBlobAndDeletFileAsync(string sourceBlobUrl, string sourceBlobName, string sourceBlobConnectionString, string sourceBlobContainer, string destinationBlobConnectionString, string destinationContainerName, string destinationBlobPath)
        {
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();
            // Parse the source blob URL
            // Initialize the BlobServiceClient for the destination
            BlobServiceClient destinationBlobServiceClient = new BlobServiceClient(destinationBlobConnectionString);
            BlobServiceClient sourceBlobServiceClient = new BlobServiceClient(sourceBlobConnectionString);
            // Get the destination container client
            BlobContainerClient sourceBlobConatinater = sourceBlobServiceClient.GetBlobContainerClient(sourceBlobContainer);
            // Get the original BlobClient from the source URL
            BlobClient sourceBlobClient = sourceBlobConatinater.GetBlobClient(sourceBlobName);

            string destinationBlobName = $"{destinationBlobPath}{sourceBlobClient.Name.Split('/').Last()}";
            // Get the destination container client
            BlobContainerClient destinationContainerClient = destinationBlobServiceClient.GetBlobContainerClient(destinationContainerName);


            // Get a reference to the destination blob
            BlobClient destinationBlob = destinationContainerClient.GetBlobClient(destinationBlobName);

            // Copy the blob from the source URL to the destination blob
            await destinationBlob.StartCopyFromUriAsync(sourceBlobClient.Uri);

            // Optionally, wait for the copy operation to complete
            BlobProperties properties;
            do
            {
                properties = await destinationBlob.GetPropertiesAsync();
                await Task.Delay(100); // Wait a short period before checking again
            }
            while (properties.CopyStatus == CopyStatus.Pending);

            // If the copy is successful, delete the original blob using its URL
            if (properties.CopyStatus == CopyStatus.Success)
            {

                await sourceBlobClient.DeleteIfExistsAsync();
                flpProcessTempFile.CanGenerateSasUri = destinationBlob.CanGenerateSasUri;
                flpProcessTempFile.Name = destinationBlob.Name;
                flpProcessTempFile.Uri = destinationBlob.Uri.ToString();
                flpProcessTempFile.CanGenerateSasUri = destinationBlob.CanGenerateSasUri;
                Console.WriteLine($"Moved blob from '{sourceBlobUrl}' to '{destinationBlobPath}' in container '{destinationContainerName}'.");
            }
            else
            {
                Console.WriteLine($"Failed to copy blob from '{sourceBlobUrl}' to '{destinationBlobPath}'. Copy status: {properties.CopyStatus}");
            }

            return flpProcessTempFile;
        }


        public async Task<(FlpProcessTempFile, BlobClient)> MovedFileToTempAsync(string sourceBlobUrl, string sourceBlobName, string sourceBlobConnectionString, string sourceBlobContainer, string destinationBlobConnectionString, string destinationContainerName, string destinationBlobPath)
        {
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();
            // Parse the source blob URL Source1 into temp
            // Initialize the BlobServiceClient for the destination
            BlobServiceClient destinationBlobServiceClient = new BlobServiceClient(destinationBlobConnectionString);
            BlobServiceClient sourceBlobServiceClient = new BlobServiceClient(sourceBlobConnectionString);
            // Get the destination container client
            BlobContainerClient sourceBlobConatinater = sourceBlobServiceClient.GetBlobContainerClient(sourceBlobContainer);
            // Get the original BlobClient from the source URL
            BlobClient sourceBlobClient = sourceBlobConatinater.GetBlobClient(sourceBlobName);

            string destinationBlobName = $"{destinationBlobPath}{sourceBlobClient.Name.Split('/').Last()}";
            // Get the destination container client
            BlobContainerClient destinationContainerClient = destinationBlobServiceClient.GetBlobContainerClient(destinationContainerName);


            // Get a reference to the destination blob
            BlobClient destinationBlob = destinationContainerClient.GetBlobClient(destinationBlobName);

            // Copy the blob from the source URL to the destination blob
            await destinationBlob.StartCopyFromUriAsync(sourceBlobClient.Uri);

            // Optionally, wait for the copy operation to complete
            BlobProperties properties;
            do
            {
                properties = await destinationBlob.GetPropertiesAsync();
                await Task.Delay(100); // Wait a short period before checking again
            }
            while (properties.CopyStatus == CopyStatus.Pending);

            // If the copy is successful, delete the original blob using its URL
            if (properties.CopyStatus == CopyStatus.Success)
            {

                // await sourceBlobClient.DeleteIfExistsAsync();
                flpProcessTempFile.CanGenerateSasUri = destinationBlob.CanGenerateSasUri;
                flpProcessTempFile.Name = destinationBlob.Name;
                flpProcessTempFile.Uri = destinationBlob.Uri.ToString();
                flpProcessTempFile.CanGenerateSasUri = destinationBlob.CanGenerateSasUri;
                //Console.WriteLine($"Moved blob from '{sourceBlobUrl}' to '{destinationBlobPath}' in container '{destinationContainerName}'.");
            }
            else
            {
                //Console.WriteLine($"Failed to copy blob from '{sourceBlobUrl}' to '{destinationBlobPath}'. Copy status: {properties.CopyStatus}");
            }

            return (flpProcessTempFile, sourceBlobClient);
        }

        public async Task<bool> MoveParquetFileToArchiveAsync(BlobClient blParquetFileLocation, string destinationBlobConnectionString, string destinationContainerName, string destinationBlobPath)
        {
            bool ret = false;
            // Parse the source blob URL
            // Initialize the BlobServiceClient for the destination
            BlobServiceClient destinationBlobServiceClient = new BlobServiceClient(destinationBlobConnectionString);

            string destinationBlobName = $"{destinationBlobPath}/{blParquetFileLocation.Name.Split('/').Last()}";
            // Get the destination container client
            BlobContainerClient destinationContainerClient = destinationBlobServiceClient.GetBlobContainerClient(destinationContainerName);


            // Get a reference to the destination blob
            BlobClient destinationBlob = destinationContainerClient.GetBlobClient(destinationBlobName);

            // Copy the blob from the source URL to the destination blob
            await destinationBlob.StartCopyFromUriAsync(blParquetFileLocation.Uri);

            // Optionally, wait for the copy operation to complete
            BlobProperties properties;
            do
            {
                properties = await destinationBlob.GetPropertiesAsync();
                await Task.Delay(100); // Wait a short period before checking again
            }
            while (properties.CopyStatus == CopyStatus.Pending);

            // If the copy is successful, delete the original blob using its URL
            if (properties.CopyStatus == CopyStatus.Success)
            {

                await blParquetFileLocation.DeleteIfExistsAsync();
                ret = true;
            }
            return ret;
        }

        public async Task<bool> MoveParquetFileToArchiveAsync1(BlobClient bcParquetFileLocation, string destinationBlobConnectionString, string destinationContainerName, string destinationBlobPath)
        {
            bool ret = false;
            // Parse the source blob URL
            // Initialize the BlobServiceClient for the destination
            BlobServiceClient destinationBlobServiceClient = new BlobServiceClient(destinationBlobConnectionString);

            string destinationBlobName = $"{destinationBlobPath}/{bcParquetFileLocation.Name.Split('/').Last()}";
            // Get the destination container client
            BlobContainerClient destinationContainerClient = destinationBlobServiceClient.GetBlobContainerClient(destinationContainerName);


            // Get a reference to the destination blob
            BlobClient destinationBlob = destinationContainerClient.GetBlobClient(destinationBlobName);

            // Copy the blob from the source URL to the destination blob
            await destinationBlob.StartCopyFromUriAsync(bcParquetFileLocation.Uri);

            // Optionally, wait for the copy operation to complete
            BlobProperties properties;
            do
            {
                properties = await destinationBlob.GetPropertiesAsync();
                await Task.Delay(100); // Wait a short period before checking again
            }
            while (properties.CopyStatus == CopyStatus.Pending);

            // If the copy is successful, delete the original blob using its URL
            if (properties.CopyStatus == CopyStatus.Success)
            {

                // await blParquetFileLocation.DeleteIfExistsAsync();
                ret = true;
            }
            return ret;
        }

        public async Task<bool> MoveParquetFileToArchiveAsyncV2(BlobClient bcParquetFileLocation, string destinationBlobConnectionString, string destinationContainerName, string destinationBlobPath)
        {
            bool ret = false;
            string parquetFileUri = "";

            // Parse the source blob URL
            // Initialize the BlobServiceClient for the destination
            BlobServiceClient destinationBlobServiceClient = new BlobServiceClient(destinationBlobConnectionString);

            string destinationBlobName = $"{destinationBlobPath}/{bcParquetFileLocation.Name.Split('/').Last()}";
            // Get the destination container client
            BlobContainerClient destinationContainerClient = destinationBlobServiceClient.GetBlobContainerClient(destinationContainerName);


            // Get a reference to the destination blob
            BlobClient destinationBlob = destinationContainerClient.GetBlobClient(destinationBlobName);

            // Copy the blob from the source URL to the destination blob
            await destinationBlob.StartCopyFromUriAsync(bcParquetFileLocation.Uri);

            // Optionally, wait for the copy operation to complete
            BlobProperties properties;
            do
            {
                properties = await destinationBlob.GetPropertiesAsync();
                await Task.Delay(100); // Wait a short period before checking again
            }
            while (properties.CopyStatus == CopyStatus.Pending);

            // If the copy is successful, delete the original blob using its URL
            if (properties.CopyStatus == CopyStatus.Success)
            {

                // await blParquetFileLocation.DeleteIfExistsAsync();
                ret = true;
            }
            return ret;
        }


        public async  Task<Stream> ReadParquetFromBlobAsync(BlobClient parquetBlobClient)
        {
            // Download the blob content as a stream
            BlobDownloadInfo downloadInfo = await parquetBlobClient.DownloadAsync();

            // Copy the content to a MemoryStream
            MemoryStream memoryStream = new MemoryStream();
            await downloadInfo.Content.CopyToAsync(memoryStream);

            // Reset the memory stream position to the beginning
            memoryStream.Seek(0, SeekOrigin.Begin);

            return memoryStream;
        }
        public async  Task<bool> DeleteBlobAsync(BlobClient blobClient)
        {
            // Delete the blob if it exists
            bool deleted = await blobClient.DeleteIfExistsAsync();
            return deleted;
        }

        public async  Task<bool> DeleteTempFileBlobAsync(string blobName, string connectionstring, string containerName)
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


        public async Task<bool> DeleteAllBlobsAsync(string connectionString, string containerName)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            int deleteCount = 0;
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {
                BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);
                if (await blobClient.DeleteIfExistsAsync())
                {
                    deleteCount++;
                }
            }
            return (deleteCount >0); // Returns the number of blobs deleted
        }
        public BlobClient GetBlobClientDetails(string blobName, string blobConnectionString, string containerName)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(blobConnectionString);
            BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
            return blobClient;
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




        /// <summary>
        /// 
        /// </summary>
        /// <param name="localFilePath"></param>
        /// <param name="destinationBlobConnectionString"></param>
        /// <param name="destinationContainerName"></param>
        /// <param name="destinationBlobPath"></param>
        /// <returns></returns>

        public async Task<(bool ret, FlpProcessTempFile)> CopyFileOnPremToDestinationBlobAsync(string onPremFilePath, BlobClient destinationBlobClient)// string destinationBlobConnectionString, string destinationContainerName, string destinationBlobPath)
        {
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();
            // Parse the source blob URL Source1 into temp
            // Initialize the BlobServiceClient for the destination
            //BlobServiceClient destinationBlobServiceClient = new BlobServiceClient(destinationBlobConnectionString);

            //string destinationBlobName = $"{destinationBlobPath}{onPremFilePath.Split('/').Last()}";
            //// Get the destination container client
            //BlobContainerClient destinationContainerClient = destinationBlobServiceClient.GetBlobContainerClient(destinationContainerName);

            //// Get a reference to the destination blob
            //BlobClient destinationBlob = destinationContainerClient.GetBlobClient(destinationBlobName);
            // Open the local file and upload it to Blob Storage.
            using FileStream uploadFileStream = File.OpenRead(onPremFilePath);
            await destinationBlobClient.UploadAsync(uploadFileStream, true);
            uploadFileStream.Close();
            // await sourceBlobClient.DeleteIfExistsAsync();
            flpProcessTempFile.CanGenerateSasUri = destinationBlobClient.CanGenerateSasUri;
            flpProcessTempFile.Name = destinationBlobClient.Name;
            flpProcessTempFile.Uri = destinationBlobClient.Uri.ToString();
            flpProcessTempFile.CanGenerateSasUri = destinationBlobClient.CanGenerateSasUri;
            return (true, flpProcessTempFile);
        }

        public async Task<(bool ret, FlpProcessTempFile)> CopyFileStreamToDestinationBlobAsync(Stream fileStream, BlobClient destinationBlobClient)// string destinationBlobConnectionString, string destinationContainerName, string destinationBlobPath)
        {
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();
            BlobUploadOptions options = new BlobUploadOptions
            {
                TransferOptions = new StorageTransferOptions
                {
                    // Set the maximum number of parallel transfer workers
                    MaximumConcurrency = 2,

                    // Set the initial transfer length to 8 MiB
                    InitialTransferSize = 8 * 1024 * 1024,

                    // Set the maximum length of a transfer to 4 MiB
                    MaximumTransferSize = 4 * 1024 * 1024
                }
            };
            await destinationBlobClient.UploadAsync(fileStream, options);// true);                        
            flpProcessTempFile.CanGenerateSasUri = destinationBlobClient.CanGenerateSasUri;
            flpProcessTempFile.Name = destinationBlobClient.Name;
            flpProcessTempFile.Uri = destinationBlobClient.Uri.ToString();
            flpProcessTempFile.CanGenerateSasUri = destinationBlobClient.CanGenerateSasUri;
            return (true, flpProcessTempFile);
        }


        public async Task<(bool ret, FlpProcessTempFile)> CopyFileSourceBlobToDestinationBlobAsync(BlobClient sourceBlobClient, BlobClient destinationBlobClient)
        {
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();
            bool ret = false;
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
     

        //    public async Task<(bool ret, FlpProcessTempFile)> CreateCsvFileFromSourceFile(
        //BlobClient sourceBlobClient,
        //BlobClient destinationBlobClient,
        //string destinationContainerName,
        //string destinationConnectionString)
        //    {
        //        FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();
        //        bool ret = false;
        //        try
        //        {
        //            // 1. Download Excel file from Blob
        //            using var excelStream = await sourceBlobClient.OpenReadAsync();

        //            // 2. Prepare CSV destination blob
        //            string csvBlobName = Path.ChangeExtension(destinationBlobClient.Name, ".csv");
        //            BlobClient csvBlobClient = GetBlobClientDetails(csvBlobName, destinationConnectionString, destinationContainerName);

        //            // 3. Convert and stream Excel to CSV, upload directly to Blob
        //            using var csvStream = new MemoryStream();
        //            using (var writer = new StreamWriter(csvStream, Encoding.UTF8, leaveOpen: true))
        //            {
        //                ExcelHelper.WriteExcelStreamToCsv(excelStream, writer);
        //                writer.Flush();
        //                csvStream.Position = 0;
        //                var response = await csvBlobClient.UploadAsync(csvStream, overwrite: true);
        //                ret = response != null && response.GetRawResponse().Status == 201;
        //            }

        //            // 4. Optionally delete the original Excel file
        //            // await sourceBlobClient.DeleteIfExistsAsync();

        //            // 5. Set CSV blob info in result
        //            flpProcessTempFile.CsvFile = new BlobLocationCsvFile
        //            {
        //                CsvName = csvBlobClient.Name,
        //                CsvUri = csvBlobClient.Uri.ToString(),
        //                CsvBlobContainerName = destinationContainerName,
        //                CsvCanGenerateSasUri = csvBlobClient.CanGenerateSasUri
        //            };
        //        }
        //        catch (Exception ex)
        //        {
        //            ret = false;
        //            throw new Exception($"Error in CreateCsvFileFromSourceFile: {ex.Message}", ex);
        //        }
        //        return (ret, flpProcessTempFile);
        //    }
        public async Task<(bool ret, FlpProcessTempFile)> CopyBlobUsingStreamAsync(BlobClient sourceBlobClient, BlobClient destinationBlobClient)
        {
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();
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

        public async Task<(bool, FlpProcessTempFile)> CopyFileBlobToOnPremAsync(string onPremFilePath, BlobClient blobClient)
        {
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();
            bool ret = false;
            // Parse the source blob URL
            // Initialize the BlobServiceClient for the destination          

            BlobDownloadInfo download = await blobClient.DownloadAsync();

            try
            {

                // Ensure the directory for the file exists
                string directoryPath = Path.GetDirectoryName(onPremFilePath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath); // Create directory if it does not exist
                }


                // Open the local file stream for writing
                using (FileStream fs = File.OpenWrite(onPremFilePath))
                {
                    // Copy the downloaded content to the local file
                    await download.Content.CopyToAsync(fs);
                    ret = true;
                    //flpProcessTempFile.fileUrl = onPremFilePath;
                    flpProcessTempFile.sourceTempFilePath = onPremFilePath;
                }
            }
            catch (Exception ex)
            {

                throw;
            }

            return (ret, flpProcessTempFile);
        }

        /// <summary>
        /// Builds the JSON and uploads it to Azure Blob Storage using a SAS URL. Note - this is same function is using in v4.1 - will removed this function in future
        /// </summary>
        /// <param name="configurationFileColumns">Source rows containing DbColumn and dataType.</param>
        /// <param name="blobSasUrl">Full blob URL including SAS token (e.g., https://account.blob.core.windows.net/container/path/file.json?sv=...)</param>
        /// <returns>Task</returns>
        /// 
        public async Task<(bool,string)> UploadColumnJsonAsync(IEnumerable<dynamic> configurationFileColumns, string blobSasUrl)
        {
            try
            {

                var uri = new Uri(blobSasUrl);
                if (string.IsNullOrWhiteSpace(uri.Query) || !uri.Query.Contains("sv=", StringComparison.OrdinalIgnoreCase))
                {
                   // _logger.LogError("Blob SAS URL must include query parameters (?sv=...).");
                    return (false, $"Blob SAS URL must include query parameters (?sv=...).");
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
                
                return (true, $"Successfully uploaded."); 
            }
            catch (RequestFailedException rfe)
            {
                string msg = $"Upload failed with error code {rfe.ErrorCode} and message: {rfe.Message}";
                return (true, $"Error: {msg}"); 
            }
            catch (Exception ex)
            {
                string msg = $"Upload failed with error code {ex.ToString()}";
                return (true, $"Error: {msg}");
            }
        }

    }
}
