using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using SMBLibrary;
using SMBLibrary.Client;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using FileAttributes = SMBLibrary.FileAttributes;

namespace Teleperformance.DataIngestion.DataAccess.Services.v1._0
{
    public class SMBLibraryServices : ISMBLibraryServices
    {
        private readonly ISMBLibraryRepository _smbLibraryRepository;
        private readonly ILogger<SMBLibraryServices> _logger;
        public SMBLibraryServices(ILogger<SMBLibraryServices> logger, ISMBLibraryRepository smbLibraryRepository)
        {
            _logger = logger;
            _smbLibraryRepository = smbLibraryRepository;
        }

        
        public async Task<bool> FileExistsInRemoteLocation(CheckConnectivitySMBLibraryModel model, string flpConfigurationId)
        {
            SMB2Client smbClient = new SMB2Client();
            var addLogMessage = Convert.ToBoolean(Environment.GetEnvironmentVariable("TPDataIngestionAddSmbRequestMessage"));

            try
            {
                // 1. Connect to SMB server
                bool isConnected = smbClient.Connect(model.serverIP, SMBTransportType.DirectTCPTransport);
                if (!isConnected)
                {
                    await LogAsync("SMB client not connected.", "error", addLogMessage, flpConfigurationId);
                    return false;
                }
                await LogAsync("Connected to SMB server.", "success", addLogMessage, flpConfigurationId);

                // 2. Login
                var loginStatus = smbClient.Login(model.domain, model.username, model.password);
                if (loginStatus != NTStatus.STATUS_SUCCESS)
                {
                    await LogAsync($"Login failed with status: {loginStatus}", "error", addLogMessage, flpConfigurationId);
                    return false;
                }
                await LogAsync("Login successful.", "success", addLogMessage, flpConfigurationId);

                // 3. Connect to shared folder
                NTStatus treeStatus;
                var fileStore = smbClient.TreeConnect(model.sharedFolderName, out treeStatus);

                if (treeStatus != NTStatus.STATUS_SUCCESS)
                {
                    await LogAsync($"Failed to connect to shared folder '{model.sharedFolderName}'. Status: {treeStatus}", "error", addLogMessage, flpConfigurationId);
                    return false;
                }
                await LogAsync("Shared folder connected.", "success", addLogMessage, flpConfigurationId);

                // 4. Check if file exists inside folder
                string directoryPath = model.sharedFolderPath;//.Replace('/', '\\').TrimEnd('\\');
                string searchPattern = !string.IsNullOrWhiteSpace(model.fileName) ? $"*{model.fileName}*" : "*";


                object directoryHandle;
                var openStatus = fileStore.CreateFile(
                    out directoryHandle,
                    out _,
                    directoryPath,
                    AccessMask.GENERIC_READ,
                    FileAttributes.Directory,
                    ShareAccess.Read,
                    CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_DIRECTORY_FILE,
                    null);

                if (openStatus != NTStatus.STATUS_SUCCESS)
                {
                    await LogAsync($"Failed to open directory: '{directoryPath}'. Status: {openStatus}",
                                   "error", addLogMessage, flpConfigurationId);
                    return false;
                }

                // 5. Query file
                List<QueryDirectoryFileInformation> fileList;
                var status = fileStore.QueryDirectory(
                    out fileList,
                    directoryHandle,
                    searchPattern,
                    FileInformationClass.FileDirectoryInformation);

                fileStore.CloseFile(directoryHandle);

                // 6. Found?               
                // bool exists = fileList != null && fileList.Any(f => f is FileDirectoryInformation info && !string.IsNullOrWhiteSpace(info.FileName) && info.FileName != "." && info.FileName != "..");
                // 6. Found?               
                bool exists = fileList != null && fileList.Any(f =>
                    f is FileDirectoryInformation info &&
                    !string.IsNullOrWhiteSpace(info.FileName) &&
                    info.FileName != "." && info.FileName != ".." &&
                    (info.FileAttributes & FileAttributes.Directory) == 0);

                await LogAsync(
                    exists ? $"File matching '{searchPattern}' found in '{directoryPath}'." : $"File matching '{searchPattern}' NOT found in '{directoryPath}'. Status: {status}",
                    exists ? "success" : "error",
                    addLogMessage,
                    flpConfigurationId);

                return exists;
            }
            catch (Exception ex)
            {
                await LogAsync($"Exception in FileExistsInRemoteLocation: {ex.Message}", "error", addLogMessage, flpConfigurationId);
                return false;
            }
            finally
            {
                smbClient.Logoff();
                smbClient.Disconnect();
                await LogAsync("Logoff and disconnect.", "success", addLogMessage, flpConfigurationId);
            }
        }   
       
        private async Task LogAsync(string message, string status, bool addLog, string flpId)
        {
            if (addLog)
                await _smbLibraryRepository.AddSmbRequestLogMessage(flpId, message, status);
        }

