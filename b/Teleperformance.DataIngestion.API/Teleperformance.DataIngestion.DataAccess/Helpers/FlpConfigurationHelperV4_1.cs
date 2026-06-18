using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Enums.v3._0;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using Teleperformance.DataIngestion.Models.Models.v4._1;

namespace Teleperformance.DataIngestion.DataAccess.Helpers
{
    public class FlpConfigurationHelperV4_1
    {
        public static (bool, string) checkSelectedValidOptions(Models.DTOs.v4._1.FileLoadingConfigurationProcess.ConfigurationTableMappingDto parameterConfig, string keyColumns, string dedupColumnList)
        {
            string message = "";
            bool ret = true;

            if (parameterConfig.IgnoreDuplicateRows)
            {
                if (string.IsNullOrWhiteSpace(keyColumns))
                {
                    message = "Ignore duplicate option is checked. Key column is empty";
                    ret = false;
                    // throw new Exception("Ignore duplicate option is checked but not found any unique columns");
                }
            }

            if (!string.IsNullOrWhiteSpace(keyColumns) || !string.IsNullOrWhiteSpace(dedupColumnList))
            {
                if (!parameterConfig.IgnoreDuplicateRows)
                {
                    message = "Ignore duplicate option is not checked but Found Key Columns Or dedup columns";
                    ret = false;
                    ///throw new Exception("Ignore duplicate option is not checked but Found Key Columns Or dedup columns");
                }
            }

            return (ret, message);

        }


        public static (bool, string) checkSelectedValidOptionsV2(bool ignoreDuplicateRows, string keyColumns, string dedupColumnList)
        {
            string message = "";
            bool ret = true;

            if (ignoreDuplicateRows)
            {
                if (string.IsNullOrWhiteSpace(keyColumns))
                {
                    message = "Ignore duplicate option is checked. Key column is empty";
                    ret = false;
                    // throw new Exception("Ignore duplicate option is checked but not found any unique columns");
                }
            }

            if (!string.IsNullOrWhiteSpace(keyColumns) || !string.IsNullOrWhiteSpace(dedupColumnList))
            {
                if (!ignoreDuplicateRows)
                {
                    message = "Ignore duplicate option is not checked but Found Key Columns Or dedup columns";
                    ret = false;
                    ///throw new Exception("Ignore duplicate option is not checked but Found Key Columns Or dedup columns");
                }
            }

            return (ret, message);

        }


        public static string GetFileLocation(FlpConfigurationResponseDtoV4_1 flpConfigurationRequestDto)
        {
            string fileLocation = "";

            //Blob | Shared
            if ((int)SourceLocationTypeEnum.OnPrem == flpConfigurationRequestDto.LocationTypeId)
            {
                fileLocation = flpConfigurationRequestDto.SourcePath;
            }
            if ((int)SourceLocationTypeEnum.Azure == flpConfigurationRequestDto.LocationTypeId)
            {
                fileLocation = flpConfigurationRequestDto.BlobClients.Name;
            }
            return fileLocation;
        }

        public static string GetUploadedFileId(FlpRequestDto4_1 flpRequestDto)
        {
            string uploadedFileId = string.Empty;
            if (flpRequestDto != null && flpRequestDto.BlobClients != null)
            {
                uploadedFileId = flpRequestDto?.BlobClients?.UploadedId ?? "";
            }
            else
            {
                uploadedFileId = flpRequestDto?.OnPremFileLocation?.UploadedId ?? "";
            }
            return uploadedFileId;
        }

        public static (bool, FlpProcessTempFileModel) CopyFileBlobToOnPremAsync(string tempFilePath, string flpConfigurationId, BlobClient blobClient, SharedLocationDestinationServerDto slDestinationServerDto, ISMBLibraryServices ismbLibraryServices)
        {
            FlpProcessTempFileModel flpProcessTempFile = new FlpProcessTempFileModel();

            CheckConnectivitySMBLibraryModel tempFileSMBmodel = new CheckConnectivitySMBLibraryModel
            {
                serverIP = slDestinationServerDto.ServerName,
                username = slDestinationServerDto.UserName,
                password = slDestinationServerDto.Password,
                sharedFolderName = slDestinationServerDto.FolderName,
                domain = slDestinationServerDto.Domain,
                sourceFilePath = tempFilePath,
                blobClient = blobClient
            };
            var smbResult = ismbLibraryServices.SMBRequest(tempFileSMBmodel, flpConfigurationId, SMBRequestEnum.CopyFileFromBlob);
            if (smbResult.CopiedFileFromBlob)
            {
                flpProcessTempFile.sourceTempFilePath = tempFilePath;
                return (true, flpProcessTempFile);
            }
            else
            {
                flpProcessTempFile.sourceTempFilePath = tempFilePath;
                return (false, flpProcessTempFile);
            }
        }

