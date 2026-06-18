using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Helpers
{
    public class ServiceHelper
    {

        public async Task<bool> AnyBlobExistsAsync(string storageAccountName,string storageAccountKey,string containerName, string sourcePath, string searchStringInFileName, string sasKey, bool sasToken)
        {
            // Build the container client
            BlobContainerClient containerClient;
            if (sasToken)
            {
                // Example: https://{account}.blob.core.windows.net/{container}?{sas}
                // If you already have a full SAS URL, just new Uri(sasUrl) is fine.
                string sasUrl = FlpConfigurationHelper.GetBlobConnectionStringBySasKey(storageAccountName, sasKey);
                containerClient = new BlobContainerClient(new Uri(sasUrl));
            }
            else
            {
                string connectionString = GetBlobConnectionString(storageAccountName, storageAccountKey);
                containerClient = new BlobContainerClient(connectionString, containerName);
            }

            // Normalize prefix (treat sourcePath as a "folder")
            string prefix = sourcePath?.TrimStart('/'); // Azure virtual folders don't start with '/'
            if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith("/"))
                prefix += "/";

            // Use hierarchical listing to avoid including subdirectories
            await foreach (var blobPage in containerClient.GetBlobsByHierarchyAsync(prefix: prefix, delimiter: "/").AsPages())
            {
                // Check the blobs in the current "folder"
                foreach (var blobItem in blobPage.Values.Where(item => item.IsBlob))
                {
                    if (string.IsNullOrWhiteSpace(searchStringInFileName) ||
                        blobItem.Blob.Name.Contains(searchStringInFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true; // Found a matching blob in the specified folder
                    }
                }
            }
            return false; // No blobs matched in the specified folder
        }

        public async Task<bool> AnyBlobExistsAsync1(
            string storageAccountName,
            string storageAccountKey,
            string containerName,
            string sourcePath,                  // e.g., "folderA/folderB/" (with or without trailing '/')
            string searchStringInFileName,      // optional; can be null/empty
            string sasKey,                      // only used if sasToken == true
            bool sasToken)
            {
                // Build the container client
                BlobContainerClient containerClient;
                if (sasToken)
                {
                    // Example: https://{account}.blob.core.windows.net/{container}?{sas}
                    // If you already have a full SAS URL, just new Uri(sasUrl) is fine.
                    string sasUrl = FlpConfigurationHelper.GetBlobConnectionStringBySasKey(storageAccountName, sasKey);
                    containerClient = new BlobContainerClient(new Uri(sasUrl));
                }
                else
                {
                    string connectionString = GetBlobConnectionString(storageAccountName, storageAccountKey);
                    containerClient = new BlobContainerClient(connectionString, containerName);
                }

                // Normalize prefix (treat sourcePath as a "folder")
                string prefix = sourcePath?.TrimStart('/'); // Azure virtual folders don't start with '/'
                if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith("/"))
                    prefix += "/";

                // Enumerate blobs using a prefix to avoid scanning entire container
                await foreach (BlobItem blob in containerClient.GetBlobsAsync(prefix: prefix, traits: BlobTraits.None, states: BlobStates.None))
                {

                    // Skip virtual directories, which are blobs ending with a '/'
                    if (blob.Name.EndsWith("/"))
                    {
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(searchStringInFileName) ||
                            blob.Name.Contains(searchStringInFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true; // Found at least one match
                    }
                }

                return false; // No blobs matched
            }

    public async Task<List<BlobClient>> FindBlobsInFoldersAsync(string storageAccountName,     string storageAccountKey,
     string containerName,  string sourcePath,  string searchStringInFileName,string sasKey,bool sasToken)
        {
            List<BlobClient> matchingBlobs = new List<BlobClient>();
            BlobContainerClient containerClient = null; 
            string blobConnectionString = "";          

            // Determine connection method
            if (sasToken)
            {
                // Use SAS token to create container client
                blobConnectionString = FlpConfigurationHelper.GetBlobConnectionStringBySasKey(storageAccountName,sasKey);
                Uri containerUri = new Uri(blobConnectionString);

                // Pass the Uri to the BlobContainerClient constructor
                containerClient = new BlobContainerClient(containerUri);

               
            }
            else
            {
                // Use connection string
                 blobConnectionString = GetBlobConnectionString(storageAccountName, storageAccountKey);
                BlobServiceClient _blobServiceClient = new BlobServiceClient(blobConnectionString);
                // Get the blob container client
                containerClient = _blobServiceClient.GetBlobContainerClient(containerName);



               
            }

            string blobStorageSourcePath = !string.IsNullOrWhiteSpace(sourcePath)
                ? sourcePath.Substring(0, sourcePath.LastIndexOf('/'))
                : null;

            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {
                string folderPath = blobItem.Name.Contains("/")
                    ? blobItem.Name.Substring(0, blobItem.Name.LastIndexOf('/'))
                    : null;

                bool isPathMatch = !string.IsNullOrWhiteSpace(blobStorageSourcePath) &&
                                   !string.IsNullOrWhiteSpace(folderPath) &&
                                   blobStorageSourcePath.Contains(folderPath, StringComparison.OrdinalIgnoreCase);

                bool isNameMatch = !string.IsNullOrWhiteSpace(searchStringInFileName) &&
                                   blobItem.Name.Contains(searchStringInFileName, StringComparison.OrdinalIgnoreCase);

                if ((isPathMatch && (isNameMatch || string.IsNullOrWhiteSpace(searchStringInFileName))) ||
                    (!isPathMatch && isNameMatch) ||
                    (string.IsNullOrWhiteSpace(blobStorageSourcePath) && string.IsNullOrWhiteSpace(searchStringInFileName)))
                {
                    matchingBlobs.Add(containerClient.GetBlobClient(blobItem.Name));
                }
            }

            return matchingBlobs;
        }


        public async Task<List<BlobClient>> FindBlobsInFoldersAsync(string storageAccountName, string storageAccountKey, string containerName, string sourcePath, string searchStringInFileName)
        {
            List<BlobClient> matchingBlobs = new List<BlobClient>();
            string blobConnectionString = GetBlobConnectionString(storageAccountName, storageAccountKey);
            BlobServiceClient _blobServiceClient = new BlobServiceClient(blobConnectionString);
            // Get the blob container client
            BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName);



            


            string blobStorageSourcePath = !string.IsNullOrWhiteSpace(sourcePath) ? sourcePath.Substring(0, sourcePath.LastIndexOf('/')) : null;
            // Iterate through all blobs in the container
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {
                //TODO: NEED TO USE  ONLY ONE CONDITION IN FUTURE - REMOVE EXTRA ELSE IF CONDITION
                //Check if the blob's name ends with the file name you're searching for
                if (!string.IsNullOrWhiteSpace(blobStorageSourcePath))
                {
                    //check path
                    // Extract folder path by removing the file name
                    string folderPath = blobItem.Name.Contains("/") ? blobItem.Name?.Substring(0, blobItem.Name.LastIndexOf('/')) : null;

                    if (!string.IsNullOrWhiteSpace(folderPath) && blobStorageSourcePath.Contains(folderPath, StringComparison.OrdinalIgnoreCase))
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

        public async Task<List<BlobClient>> FindLandingLayerBlobsInFoldersAsync(string storageAccountName, string storageAccountKey,
string containerName, string sourcePath, string searchStringInFileName, string sasKey, bool sasToken)
        {
            List<BlobClient> matchingBlobs = new List<BlobClient>();
            BlobContainerClient containerClient = null;
            string blobConnectionString = "";

            // Determine connection method
            if (sasToken)
            {
                // Use SAS token to create container client
                blobConnectionString = FlpConfigurationHelper.GetBlobConnectionStringBySasKey(storageAccountName, sasKey);
                Uri containerUri = new Uri(blobConnectionString);

                // Pass the Uri to the BlobContainerClient constructor
                containerClient = new BlobContainerClient(containerUri);
            }
            else
            {
                // Use connection string if sasToken is false
                blobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(storageAccountName, storageAccountKey);
                containerClient = new BlobContainerClient(blobConnectionString, containerName);
            }

            // Normalize the sourcePath to ensure it's treated as a folder prefix.
            string prefix = sourcePath ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(prefix) && !prefix.EndsWith("/"))
            {
                prefix += "/";
            }

            // Use hierarchical listing to avoid including subdirectories
            await foreach (var blobPage in containerClient.GetBlobsByHierarchyAsync(prefix: prefix, delimiter: "/").AsPages())
            {
                // Check the blobs in the current "folder"
                foreach (var blobItem in blobPage.Values.Where(item => item.IsBlob))
                {
                    // The prefix search has already filtered by path. Now, check the file name.
                    bool isNameMatch = string.IsNullOrWhiteSpace(searchStringInFileName) ||
                                       blobItem.Blob.Name.Contains(searchStringInFileName, StringComparison.OrdinalIgnoreCase);

                    if (isNameMatch)
                    {
                        BlobClient blobClient = containerClient.GetBlobClient(blobItem.Blob.Name);
                        if (await blobClient.ExistsAsync())
                        {
                            matchingBlobs.Add(blobClient);
                        }
                    }
                }
            }

            return matchingBlobs;
        }
        public async Task<List<BlobClient>> FindLandingLayerBlobsInFoldersAsync1(string storageAccountName, string storageAccountKey,
string containerName, string sourcePath, string searchStringInFileName, string sasKey, bool sasToken)
        {
            List<BlobClient> matchingBlobs = new List<BlobClient>();
            BlobContainerClient containerClient = null;
            string blobConnectionString = "";

            // Determine connection method
            if (sasToken)
            {
                // Use SAS token to create container client
                blobConnectionString = FlpConfigurationHelper.GetBlobConnectionStringBySasKey(storageAccountName, sasKey);
                Uri containerUri = new Uri(blobConnectionString);

                // Pass the Uri to the BlobContainerClient constructor
                containerClient = new BlobContainerClient(containerUri);
            }
            else
            {
                // Use connection string if sasToken is false
                blobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(storageAccountName, storageAccountKey);
                containerClient = new BlobContainerClient(blobConnectionString, containerName);
            }

            // Normalize the sourcePath to ensure it's treated as a folder prefix.
            string prefix = sourcePath ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(prefix) && !prefix.EndsWith("/"))
            {
                prefix += "/";
            }


           

            // Use the normalized prefix for efficient, server-side filtering.
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(prefix: prefix))
            {
                // The prefix search has already filtered by path. Now, check the file name.
                // If searchStringInFileName is null or empty, isNameMatch will be true.
                bool isNameMatch = string.IsNullOrWhiteSpace(searchStringInFileName) ||
                                   blobItem.Name.Contains(searchStringInFileName, StringComparison.OrdinalIgnoreCase);

                if (isNameMatch)
                {
                    BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);
                    if (await blobClient.ExistsAsync())
                    {
                       
                        matchingBlobs.Add(blobClient);
                    }
                }
            }       

            return matchingBlobs;
        }

        public static string GetBlobConnectionString(string storageAccountName, string storageAccountKey)
        {
            string blobConnectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageAccountKey};EndpointSuffix=core.windows.net"; ;
            return blobConnectionString;

        }

    }
}