        public SMBResponse SMBRequest(CheckConnectivitySMBLibraryModel model, string flpConfigurationId, SMBRequestEnum sMBRequestEnum)
        {
            SMBResponse sMBResponse = new SMBResponse();
            SMB2Client smbClient = new SMB2Client();
            var addLogMessage = Convert.ToBoolean(Environment.GetEnvironmentVariable("TPDataIngestionAddSmbRequestMessage"));
            try
            {
                bool isConnected = smbClient.Connect(model.serverIP, SMBTransportType.DirectTCPTransport);

                if (isConnected)
                {
                    //AddSmbRequestMessage
                    if (addLogMessage)
                    {
                        var data = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, "Connected to SMB server.", "success").Result;
                    }


                    NTStatus status = smbClient.Login(model.domain, model.username, model.password);
                    if (status == NTStatus.STATUS_SUCCESS)
                    {
                        if (addLogMessage)
                        {
                            var data1 = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, "Login successful.", "success").Result;
                        }

                        //ISMBFileStore fileStore;
                        //status = smbClient.TreeConnect(model.sharedFolderName, out fileStore);
                        ISMBFileStore fileStore = smbClient.TreeConnect(model.sharedFolderName, out status);
                        if (status == NTStatus.STATUS_SUCCESS)
                        {
                            if (addLogMessage)
                            {
                                var data2 = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, "tree is connected successfully.", "success").Result;
                            }
                            if (sMBRequestEnum == SMBRequestEnum.FileListFromLocation)
                            {
                                sMBResponse.SharedFileLocations = FileListFromLocation(fileStore, model.sharedFolderPath, flpConfigurationId, model);

                            }
                            else if (sMBRequestEnum == SMBRequestEnum.DeleteFile)
                            {
                                sMBResponse.FileIsDeleted = DeleteFile(fileStore, model.sourceFilePath, flpConfigurationId);
                            }
                            else if (sMBRequestEnum == SMBRequestEnum.Stream)
                            {
                                sMBResponse.GetFileStream = GetFileStream(fileStore, model.sourceFilePath, flpConfigurationId);
                            }
                            else if (sMBRequestEnum == SMBRequestEnum.CopiedFileToArchivedFolder)
                            {
                                sMBResponse.CopiedFileToArchivedFolder = CopyFileToAnotherLocation(fileStore, model.sourceFilePath, model.destinationFilePath, flpConfigurationId);
                            }
                            else if (sMBRequestEnum == SMBRequestEnum.CopyFileFromBlob)
                            {
                                sMBResponse.CopiedFileFromBlob = CopyFileBlobToOnPremAsync(model.sourceFilePath, model.blobClient, fileStore, flpConfigurationId).Result;
                            }

                        }
                        else
                        {

                            var data3 = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, "Failed to connect to the shared folder.", "error").Result;
                        }
                    }
                    else
                    {

                        var data4 = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, "login failed", "error").Result;
                    }


                }
                else
                {
                    var data = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, "smbClient client not connected", "error").Result;
                }
            }
            catch (Exception ex)
            {
                var data = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, $"error:{ex.Message}", "error").Result;
                _logger.LogError(ex, ex.Message);
            }
            finally
            {
                if (addLogMessage)
                {
                    var data = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, $"Loggoff and Disconnect", "Success").Result;
                }
                smbClient.Logoff();
                smbClient.Disconnect();
            }
            return sMBResponse;
        }


        public (SMB2Client, ISMBFileStore) SMBRequest(CheckConnectivitySMBLibraryModel model, string flpConfigurationId)
        {
            SMB2Client smbClient = new SMB2Client();
            var addLogMessage = Convert.ToBoolean(Environment.GetEnvironmentVariable("TPDataIngestionAddSmbRequestMessage"));
            try
            {
                bool isConnected = smbClient.Connect(model.serverIP, SMBTransportType.DirectTCPTransport);
                if (isConnected)
                {

                    if (addLogMessage)
                    {
                        var data = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, "Connected to SMB server.", "success").Result;
                    }
                    NTStatus status = smbClient.Login(model.domain, model.username, model.password);
                    if (status == NTStatus.STATUS_SUCCESS)
                    {
                        if (addLogMessage)
                        {
                            var data1 = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, "Login successful.", "success").Result;
                        }
                        ISMBFileStore fileStore = smbClient.TreeConnect(model.sharedFolderName, out status);
                        if (status == NTStatus.STATUS_SUCCESS)
                        {
                            if (addLogMessage)
                            {
                                var data2 = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, "tree is connected successfully.", "success").Result;
                            }


                        }
                        else
                        {

                            var data3 = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, "Failed to connect to the shared folder.", "error").Result;

                        }
                        return (smbClient, fileStore);
                    }
                    else
                    {

                        var data4 = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, "login failed", "error").Result;
                    }


                }
                else
                {
                    var data = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, "smbClient client not connected", "error").Result;
                }
            }
            catch (Exception ex)
            {
                var data = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, $"error:{ex.Message}", "error").Result;
                _logger.LogError(ex, ex.Message);
            }

            return (smbClient, null);
        }
        private Stream CreateFileOnServer(ISMBFileStore fileStore, string destinationPath)
        {
            object fileHandle;
            NTStatus status = fileStore.CreateFile(out fileHandle, out _, destinationPath, AccessMask.GENERIC_WRITE, FileAttributes.Normal, ShareAccess.None, CreateDisposition.FILE_CREATE, CreateOptions.FILE_NON_DIRECTORY_FILE, null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                Console.WriteLine($"Failed to create file {destinationPath}. Error: {status}");
                return null;
            }

            return new SmbFileWriteStream(fileStore, fileHandle);
        }
        private List<SharedFileLocation> FileListFromLocation(ISMBFileStore fileStore, string directoryPath, string flpConfigurationId, CheckConnectivitySMBLibraryModel model)
        {
            List<SharedFileLocation> sharedFileLocations = new List<SharedFileLocation>();
            // Open the directory handle
            object directoryHandle;
            NTStatus status = fileStore.CreateFile(
                out directoryHandle,
                out _,
                directoryPath,
                AccessMask.GENERIC_READ,
                FileAttributes.Directory,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                var data1 = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, $"Failed to open directory:{directoryPath}", "error").Result;
                return new List<SharedFileLocation>();
            }

            // List to store all the files
            List<QueryDirectoryFileInformation> fileList = new List<QueryDirectoryFileInformation>();
            List<QueryDirectoryFileInformation> currentFiles = new List<QueryDirectoryFileInformation>();

            do
            {
                // Query directory with '*' search pattern to get all files
                status = fileStore.QueryDirectory(
                    out currentFiles,
                    directoryHandle,
                    $"*{model.fileName}*",
                    FileInformationClass.FileDirectoryInformation);

                if (status == NTStatus.STATUS_NO_MORE_FILES)
                {
                    // If no more files but currentFiles still has values, process the last batch
                    if (currentFiles != null && currentFiles.Count > 0)
                    {
                        fileList.AddRange(currentFiles);
                    }
                    break;  // Exit the loop as no more files are found
                }
                else if (status == NTStatus.STATUS_SUCCESS && currentFiles != null)
                {
                    // If status is successful, add the current batch of files to the list
                    fileList.AddRange(currentFiles);
                }
                else
                {

                    var data1 = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, $"Error querying directory", "error").Result;
                    break;  // Exit on error
                }
            }
            while (true);  // Loop until no more files

            // If no files were found, print a message
            if (fileList.Count == 0)
            {
                var data1 = _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, $"No files found in the directory.", "error").Result;
                return new List<SharedFileLocation>();
            }

            // Display the file list with full paths

            //TODO: Define the filter - > Checked below code and remove if/else conditions in future
            string filterFileName = model.fileName?.ToLower() ?? "";  //Filter by a substring in the file name (case-insensitive)
            FileAttributes requiredAttributes = FileAttributes.Archive;  //Filter by specific attributes

            // Display the file list with full paths after filtering
            foreach (var fileInfo in fileList)
            {
                SharedFileLocation sharedFileLocation = new SharedFileLocation();
                if (fileInfo is FileDirectoryInformation fileDirectoryInfo)
                {
                    // Apply the file name filter (you can customize the condition)
                    if (!string.IsNullOrWhiteSpace(fileDirectoryInfo.FileName) && fileDirectoryInfo.FileName.ToLower().Contains(filterFileName))
                    {
                        // Apply the file attribute filter (you can add more complex attribute checks if needed)
                        if ((fileDirectoryInfo.FileAttributes & requiredAttributes) == requiredAttributes)
                        {
                            // Combine the directory path with the file name to get the full file path
                            string fullFilePath = System.IO.Path.Combine(model.sharedFolderName, directoryPath + fileDirectoryInfo.FileName);
                            sharedFileLocation.FilePath = System.IO.Path.Combine(model.serverIP, fullFilePath);
                            sharedFileLocation.FileName = fileDirectoryInfo.FileName;
                            sharedFileLocation.FileSize = fileDirectoryInfo.EndOfFile;
                            sharedFileLocations.Add(sharedFileLocation);

                        }
                    }
                    else
                    {
                        // Apply the file attribute filter (you can add more complex attribute checks if needed)
                        if ((fileDirectoryInfo.FileAttributes & requiredAttributes) == requiredAttributes)
                        {
                            // Combine the directory path with the file name to get the full file path
                            string fullFilePath = System.IO.Path.Combine(model.sharedFolderName, directoryPath + fileDirectoryInfo.FileName);
                            sharedFileLocation.FilePath = System.IO.Path.Combine(model.serverIP, fullFilePath);
                            sharedFileLocation.FileName = fileDirectoryInfo.FileName;
                            sharedFileLocation.FileSize = fileDirectoryInfo.EndOfFile;
                            sharedFileLocations.Add(sharedFileLocation);
                        }
                    }
                }


            }
            // Close the directory handle after querying
            fileStore.CloseFile(directoryHandle);
            return sharedFileLocations;
        }



        public bool CopyFileFromOneToAnotherLocation(CheckConnectivitySMBLibraryModel sourceModel, CheckConnectivitySMBLibraryModel destinationModel, string sourceFilePath, string destinationFilePath, string flpConfigurationId)
        {
            bool success = false;
            SMB2Client sourceSmbClient = new SMB2Client();
            SMB2Client destinationSmbClient = new SMB2Client();
            bool addLogMessage = Convert.ToBoolean(Environment.GetEnvironmentVariable("TPDataIngestionAddSmbRequestMessage"));

            try
            {
                // Step 1: Connect to the source SMB server
                bool sourceConnected = sourceSmbClient.Connect(sourceModel.serverIP, SMBTransportType.DirectTCPTransport);
                if (!sourceConnected)
                {
                    LogMessage(flpConfigurationId, "Failed to connect to source SMB server", "error");
                    success = false;
                    return success;
                }

                NTStatus sourceLoginStatus = sourceSmbClient.Login(sourceModel.domain, sourceModel.username, sourceModel.password);
                if (sourceLoginStatus != NTStatus.STATUS_SUCCESS)
                {
                    LogMessage(flpConfigurationId, "Source login failed", "error");
                    success = false;
                    return success;
                }

                ISMBFileStore sourceFileStore = sourceSmbClient.TreeConnect(sourceModel.sharedFolderName, out NTStatus sourceTreeStatus);
                if (sourceTreeStatus != NTStatus.STATUS_SUCCESS)
                {
                    LogMessage(flpConfigurationId, "Failed to connect to source shared folder", "error");
                    success = false;
                    return success;
                }

                // Step 2: Read the file content from the source server
                byte[] fileContent = ReadFileFromServer(sourceFileStore, sourceFilePath);
                if (fileContent == null || fileContent.Length == 0)
                {
                    LogMessage(flpConfigurationId, "Failed to read file from source server", "error");
                    success = false;
                    return success;
                }

                // Step 3: Connect to the destination SMB server
                bool destinationConnected = destinationSmbClient.Connect(destinationModel.serverIP, SMBTransportType.DirectTCPTransport);
                if (!destinationConnected)
                {
                    LogMessage(flpConfigurationId, "Failed to connect to destination SMB server", "error");
                    success = false;
                    return success;
                }

                NTStatus destinationLoginStatus = destinationSmbClient.Login(destinationModel.domain, destinationModel.username, destinationModel.password);
                if (destinationLoginStatus != NTStatus.STATUS_SUCCESS)
                {
                    LogMessage(flpConfigurationId, "Destination login failed", "error");
                    success = false;
                    return success;
                }

                ISMBFileStore destinationFileStore = destinationSmbClient.TreeConnect(destinationModel.sharedFolderName, out NTStatus destinationTreeStatus);
                if (destinationTreeStatus != NTStatus.STATUS_SUCCESS)
                {
                    LogMessage(flpConfigurationId, "Failed to connect to destination shared folder", "error");
                    success = false;
                    return success;
                }


                // Extract the directory from the destination file path
                string destinationDirectory = Path.GetDirectoryName(destinationFilePath);

                // 1. Check if the destination directory exists
                if (!DirectoryExists(destinationFileStore, destinationDirectory))
                {
                    LogMessage(flpConfigurationId, "Directory doen't exit, creating directory", "error");
                    // If the directory doesn't exist, create it
                    CreateDirectory(destinationFileStore, destinationDirectory);
                }

                // Step 4: Write the file content to the destination server
                bool fileWritten = WriteFileToServerV2(destinationFileStore, destinationFilePath, fileContent);
                if (!fileWritten)
                {
                    LogMessage(flpConfigurationId, "Failed to write file to destination server", "error");
                    success = false;
                    return success;
                }

                if (addLogMessage)
                    LogMessage(flpConfigurationId, $"File copied successfully from {sourceFilePath} to {destinationFilePath}", "success");

                success = true;
            }
            catch (Exception ex)
            {
                LogMessage(flpConfigurationId, $"Error: {ex.Message}", "error");
                success = false;
            }
            finally
            {
                LogMessage(flpConfigurationId, "Logging off and disconnecting", "success");
                sourceSmbClient.Logoff();
                sourceSmbClient.Disconnect();
                destinationSmbClient.Logoff();
                destinationSmbClient.Disconnect();
            }

            return success;
        }




        private static void CreateFileInDirectory(ISMBFileStore fileStore, string directoryPath, string fileName, byte[] fileContent)
        {
            // Combine directory path and file name to create the full file path
            string filePath = System.IO.Path.Combine(directoryPath, fileName);

            // Create a handle for the file
            object fileHandle;

            // Use CreateFile to create the file on the SMB share
            NTStatus status = fileStore.CreateFile(
                out fileHandle,        // Handle for the created file
                out _,                 // Standard information (you can ignore it for now)
                filePath,              // Full path to the file (including file name)
                AccessMask.GENERIC_WRITE, // Access rights for writing to the file
                0,                     // File attributes (e.g., normal file)
                ShareAccess.None,       // No file sharing
                CreateDisposition.FILE_CREATE, // Create the file; if it already exists, this will fail
                CreateOptions.FILE_NON_DIRECTORY_FILE, // Specify that the file is a normal file (not a directory)
                null);                 // Optional security context

            if (status != NTStatus.STATUS_SUCCESS)
            {
                Console.WriteLine($"Failed to create the file: {filePath}. Error: {status}");
                return;
            }

            Console.WriteLine($"File created successfully: {filePath}");

            // If you want to write content to the file
            if (fileContent != null && fileContent.Length > 0)
            {
                // Declare a variable to hold the number of bytes written
                int bytesWritten;

                // Write content to the file starting at offset 0
                status = fileStore.WriteFile(out bytesWritten, fileHandle, 0, fileContent);

                if (status != NTStatus.STATUS_SUCCESS)
                {
                    Console.WriteLine($"Failed to write to the file: {filePath}. Error: {status}");
                }
                else
                {
                    Console.WriteLine($"Successfully wrote {bytesWritten} bytes to the file: {filePath}");
                }
            }

            // Close the file handle after writing the file
            fileStore.CloseFile(fileHandle);
        }

        private bool CopyFileToAnotherLocation(ISMBFileStore fileStore, string sourceFilePath, string destinationFilePath, string flpConfigurationId)
        {
            // Extract the directory from the destination file path
            string destinationDirectory = Path.GetDirectoryName(destinationFilePath);

            // 1. Check if the destination directory exists
            if (!DirectoryExists(fileStore, destinationDirectory))
            {
                // If the directory doesn't exist, create it
                CreateDirectory(fileStore, destinationDirectory);
            }

            // 2. Open the source file for reading
            object sourceFileHandle;
            NTStatus status = fileStore.CreateFile(
                out sourceFileHandle,
                out _,
                sourceFilePath,
                AccessMask.GENERIC_READ,
                FileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                LogMessage(flpConfigurationId, $"Failed to open source file: {sourceFilePath}. Error: {status}", "error");
                return false;
            }

            // 3. Read the file content from the source
            byte[] fileContent = ReadFileContent(fileStore, sourceFileHandle);

            if (fileContent == null || fileContent.Length == 0)
            {
                LogMessage(flpConfigurationId, $"Failed to read content from the source file: {sourceFilePath}", "error");
                fileStore.CloseFile(sourceFileHandle);
                return false;
            }

            // Close the source file handle after reading
            fileStore.CloseFile(sourceFileHandle);

            // 4. Create the destination file and write the content
            object destinationFileHandle;
            status = fileStore.CreateFile(
                out destinationFileHandle,
                out _,
                destinationFilePath,
                AccessMask.GENERIC_WRITE,
                FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_CREATE, // This creates the file if it doesn't exist
                CreateOptions.FILE_NON_DIRECTORY_FILE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                LogMessage(flpConfigurationId, $"Failed to create the destination file: {destinationFilePath}. Error: {status}", "error");
                return false;
            }

            // Write content to the destination file
            int bytesWritten = 0;
            status = fileStore.WriteFile(out bytesWritten, destinationFileHandle, 0, fileContent);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                LogMessage(flpConfigurationId, $"Failed to write to the destination file: {destinationFilePath}. Error: {status}", "error");
            }
            // Close the destination file handle after writing
            fileStore.CloseFile(destinationFileHandle);
            return true;
        }

        // Helper method to read file content
        private static byte[] ReadFileContent(ISMBFileStore fileStore, object fileHandle)
        {
            List<byte> fileContent = new List<byte>();
            byte[] buffer;
            long offset = 0;
            int bufferSize = 4096;  // Adjust buffer size if needed
            NTStatus status;

            do
            {
                // Call the ReadFile method using the provided signature
                status = fileStore.ReadFile(out buffer, fileHandle, offset, bufferSize);

                if (status == NTStatus.STATUS_SUCCESS && buffer.Length > 0)
                {
                    fileContent.AddRange(buffer);
                    offset += buffer.Length;
                }
            }
            while (status == NTStatus.STATUS_SUCCESS && buffer.Length > 0);

            if (status != NTStatus.STATUS_SUCCESS && status != NTStatus.STATUS_END_OF_FILE)
            {
                Console.WriteLine($"Failed to read the file. Error: {status}");
                return null;
            }

            return fileContent.ToArray();
        }

        // Helper method to check if a directory exists
        private static bool DirectoryExists(ISMBFileStore fileStore, string directoryPath)
        {
            object directoryHandle;
            NTStatus status = fileStore.CreateFile(
                out directoryHandle,
                out _,
                directoryPath,
                AccessMask.GENERIC_READ,
                FileAttributes.Directory,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE,
                null);

            if (status == NTStatus.STATUS_SUCCESS)
            {
                fileStore.CloseFile(directoryHandle);
                return true;
            }
            return false;
        }

        // Helper method to create a directory
        //private static bool CreateDirectory(ISMBFileStore fileStore, string directoryPath)
        //{
        //    object directoryHandle;
        //    NTStatus status = fileStore.CreateFile(
        //        out directoryHandle,
        //        out _,
        //        directoryPath,
        //        AccessMask.GENERIC_WRITE,
        //        FileAttributes.Directory,
        //        ShareAccess.None,
        //        CreateDisposition.FILE_CREATE,
        //        CreateOptions.FILE_DIRECTORY_FILE,
        //        null);

        //    if (status == NTStatus.STATUS_SUCCESS)
        //    {
        //        fileStore.CloseFile(directoryHandle);
        //        Console.WriteLine($"Directory created: {directoryPath}");
        //        return true;
        //    }
        //    else
        //    {
        //        Console.WriteLine($"Failed to create directory: {directoryPath}. Error: {status}");
        //        return false;
        //    }
        //}

        private static bool CreateDirectory(ISMBFileStore fileStore, string directoryPath)
        {
            try
            {
                // Split the directory path into individual directories
                var directories = directoryPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                string currentPath = string.Empty;

                foreach (var dir in directories)
                {
                    // Rebuild the path step by step, ensuring each directory exists
                    currentPath = Path.Combine(currentPath, dir);

                    // Check if the directory already exists
                    if (!DirectoryExists(fileStore, currentPath))
                    {
                        // Try to create the directory
                        object directoryHandle;
                        NTStatus status = fileStore.CreateFile(
                            out directoryHandle,
                            out _,
                            currentPath,
                            AccessMask.GENERIC_WRITE, // or AccessMask.FILE_WRITE_DATA
                            FileAttributes.Directory,
                            ShareAccess.None,
                            CreateDisposition.FILE_CREATE,
                            CreateOptions.FILE_DIRECTORY_FILE,
                            null);

                        if (status != NTStatus.STATUS_SUCCESS)
                        {
                            Console.WriteLine($"Failed to create directory: {currentPath}. Error: {status}");
                            return false;
                        }

                        // Close the handle to the created directory
                        fileStore.CloseFile(directoryHandle);
                        Console.WriteLine($"Directory created: {currentPath}");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating directory {directoryPath}: {ex.Message}");
                return false;
            }
        }



        private bool DeleteFile(ISMBFileStore fileStore, string filePath, string flpConfigurationId)
        {
            // Step 1: Open the file handle to the file you want to delete
            object fileHandle;
            NTStatus status = fileStore.CreateFile(
                out fileHandle,
                out _,
                filePath,
                AccessMask.DELETE, // Use DELETE access
                FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_OPEN, // Open the file if it exists
                CreateOptions.FILE_NON_DIRECTORY_FILE, // Ensure it is a file, not a directory
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {

                LogMessage(flpConfigurationId, $"Failed to open file for deletion: {filePath}. Error: {status}", "error");
                return false;
            }

            // Step 2: Mark the file for deletion
            FileDispositionInformation fileDispositionInfo = new FileDispositionInformation
            {
                DeletePending = true
            };

            status = fileStore.SetFileInformation(fileHandle, fileDispositionInfo);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                LogMessage(flpConfigurationId, $"Failed to mark file for deletion: {filePath}. Error: {status}", "error");
                fileStore.CloseFile(fileHandle);
                return false;
            }

            // Step 3: Close the file handle, which will delete the file
            fileStore.CloseFile(fileHandle);
            // LogMessage(flpConfigurationId, $"Successfully deleted the file: {filePath}", "error");           
            return true;
        }


        public Stream GetFileStream(ISMBFileStore fileStore, string filePath, string flpConfigurationId)
        {
            // Step 1: Open the file for reading
            object fileHandle;
            NTStatus status = fileStore.CreateFile(
                out fileHandle,
                out _,
                filePath,
                AccessMask.GENERIC_READ,  // Read-only access
                FileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,  // Open if the file exists
                CreateOptions.FILE_NON_DIRECTORY_FILE,  // Ensure it's a file, not a directory
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                throw new IOException($"Failed to open file: {filePath}. Error: {status}");
            }

            // Step 2: Increase buffer size for reading in larger chunks
            const int bufferSize = 1 * 1024 * 1024; // 1mb buffer size (you can adjust this size as needed)
            byte[] buffer = new byte[bufferSize];
            MemoryStream memoryStream = new MemoryStream(); // Stream to store file data
            long offset = 0;
            int bytesRead;

            // Step 3: Efficiently read file chunk by chunk
            do
            {
                // Read file data from the file store
                status = fileStore.ReadFile(out buffer, fileHandle, offset, bufferSize);

                if (status == NTStatus.STATUS_END_OF_FILE)
                {
                    break; // End of file reached
                }

                if (status != NTStatus.STATUS_SUCCESS)
                {
                    fileStore.CloseFile(fileHandle); // Ensure to close the file handle on error
                    throw new IOException($"Failed to read file: {filePath}. Error: {status}");
                }

                bytesRead = buffer.Length;
                memoryStream.Write(buffer, 0, bytesRead);  // Write the data to the memory stream
                offset += bytesRead;  // Update the offset for the next read
            }
            while (bytesRead > 0);

            // Step 4: Close the file handle after reading
            fileStore.CloseFile(fileHandle);

            // Reset the stream position to the beginning for reading
            memoryStream.Seek(0, SeekOrigin.Begin);

            // Return the memory stream containing the file data
            return memoryStream;
        }


        /////////////
        ///




        // Helper method to read the file content from the server
        public byte[] ReadFileFromServer(ISMBFileStore fileStore, string filePath)
        {
            object fileHandle;
            NTStatus status = fileStore.CreateFile(
                out fileHandle,
                out _,
                filePath,
                AccessMask.GENERIC_READ,
                FileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                Console.WriteLine($"Failed to open file: {filePath}. Error: {status}");
                return null;
            }

            // Read the file content
            byte[] fileContent = ReadFileContent(fileStore, fileHandle);
            fileStore.CloseFile(fileHandle);
            return fileContent;
        }

        // Helper method to write the file content to the server
        private bool WriteFileToServer(ISMBFileStore fileStore, string filePath, byte[] fileContent)
        {
            object fileHandle;
            NTStatus status = fileStore.CreateFile(
                out fileHandle,
                out _,
                filePath,
                AccessMask.GENERIC_WRITE,
                FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_CREATE, // This creates the file if it doesn't exist
                CreateOptions.FILE_NON_DIRECTORY_FILE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                Console.WriteLine($"Failed to create file: {filePath}. Error: {status}");
                return false;
            }

            // Write content to the file
            int bytesWritten;
            status = fileStore.WriteFile(out bytesWritten, fileHandle, 0, fileContent);

            fileStore.CloseFile(fileHandle);

            return status == NTStatus.STATUS_SUCCESS;
        }



        private bool WriteFileToServerV2(ISMBFileStore fileStore, string filePath, byte[] fileContent)
        {
            object fileHandle;
            NTStatus status = fileStore.CreateFile(
                out fileHandle,
                out _,
                filePath,
                AccessMask.GENERIC_WRITE,
                FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_CREATE, // This creates the file if it doesn't exist
                CreateOptions.FILE_NON_DIRECTORY_FILE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                Console.WriteLine($"Failed to create file: {filePath}. Error: {status}");
                return false;
            }

            try
            {
                int bufferSize = 4096;  // Adjust buffer size if needed
                long offset = 0;
                int remainingBytes = fileContent.Length;
                int bytesWritten;

                while (remainingBytes > 0)
                {
                    // Calculate the number of bytes to write in the current iteration
                    int bytesToWrite = Math.Min(bufferSize, remainingBytes);

                    // Create a buffer for the current chunk
                    byte[] buffer = new byte[bytesToWrite];
                    Array.Copy(fileContent, offset, buffer, 0, bytesToWrite);

                    // Write content to the file in chunks
                    status = fileStore.WriteFile(out bytesWritten, fileHandle, offset, buffer);

                    if (status != NTStatus.STATUS_SUCCESS)
                    {
                        Console.WriteLine($"Failed to write file: {filePath} at offset {offset}. Error: {status}");
                        return false;
                    }

                    offset += bytesToWrite;
                    remainingBytes -= bytesToWrite;
                }
            }
            finally
            {
                // Close the file handle
                fileStore.CloseFile(fileHandle);
            }

            return true;
        }


        private void LogMessage(string flpConfigurationId, string message, string status)
        {
            _smbLibraryRepository.AddSmbRequestLogMessage(flpConfigurationId, message, status).Wait();
        }



        private async Task<bool> CopyFileBlobToOnPremAsync(string filePath, BlobClient blobClient, ISMBFileStore fileStore, string flpConfigurationId)
        {
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();
            bool ret = false;

            // Download the blob content
            BlobDownloadInfo download = await blobClient.DownloadAsync();

            // Try to write to the remote location via SMB
            try
            {
                // Ensure the directory for the file exists on the remote SMB server
                string directoryPath = Path.GetDirectoryName(filePath);
                if (!DirectoryExists(fileStore, directoryPath))
                {
                    CreateDirectory(fileStore, directoryPath); // Create directory on the remote server if it doesn't exist
                }

                // Create or open the file on the remote SMB server
                object remoteFileHandle;
                NTStatus status = fileStore.CreateFile(
                    out remoteFileHandle,
                    out _,
                    filePath,
                    AccessMask.GENERIC_WRITE,
                    FileAttributes.Normal,
                    ShareAccess.None,
                    CreateDisposition.FILE_CREATE,
                    CreateOptions.FILE_NON_DIRECTORY_FILE,
                    null);

                if (status != NTStatus.STATUS_SUCCESS)
                {
                    LogMessage(flpConfigurationId, $"Failed to create remote file: {filePath}. Error: {status}", "error");
                    //throw new IOException($"Failed to create remote file: {filePath}. Error: {status}");

                }

                // Use the remote file handle to write the blob content to the remote SMB server
                using (var remoteFileStream = new SmbFileWriteStream(fileStore, remoteFileHandle))
                {
                    await download.Content.CopyToAsync(remoteFileStream); // Copy the blob content to the remote file
                    ret = true;
                    flpProcessTempFile.sourceTempFilePath = filePath;
                }

                // Close the remote file handle after writing
                fileStore.CloseFile(remoteFileHandle);
            }
            catch (Exception ex)
            {
                ret = false;
                LogMessage(flpConfigurationId, $"Failed to copy file from Blob to SMB location: {ex.Message}", "error");
                //throw new Exception($"Failed to copy file from Blob to SMB location: {ex.Message}", ex);
            }

            return ret;
        }



        /*public ByteArrayContent SMBRequest(CheckConnectivitySMBLibraryModel request, int userId, int caseId)
      {
          int res;
          MemoryStream stream = new MemoryStream();
          ByteArrayContent byteArrayContent = new ByteArrayContent(new byte[0]);
         // UtilitieRepository utilitieRepository = new UtilitieRepository(_config);
          NTStatus status = NTStatus.STATUS_WRONG_PASSWORD;
          SMB2Client client = new SMB2Client();
         // res = utilitieRepository.AddInfoLog("1. SMB2 Client Created.", "Authorization Controller - SMBHelper - SMBRequest", userId, caseId).Result;
          try
          {
              // Connect to the SMB server.
              bool isConnected = client.Connect(request.serverIP, SMBTransportType.DirectTCPTransport);
              if (isConnected)
              {
                 // res = utilitieRepository.AddInfoLog("2. SMB2 Client Connected.", "Authorization Controller - SMBHelper - SMBRequest", userId, caseId).Result;
                  // Authenticate with service account credentials.
                  status = client.Login(request.domain, request.username, request.password);
                  if (status == NTStatus.STATUS_SUCCESS)
                  {
                      List<string> shares = client.ListShares(out status);
                      int i = 0;
                      foreach (string item in shares)
                      {
                          i++;
                         // res = utilitieRepository.AddInfoLog("2." + i + " Share: " + item, "Authorization Controller - SMBHelper - SMBRequest", userId, caseId).Result;
                      }

                      // res = utilitieRepository.AddInfoLog("3. SMB2 Client Login Successful.", "Authorization Controller - SMBHelper - SMBRequest", userId, caseId).Result;

                      ISMBFileStore fileStore = client.TreeConnect(request.sharedFolderName, out status);
                      //res = utilitieRepository.AddInfoLog("4. TreeConnect Success: " + status, "Authorization Controller - SMBHelper - SMBRequest", userId, caseId).Result;
                      object fileHandle;
                      FileStatus fileStatus;

                      //res = utilitieRepository.AddInfoLog($"4.1 SMB2 Client TreeConnect.\nFileStore: {fileStore} \nStatus: {status}", "Authorization Controller - SMBHelper - SMBRequest", userId, caseId).Result;

                      //status = fileStore.CreateFile(out fileHandle, out fileStatus, request.sharedFolderPath + "\\" + request.fileName, AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE, SMBLibrary.FileAttributes.Normal, ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
                      status = fileStore.CreateFile(out fileHandle, out fileStatus, request.sharedFolderPath + "\\", AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE, SMBLibrary.FileAttributes.Normal, ShareAccess.Read, CreateDisposition.FILE_OPEN, CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);
                      if (status == NTStatus.STATUS_SUCCESS)
                      {
                          //res = utilitieRepository.AddInfoLog("5. SMB2 Client File Found.", "Authorization Controller - SMBHelper - SMBRequest", userId, caseId).Result;

                          byte[] data;
                          long bytesRead = 0;
                          while (true)
                          {
                              status = fileStore.ReadFile(out data, fileHandle, bytesRead, (int)client.MaxReadSize);
                              if (status != NTStatus.STATUS_SUCCESS && status != NTStatus.STATUS_END_OF_FILE)
                              {
                                 // res = utilitieRepository.AddInfoLog("6. Failed to read from file.", "Authorization Controller - SMBHelper - SMBRequest", userId, caseId).Result;
                                  throw new Exception("Failed to read from file");
                              }

                              if (status == NTStatus.STATUS_END_OF_FILE || data.Length == 0)
                              {
                                  break;
                              }
                              bytesRead += data.Length;
                              stream.Write(data, 0, data.Length);
                          }
                          byteArrayContent = new ByteArrayContent(stream.ToArray());
                      }
                      status = fileStore.CloseFile(fileHandle);
                      status = fileStore.Disconnect();
                      //res = utilitieRepository.AddInfoLog("7. File Read Sucessful.", "Authorization Controller - SMBHelper - SMBRequest", userId, caseId).Result;
                  }
                  else
                  {
                      //res = utilitieRepository.AddInfoLog($"8. Client Disconnected. Status: {status}", "Authorization Controller - SMBHelper - SMBRequest", userId, caseId).Result;
                      client.Disconnect();
                  }
              }
              else
              {
                 // res = utilitieRepository.AddInfoLog("9. Not Connected Exiting...", "Authorization Controller - SMBHelper - SMBRequest", userId, caseId).Result;
              }
          }
          catch (Exception ex)
          {
             /* ErrorLogData errorLogData = new ErrorLogData();
              errorLogData.Logger = "Authorization Controller - SMBHelper - SMBRequest";
              errorLogData.ExceptionMessage = ex.Message;
              errorLogData.ExceptionDetails = ex.StackTrace;
              errorLogData.Source = "CheckConnectivitySMBLibrary";
              errorLogData.recordingIdent = "0";
              errorLogData.CaseId = "0";
              var adderror = utilitieRepository.AddErrorLog(errorLogData);
        }
         finally
         {
             if (status == NTStatus.STATUS_SUCCESS)
                 client.Logoff();

             client.Disconnect();
         }
         return byteArrayContent;
     }*/



    }
}