        public static async Task<(bool, string)> CopySourceOnPremFileToDestinationOnPrem(string flpConfigurationId, string sourcePath, string destinationPath, string backUpFileName, CheckConnectivitySMBLibraryModel sourceServerModel, SharedLocationDestinationServerDto slDestinationServerDto, ISMBLibraryServices ismbLibraryServices)
        {
            string currentTimeString = DateTime.Now.ToString("yyyyMMdd");
            string tempFolderPath = $"{destinationPath}{currentTimeString}\\";

            try
            {

                var onPremLocation = true;// Convert.ToBoolean(KeyVault.GetKeyVaultValue("OnPremLocation").Result);
                if (onPremLocation)
                {
                    CheckConnectivitySMBLibraryModel destinationServer = new CheckConnectivitySMBLibraryModel();
                    destinationServer.serverIP = slDestinationServerDto.ServerName;
                    destinationServer.sharedFolderName = slDestinationServerDto.FolderName;
                    destinationServer.sharedFolderPath = $@"{sourcePath}";
                    destinationServer.username = slDestinationServerDto.UserName;
                    destinationServer.password = slDestinationServerDto.Password;
                    destinationServer.domain = slDestinationServerDto.Domain;
                    destinationServer.fileName = "";
                    destinationServer.sourceFilePath = sourcePath;
                    destinationServer.destinationFilePath = destinationPath;
                    string tempFilePath = Path.Combine(tempFolderPath, backUpFileName);
                    var result = ismbLibraryServices.CopyFileFromOneToAnotherLocation(sourceServerModel, destinationServer, sourcePath, tempFilePath, flpConfigurationId);

                    if (result)
                    {
                        return (true, tempFilePath);
                    }
                    else
                    {
                        return (false, "Failed coy to temp folder");
                    }

                }
                else
                {
                    // Create archive folder if it doesn't exist
                    if (!Directory.Exists(tempFolderPath))
                    {
                        Directory.CreateDirectory(tempFolderPath);
                    }
                    //Move parquet file to archive folder
                    string tempFilePath = Path.Combine(tempFolderPath, backUpFileName);// Path.GetFileName(sourcePath));
                    File.Copy(sourcePath, tempFilePath, true);
                    return (true, tempFilePath);
                }



            }
            catch (Exception ex)
            {
                return (false, ex.Message.ToString());
            }

        }

        public async static Task<(bool ret, FlpProcessTempFileModel)> CopyFileOnPremToDestinationBlobAsync(string onPremFilePath, string flpConfigurationId, BlobClient destinationBlobClient, CheckConnectivitySMBLibraryModel checkConnectivitySMB, ISMBLibraryServices ismbLibraryServices)
        {
            FlpProcessTempFileModel flpProcessTempFile = new FlpProcessTempFileModel();
            //using FileStream uploadFileStream = File.OpenRead(onPremFilePath);

            try
            {
                using (var fileStream = ismbLibraryServices.SMBRequest(checkConnectivitySMB, flpConfigurationId, SMBRequestEnum.Stream).GetFileStream)
                {
                    // Get file size in bytes                 

                    await destinationBlobClient.UploadAsync(fileStream, true);
                    // uploadFileStream.Close();

                    // Get blob properties to retrieve file size
                    var properties = await destinationBlobClient.GetPropertiesAsync();
                    long fileSizeBytes = properties.Value.ContentLength;
                    double fileSizeMB = Math.Round(fileSizeBytes / (1024.0 * 1024.0), 2);

                    // Set metadata
                    flpProcessTempFile.CanGenerateSasUri = destinationBlobClient.CanGenerateSasUri;
                    flpProcessTempFile.Name = destinationBlobClient.Name;
                    flpProcessTempFile.Uri = destinationBlobClient.Uri.ToString();
                    flpProcessTempFile.FileSize = fileSizeMB; // Add this property to your model if 

                }

                return (true, flpProcessTempFile);
            }
            catch (Exception ex)
            {
                flpProcessTempFile.ErrorMessage = ex.Message.ToString();
                return (false, flpProcessTempFile);
            }
        }


        public async static Task<Stream> GetParquetFileStreamFromBlob(string blobConnectionString, string containerName, string blobName)
        {

            // deleting file from location:PHP / PHP / Citibank / parquet / xlsx dedup text file_20250928075755.parquet
            var blobClient = new BlobContainerClient(blobConnectionString, containerName);
            var blob = blobClient.GetBlobClient(blobName);
            var stream = new MemoryStream();
            await blob.DownloadToAsync(stream);
            stream.Position = 0; // Reset stream position
            return stream;
        }

        public static FlpFileColumnMappingDto AddRowNumberInColumnList()
        {
            FlpFileColumnMappingDto flpFileColumnMappingDto = new FlpFileColumnMappingDto
            {
                FileColumn = "divalidationrowno",
                DbColumn = "divalidationrowno",
                DataTypeId = (int)DatatypeNamesEnum.INT,
                dataType = "Int"
            };

            return flpFileColumnMappingDto;
        }


        public static string CreatedJsonFilePath(string parquetFileURL, string processName)
        {
            string fileLocation = "";
            string jsonFilePath = $"{parquetFileURL}";
            string url = jsonFilePath;
            int lastSlashIndex = url.LastIndexOf('/');
            string directoryUrl = lastSlashIndex > -1 ? url.Substring(0, lastSlashIndex + 1) : url;
            string folderName = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string jsonFileLocation = $"{directoryUrl}{processName}_{folderName}.json";
            return jsonFileLocation;
        }


        public static Dictionary<string, string> GetSearchColumnMapping()
        {
            return new Dictionary<string, string>
            {
                { "processName", "flp.process_name" },
                { "fileId", "uf.uploadFileId" },
                { "fileName", "uf.fileName" },
                { "statusName", "pst.statusName" },
                { "processedBy", "uf.uploadedBy" }
            };
        }

        public static string GetLandingLayerFilePath(string fileURL, string sourceLocation)
        {
            if (string.IsNullOrWhiteSpace(fileURL))
            {
                return string.Empty;
            }

            var fileUrlFolderNameList = fileURL.Split(new string[] { "\\" }, StringSplitOptions.None);
            if (fileUrlFolderNameList.Length == 0)
                return string.Empty;

            string fileLocation = $"{sourceLocation}{fileUrlFolderNameList[fileUrlFolderNameList.Length -1]}";
            return fileLocation;


        }
    }
}
