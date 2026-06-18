using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NPOI.HPSF;
using Org.BouncyCastle.Tls;
using Renci.SshNet.Security;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v4._0;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v4._1;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Enums.v3._0;
using Teleperformance.DataIngestion.Models.Enums.v4._1;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using Teleperformance.DataIngestion.Models.Models.v4._1;

namespace Teleperformance.DataIngestion.DataAccess.Services.v4._1
{
    public class LandingLayerService : ILandingLayerService
    {
        private readonly ILogger<LandingLayerService> _logger;
        private readonly ILandingLayerRepository _landingLayerRepository;
        private readonly IFlpProcessingServiceV4_1 _iIFileProcessingService;
        private readonly IFileLoadingProcessConfigurationServiceV4_1 _fileLoadingProcessConfigurationService;
        private readonly IBlobStorageServiceV4_1 _iBlobStorageService;
        private ServiceHelper serviceHelper = new ServiceHelper();
        private readonly ISMBLibraryServices _ismbLibraryServices;
        private readonly IProcessConfigurationServiceV4_1 _processConfigurationServiceV4_1;
        public LandingLayerService(ILogger<LandingLayerService> logger, ILandingLayerRepository landingLayerRepository, IFlpProcessingServiceV4_1 iIFileProcessingService,
            IFileLoadingProcessConfigurationServiceV4_1 fileLoadingProcessConfigurationService,
            IBlobStorageServiceV4_1 iBlobStorageService, ISMBLibraryServices ismbLibraryServices, IProcessConfigurationServiceV4_1 processConfigurationServiceV4_1)
        {
            _logger = logger;
            _landingLayerRepository = landingLayerRepository;
            _iIFileProcessingService = iIFileProcessingService;
            _fileLoadingProcessConfigurationService = fileLoadingProcessConfigurationService;
            _iBlobStorageService = iBlobStorageService;
            _ismbLibraryServices = ismbLibraryServices;
            _processConfigurationServiceV4_1 = processConfigurationServiceV4_1;
        }
       
        public async Task<APIResponse<FlpProcessResponseDto>> ProcessFileFromRemoteToLandingLayer(FlpRequestDto4_1 flpRequestDto)
        {
            string flpConfigurationId = flpRequestDto?.FlpConfigurationId?.Trim() ?? string.Empty;
            string loginId = "function app";
            string processName = flpRequestDto?.ProcessName ?? string.Empty;
            long processId = long.Parse(DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            FlpActivityLogStatusEnum flpActivityLogStatusEnum = FlpActivityLogStatusEnum.FileProcessCreated;

            if (string.IsNullOrWhiteSpace(flpConfigurationId))
                return Error("flpConfigurationId is NULL");

            string uploadFileId = flpRequestDto?.LandingLayerUploadedId??"";
            if (string.IsNullOrWhiteSpace(uploadFileId))
                return Error("Uploaded File Id is NULL");

            try
            {
                await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromRemoteToLandingLayer", $"Process Started.", $"Process Started.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);

                //-------------------------------------------------------------------
                // 1) Fetch Landing Layer Details
                //-------------------------------------------------------------------
                var dbll = await _landingLayerRepository.GetLandingLayerDetailsAsync(flpConfigurationId);

                if (dbll == null || dbll.Result?.Equals("Failure", StringComparison.OrdinalIgnoreCase) == true)
                    return Error("Return failure from database");

                //-------------------------------------------------------------------
                // 2) Ensure process type = BlobStorageUpload (as per your current check)
                //    If your SMB is a different ProcessType, update this condition accordingly.
                //-------------------------------------------------------------------
                if (dbll.processTypeId != (int)ProcessTypeEnum.SharedLocationUpload)
                    return Error("ProcessTypeId is not BlobStorageUpload");

                //-------------------------------------------------------------------
                // 3) Find files in remote SMB location
                //-------------------------------------------------------------------
                var smbModel = new CheckConnectivitySMBLibraryModel
                {
                    serverIP = dbll.serverName,
                    sharedFolderName = dbll.folderName,
                    sharedFolderPath = $@"{dbll.sourcePath}",
                    username = dbll.userName,
                    password = dbll.password,
                    domain = dbll.domain,
                    fileName = dbll.search_string_in_file_name
                };

                var listResult = _ismbLibraryServices.SMBRequest(smbModel, flpConfigurationId, SMBRequestEnum.FileListFromLocation);
                if (listResult == null || listResult.SharedFileLocations == null || !listResult.SharedFileLocations.Any())
                    return Error("No files found in remote location.");

                // SharedFileLocation { FileName, FilePath }
                var files = listResult.SharedFileLocations;

                var fileExtension = files.Count() == 1 ? FlpConfigurationHelper.GetFileExtension(Path.GetFileName(files.First().FileName)) : null;

                await _iIFileProcessingService.AddFileProcessLosStatus(
                    tabName: null,
                    fileType: fileExtension,
                    loginId: loginId,
                    message: $"File Processing Initialization for total {files.Count} files.",
                    messageType: "info",
                    processId: processId,
                    processName: processName,
                    tableName: null,
                    totalRows: 0,
                    flpConfigurationId: flpConfigurationId,
                    fileUploadedId: uploadFileId,
                    FileStatusActivityEnum.Processing,
                    FlpActivityLogStatusEnum.FileProcessCreated, null
                );

                //-------------------------------------------------------------------
                // 4) Validation Configuration
                //-------------------------------------------------------------------
                var validationDetails = await _landingLayerRepository.GetValidationDetailsAsync(flpConfigurationId);
                if (validationDetails == null)
                {
                    await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromRemoteToLandingLayer", $"Process Started.", $"Not found validation details.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: fileExtension,
                        loginId: loginId,
                        message: "File Processing Initialization- Not found validation details.",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.Error,
                        FlpActivityLogStatusEnum.FileProcessCreated, null
                    );
                    return Error("Validation details not found");
                }

                await _iIFileProcessingService.AddFileProcessLosStatus(
                    tabName: null,
                    fileType: fileExtension,
                    loginId: loginId,
                    message: $"File Processing Initialization – Details Retrieved for total {files.Count} files.",
                    messageType: "info",
                    processId: processId,
                    processName: processName,
                    tableName: null,
                    totalRows: 0,
                    flpConfigurationId: flpConfigurationId,
                    fileUploadedId: uploadFileId,
                    FileStatusActivityEnum.ProcessCompleted,
                    FlpActivityLogStatusEnum.FileProcessCreated, null
                );

                //-------------------------------------------------------------------
                // 5) Extension validation (uses your FileValidationForExtension)
                //-------------------------------------------------------------------
                flpActivityLogStatusEnum = FlpActivityLogStatusEnum.ValidationFileForExtension;

                await _iIFileProcessingService.AddFileProcessLosStatus(
                    tabName: null,
                    fileType: null,
                    loginId: loginId,
                    message: $"File extension validation started for total {files.Count} files.",
                    messageType: "info",
                    processId: processId,
                    processName: processName,
                    tableName: null,
                    totalRows: 0,
                    flpConfigurationId: flpConfigurationId,
                    fileUploadedId: uploadFileId,
                    FileStatusActivityEnum.Processing,
                    FlpActivityLogStatusEnum.ValidationFileForExtension, null
                );

                var validExtFiles = new List<LandingLayerValidationFile>();
                var invalidExtFiles = new List<LandingLayerValidationFile>();

                await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromRemoteToLandingLayer", $"ValidationFileForExtension.", $"ValidationFileForExtension started.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);

                foreach (var sf in files)
                {
                    // NOTE: we don't open a real stream here; validators only need FileName
                    string originalName = !string.IsNullOrWhiteSpace(sf.FileName)
                        ? sf.FileName
                        : Path.GetFileName(sf.FilePath);

                    IFormFile tempFile = new SmbFormFile(originalName);

                    (string generatedName, bool isValid) =
                        await FileValidationForExtension(
                            tempFile,
                            flpRequestDto.ProcessName,
                            flpConfigurationId,
                            uploadFileId,
                            loginId,
                            validationDetails);

                    var llFile = new LandingLayerValidationFile
                    {
                        GeneratedFileName = generatedName,
                        File = tempFile,
                        RemoteFilePath = sf.FilePath //keep full path for later move
                    };

                    if (isValid)
                        validExtFiles.Add(llFile);
                    else
                    {
                        llFile.ErrorMessage = $"Invalid extension for {originalName}";
                        invalidExtFiles.Add(llFile);
                    }
                }

                string inValidFilesMessage = invalidExtFiles.Count > 0 ? $"Validation failed for {invalidExtFiles.Count}: " : "All files are valid.";
                await _iIFileProcessingService.AddFileProcessLosStatus(
                    tabName: null,
                    fileType: null,
                    loginId: loginId,
                    message: $"File extension validation completed for total {files.Count} files. Valid files {validExtFiles.Count} and Invalid files {invalidExtFiles.Count}",
                    messageType: "info",
                    processId: processId,
                    processName: processName,
                    tableName: null,
                    totalRows: 0,
                    flpConfigurationId: flpConfigurationId,
                    fileUploadedId: uploadFileId,
                    FileStatusActivityEnum.ProcessCompleted,
                    FlpActivityLogStatusEnum.ValidationFileForExtension, null
                );

                //-------------------------------------------------------------------
                // 6) Regex validation (uses your FileValidationForRegex)
                //-------------------------------------------------------------------
                flpActivityLogStatusEnum = FlpActivityLogStatusEnum.ValidationFileForRegex;
                bool hasRegex = validationDetails.RegexList?.Any() ?? false;

                List<LandingLayerValidationFile> validRegexFiles = new();
                List<LandingLayerValidationFile> invalidRegexFiles = new();

                if (hasRegex)
                {
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: null,
                        loginId: loginId,
                        message: "Regex validation started",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.Processing,
                        FlpActivityLogStatusEnum.ValidationFileForRegex, null
                    );

                    await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromRemoteToLandingLayer", $"ValidationFileForRegex", $"ValidationFileForRegex started.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);

                    foreach (var file in validExtFiles)
                    {
                        (string newName, bool isValid) =
                            await FileValidationForRegex(
                                file.File,
                                flpRequestDto.ProcessName,
                                flpConfigurationId,
                                uploadFileId,
                                loginId,
                                validationDetails);

                        file.GeneratedFileName = newName;

                        if (isValid)
                            validRegexFiles.Add(file);
                        else
                        {
                            file.ErrorMessage = $"Regex failed for {file.File.FileName}";
                            invalidRegexFiles.Add(file);
                        }
                    }

                    await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromRemoteToLandingLayer", $"ValidationFileForRegex", $"ValidationFileForRegex completed.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    string inValidRegexMessage = invalidRegexFiles.Count > 0 ? $"Validation failed for {invalidRegexFiles.Count}: " : "All files are valid.";
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: null,
                        loginId: loginId,
                        message: $"Regex validation completed.{inValidRegexMessage}",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.ProcessCompleted,
                        FlpActivityLogStatusEnum.ValidationFileForRegex, null
                    );
                }

                //-------------------------------------------------------------------
                // 7) Final valid / invalid file sets
                //-------------------------------------------------------------------
                var validFiles = new List<LandingLayerValidationFile>();
                var invalidFiles = new List<LandingLayerValidationFile>();

                if (hasRegex)
                {
                    if (validRegexFiles.Any()) validFiles.AddRange(validRegexFiles);
                    if (invalidExtFiles.Any()) invalidFiles.AddRange(invalidExtFiles);
                    if (invalidRegexFiles.Any()) invalidFiles.AddRange(invalidRegexFiles);
                }
                else
                {
                    if (validExtFiles.Any()) validFiles.AddRange(validExtFiles);
                    if (invalidExtFiles.Any()) invalidFiles.AddRange(invalidExtFiles);
                }

                //-------------------------------------------------------------------
                // 8) Target Databricks Storage Info
                //-------------------------------------------------------------------
                var databricksInfo =
                    await _fileLoadingProcessConfigurationService.DatabricksStorageAccountInfo(flpConfigurationId, null);

                //-------------------------------------------------------------------
                // 9) Move Valid Files → Landing Layer
                //-------------------------------------------------------------------
                string landingLayerPath = validationDetails.FileAdditionalDetails.landingLayerPath;

                if (validFiles.Any())
                {
                    flpActivityLogStatusEnum = FlpActivityLogStatusEnum.MovedFileToLandingLayer;
                    int movedFileCountToLandingLayer = 0;
                    await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromRemoteToLandingLayer", $"MovedFileToLandingLayer", $"Valid files are started moving to landing layer.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: null,
                        loginId: loginId,
                        message: $"Moving file to landing layer.{landingLayerPath}",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.Processing,
                        FlpActivityLogStatusEnum.MovedFileToLandingLayer, null
                    );

                    foreach (var vf in validFiles)
                    {


                        var sourceFilePath = FlpConfigurationHelperV4_1.GetLandingLayerFilePath(vf.RemoteFilePath, dbll.sourcePath);
                        if (string.IsNullOrWhiteSpace(sourceFilePath))
                        {
                            await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromRemoteToLandingLayer", $"MovedValidFileToLandingFolder", $"Source file path is null or empty for file {vf.File.FileName}.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                        }


                        var fileModel = new CheckConnectivitySMBLibraryModel
                        {
                            serverIP = dbll.serverName,
                            sharedFolderName = dbll.folderName,
                            sharedFolderPath = $@"{vf.RemoteFilePath}",
                            sourceFilePath = $@"{sourceFilePath}",
                            username = dbll.userName,
                            password = dbll.password,
                            domain = dbll.domain,
                            fileName = dbll.search_string_in_file_name
                        };
                        var fileModelStream = _ismbLibraryServices.SMBRequest(fileModel, flpConfigurationId, SMBRequestEnum.Stream);
                        var st = fileModelStream?.GetFileStream;

                        var (moved, msg) = await MoveFileInLineageFolder(
                            st,
                            vf.GeneratedFileName,
                            flpRequestDto.ProcessName,
                            flpConfigurationId,
                            uploadFileId,
                            landingLayerPath, dbll.securityGroupId,loginId,
                            databricksInfo);

                        if (moved)
                        {
                            movedFileCountToLandingLayer++;
                        }

                        await _landingLayerRepository.AddLandingLayerFileDetails(
                            flpConfigurationId, uploadFileId,
                            vf.File.FileName, vf.GeneratedFileName, moved, msg, vf.ErrorMessage,true);
                    }

                    string errorMessage = $"Total moved files {movedFileCountToLandingLayer} to landing layer \"{landingLayerPath}\"";
                    await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromRemoteToLandingLayer", $"MovedFileToLandingLayer", $"Valid files stage completed.Message {errorMessage}", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: null,
                        loginId: loginId,
                        message: $"{errorMessage}",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.ProcessCompleted,
                        FlpActivityLogStatusEnum.MovedFileToLandingLayer, null
                    );
                }

                //-------------------------------------------------------------------
                // 10) Move Invalid Files → Rejected
                //-------------------------------------------------------------------
                string rejectedPath = validationDetails.FileAdditionalDetails.rejectedLayerPath;

                if (invalidFiles.Any())
                {
                    flpActivityLogStatusEnum = FlpActivityLogStatusEnum.MovedFileToRejectedFolder;
                    int movedFileCountToRejectedFolder = 0;

                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: null,
                        loginId: loginId,
                        message: $"Moving file to rejected folder.{rejectedPath}",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.Processing,
                        FlpActivityLogStatusEnum.MovedFileToRejectedFolder, null
                    );

                    foreach (var inv in invalidFiles)
                    {

                        var sourceFilePath = FlpConfigurationHelperV4_1.GetLandingLayerFilePath(inv.RemoteFilePath, dbll.sourcePath);
                        if (string.IsNullOrWhiteSpace(sourceFilePath))
                        {
                            await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromRemoteToLandingLayer", $"MovedFileToRejectedFolder", $"Source file path is null or empty for file {inv.File.FileName}.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);                          
                        }

                        var fileModel = new CheckConnectivitySMBLibraryModel
                        {
                            serverIP = dbll.serverName,
                            sharedFolderName = dbll.folderName,
                            sharedFolderPath = $@"{inv.RemoteFilePath}",
                            sourceFilePath = sourceFilePath,
                            username = dbll.userName,
                            password = dbll.password,
                            domain = dbll.domain,
                            fileName = dbll.search_string_in_file_name
                        };
                        var fileModelStream = _ismbLibraryServices.SMBRequest(fileModel, flpConfigurationId, SMBRequestEnum.Stream);
                        var st = fileModelStream?.GetFileStream;
                        var (moved, msg) = await MoveFileInLineageFolder(
                            st,
                            inv.GeneratedFileName,
                            flpRequestDto.ProcessName,
                            flpConfigurationId,
                            uploadFileId,
                            rejectedPath, dbll.securityGroupId,loginId,
                            databricksInfo);

                        if (moved)
                        {
                            movedFileCountToRejectedFolder++;
                        }

                        await _landingLayerRepository.AddLandingLayerFileDetails(
                            flpConfigurationId, uploadFileId,
                            inv.File.FileName, inv.GeneratedFileName, moved, msg, inv.ErrorMessage,false);
                    }

                    string errorMessage = $"Total moved files {movedFileCountToRejectedFolder} to rejected folder \"{rejectedPath}\"";

                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: null,
                        loginId: loginId,
                        message: $"{errorMessage}",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.ProcessCompleted,
                        FlpActivityLogStatusEnum.MovedFileToRejectedFolder, null
                    );
                }

                //-------------------------------------------------------------------
                // 11) Delete Files from SMB Source (Similar to blob deletion)
                //-------------------------------------------------------------------
                flpActivityLogStatusEnum = FlpActivityLogStatusEnum.DeletedFilesFromFolder;
                await _iIFileProcessingService.AddFileProcessLosStatus(
                      tabName: null,
                      fileType: null,
                      loginId: loginId,
                      message: $"Files deleting from SMB source",
                      messageType: "info",
                      processId: processId,
                      processName: processName,
                      tableName: null,
                      totalRows: 0,
                      flpConfigurationId: flpConfigurationId,
                      fileUploadedId: uploadFileId,
                      FileStatusActivityEnum.Processing,
                      FlpActivityLogStatusEnum.DeletedFilesFromFolder, null
                  );

                int deletedFileCount = 0;
                foreach (var sf in files)
                {
                    _logger.LogInformation("Attempting to delete file {FilePath} from SMB source for FlpConfigurationId {FlpConfigurationId}", sf.FilePath, flpConfigurationId);


                    var sourceFilePath = FlpConfigurationHelperV4_1.GetLandingLayerFilePath(sf.FilePath, dbll.sourcePath);
                    if (string.IsNullOrWhiteSpace(sourceFilePath))
                    {
                        await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromRemoteToLandingLayer", $"DeletedFileFromMainFolder", $"Source file path is null or empty for file {sf.FileName}.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    }


                    // Delete the source file from SMB after processing
                    var deleteFileModel = new CheckConnectivitySMBLibraryModel
                    {
                        serverIP = dbll.serverName,
                        sharedFolderName = dbll.folderName,
                        sharedFolderPath = sourceFilePath,// sf.FilePath,
                        sourceFilePath = sourceFilePath,
                        username = dbll.userName,
                        password = dbll.password,
                        domain = dbll.domain,
                        fileName = dbll.search_string_in_file_name
                    };

                    var deleteResult = _ismbLibraryServices.SMBRequest(deleteFileModel, flpConfigurationId, SMBRequestEnum.DeleteFile);
                    bool deleted = deleteResult?.FileIsDeleted ?? false;

                    if (deleted)
                    {
                        deletedFileCount++;
                    }

                    string fileName = !string.IsNullOrWhiteSpace(sf.FileName) ? sf.FileName : Path.GetFileName(sf.FilePath);
                    string fileDeletedStatusMessage = $"Deleted file: {fileName} from SMB source after processing. Deletion status: {deleted}";
                    await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromRemoteToLandingLayer", $"FileDeletingFromBlob", $"{fileDeletedStatusMessage}", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                }

                if (deletedFileCount == files.Count)
                {
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: null,
                        loginId: loginId,
                        message: $"Files deleted from SMB source",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.ProcessCompleted,
                        FlpActivityLogStatusEnum.DeletedFilesFromFolder, null
                    );

                    flpActivityLogStatusEnum = FlpActivityLogStatusEnum.ProcessCompleted;
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: fileExtension,
                        loginId: loginId,
                        message: "Processing Completing",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.Processing,
                        FlpActivityLogStatusEnum.ProcessCompleted, null
                    );

                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: fileExtension,
                        loginId: loginId,
                        message: "Processing Completed",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.ProcessCompleted,
                        FlpActivityLogStatusEnum.ProcessCompleted, null
                    );

                    //-------------------------------------------------------------------
                    // 12) Build Response
                    //-------------------------------------------------------------------
                    var response = new FlpProcessResponseDto
                    {
                        Message = "File(s) processed from remote location to Landing Layer",
                        FileUploadedId = uploadFileId,
                        TotalRows = 0,
                        DuplicateRows = 0,
                        InsertedRows = 0,
                        BlobName = null
                    };

                    return new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Completed,
                        ResponseMessage = new(),
                        Result = response
                    };
                }
                else
                {
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                       tabName: null,
                       fileType: null,
                       loginId: loginId,
                       message: $"Not deleted files from SMB source",
                       messageType: "info",
                       processId: processId,
                       processName: processName,
                       tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.Error,
                        FlpActivityLogStatusEnum.DeletedFilesFromFolder, null
                       );
                }

                return new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Not all files were deleted from SMB source" },
                    Result = null
                };
            }
            catch (Exception ex)
            {
                await _iIFileProcessingService.AddFileProcessLosStatus(
                    tabName: null,
                    fileType: null,
                    loginId: loginId,
                    message: ex.Message.ToString(),
                    messageType: "error",
                    processId: processId,
                    processName: processName,
                    tableName: null,
                    totalRows: 0,
                    flpConfigurationId: flpConfigurationId,
                    fileUploadedId: uploadFileId,
                    FileStatusActivityEnum.Error,
                    flpActivityLogStatusEnum, null
                );

                _logger.LogError(ex, "Error in ProcessFileFromRemoteToLandingLayer for {FlpConfigurationId}", flpRequestDto?.FlpConfigurationId);
                return new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { ex.Message },
                    Result = null
                };
            }

            // Local helper for short error returns
            APIResponse<FlpProcessResponseDto> Error(string message) =>
                new()
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { message },
                    Result = null
                };
        }
        public async Task<APIResponse<FlpProcessResponseDto>> ProcessFileFromBlobToLandingLayer(FlpRequestDto4_1 flpRequestDto)
        {
            string flpConfigurationId = flpRequestDto?.FlpConfigurationId?.Trim() ?? string.Empty;
            string loginId = "function app";
            string processName = flpRequestDto?.ProcessName ?? string.Empty;
            long processId = long.Parse(DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            FlpActivityLogStatusEnum flpActivityLogStatusEnum = FlpActivityLogStatusEnum.FileProcessCreated;
            
            if (string.IsNullOrWhiteSpace(flpConfigurationId))
                return Error("flpConfigurationId is NULL");

            string uploadFileId = flpRequestDto?.LandingLayerUploadedId;// FlpConfigurationHelperV4_1.GetUploadedFileId(flpRequestDto);
            if (string.IsNullOrWhiteSpace(uploadFileId))
                return Error("Uploaded File Id is NULL");

            try
            {
                await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromBlobToLandingLayer", $"Process Started.", $"Process Started.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);

                //-------------------------------------------------------------------
                // 1) Fetch Landing Layer Details
                //-------------------------------------------------------------------
                var dbll = await _landingLayerRepository.GetLandingLayerDetailsAsync(flpConfigurationId);

                if (dbll == null || dbll.Result?.Equals("Failure", StringComparison.OrdinalIgnoreCase) == true)
                    return Error("Return failure from database");

                //-------------------------------------------------------------------
                // 2) Ensure process type = BlobStorageUpload
                //-------------------------------------------------------------------
                if (dbll.processTypeId != (int)ProcessTypeEnum.BlobStarageUpload)
                    return Error("ProcessTypeId is not BlobStarageUpload");

                //-------------------------------------------------------------------
                // 3) Find files in Blob Source Location
                //-------------------------------------------------------------------
                var blobFiles = await serviceHelper.FindLandingLayerBlobsInFoldersAsync(
                    dbll.sourceStorageAccount,
                    dbll.sourceStorageAccountKey,
                    dbll.sourceContainerName,
                    dbll.sourcePath,
                    dbll.search_string_in_file_name,
                    dbll.sasKey,
                    dbll.sasKeyToken
                );


                if (blobFiles == null || !blobFiles.Any())
                    return Error("No files found in blob source location.");

                var fileExtension = blobFiles.Count() == 1 ? FlpConfigurationHelper.GetFileExtension(Path.GetFileName(blobFiles.First().Name)) : null;

                await _iIFileProcessingService.AddFileProcessLosStatus(
                    tabName: null,
                    fileType: fileExtension,
                    loginId: loginId,
                    message: $"File Processing Initialization for total {blobFiles.Count()} files.",
                    messageType: "info",
                    processId: processId,
                    processName: processName,
                    tableName: null,
                    totalRows: 0,
                    flpConfigurationId: flpConfigurationId,
                    fileUploadedId: uploadFileId,
                    FileStatusActivityEnum.Processing,
                    FlpActivityLogStatusEnum.FileProcessCreated, null
                );

                //-------------------------------------------------------------------
                // 4) Validation Configuration
                //-------------------------------------------------------------------
                var validationDetails = await _landingLayerRepository.GetValidationDetailsAsync(flpConfigurationId);
                if (validationDetails == null)
                {
                    await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromBlobToLandingLayer", $"Process Started.", $"Not found validation details.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: fileExtension,
                        loginId: loginId,
                        message: "File Processing Initialization- Not found validation details.",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.Error,
                        FlpActivityLogStatusEnum.FileProcessCreated, null
                    );
                    return Error("Validation details not found");
                }

                await _iIFileProcessingService.AddFileProcessLosStatus(
                    tabName: null,
                    fileType: fileExtension,
                    loginId: loginId,
                    message: $"File Processing Initialization – Details Retrieved for total {blobFiles.Count()} files.",
                    messageType: "info",
                    processId: processId,
                    processName: processName,
                    tableName: null,
                    totalRows: 0,
                    flpConfigurationId: flpConfigurationId,
                    fileUploadedId: uploadFileId,
                    FileStatusActivityEnum.ProcessCompleted,
                    FlpActivityLogStatusEnum.FileProcessCreated, null
                );

                //-------------------------------------------------------------------
                // 5) Extension validation (using FileValidationForExtension)
                //-------------------------------------------------------------------
                flpActivityLogStatusEnum = FlpActivityLogStatusEnum.ValidationFileForExtension;

                await _iIFileProcessingService.AddFileProcessLosStatus(
                    tabName: null,
                    fileType: null,
                    loginId: loginId,
                    message: $"File extension validation started for total {blobFiles.Count()} files.",
                    messageType: "info",
                    processId: processId,
                    processName: processName,
                    tableName: null,
                    totalRows: 0,
                    flpConfigurationId: flpConfigurationId,
                    fileUploadedId: uploadFileId,
                    FileStatusActivityEnum.Processing,
                    FlpActivityLogStatusEnum.ValidationFileForExtension, null
                );

                var validExtFiles = new List<LandingLayerValidationFile>();
                var invalidExtFiles = new List<LandingLayerValidationFile>();

                await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromBlobToLandingLayer", $"ValidationFileForExtension.", $"ValidationFileForExtension started.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);

                foreach (var blob in blobFiles)
                {
                    string originalName = Path.GetFileName(blob.Name);

                    var stream = await blob.OpenReadAsync();

                    // Convert Blob → Fake IFormFile
                    IFormFile tempFile = new BlobFormFile(stream, originalName, originalName);

                    (string fileName, bool isValid) =
                        await FileValidationForExtension(tempFile, flpRequestDto.ProcessName,
                            flpConfigurationId, uploadFileId, loginId, validationDetails);

                    var llFile = new LandingLayerValidationFile
                    {
                        //OriginalFileName = originalName,
                        GeneratedFileName = fileName,
                        BlobClient = blob,
                        File = tempFile
                    };

                    if (isValid)
                        validExtFiles.Add(llFile);
                    else
                    {
                        llFile.ErrorMessage = $"Invalid extension for {originalName}";
                        invalidExtFiles.Add(llFile);
                    }
                }

                string inValidFilesMessage = invalidExtFiles.Count > 0 ? $"Validation failed for {invalidExtFiles.Count}: " : "All files are valid.";
                await _iIFileProcessingService.AddFileProcessLosStatus(
                    tabName: null,
                    fileType: null,
                    loginId: loginId,
                    message: $"File extension validation completed for total {blobFiles.Count()} files. Valid files {validExtFiles.Count} and Invalid files {invalidExtFiles.Count}",
                    messageType: "info",
                    processId: processId,
                    processName: processName,
                    tableName: null,
                    totalRows: 0,
                    flpConfigurationId: flpConfigurationId,
                    fileUploadedId: uploadFileId,
                    FileStatusActivityEnum.ProcessCompleted,
                    FlpActivityLogStatusEnum.ValidationFileForExtension, null
                );

                //-------------------------------------------------------------------
                // 6) Regex validation (using FileValidationForRegex)
                //-------------------------------------------------------------------
                flpActivityLogStatusEnum = FlpActivityLogStatusEnum.ValidationFileForRegex;
                bool hasRegex = validationDetails.RegexList?.Any() ?? false;

                List<LandingLayerValidationFile> validRegexFiles = new();
                List<LandingLayerValidationFile> invalidRegexFiles = new();

                if (hasRegex)
                {
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: null,
                        loginId: loginId,
                        message: "Regex validation started",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.Processing,
                        FlpActivityLogStatusEnum.ValidationFileForRegex, null
                    );

                    await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromBlobToLandingLayer", $"ValidationFileForRegex", $"ValidationFileForRegex started.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);

                    foreach (var file in validExtFiles)
                    {
                        (string fileName, bool isValid) =
                            await FileValidationForRegex(
                                file.File,
                                flpRequestDto.ProcessName,
                                flpConfigurationId,
                                uploadFileId,
                                loginId,
                                validationDetails);

                        file.GeneratedFileName = fileName;

                        if (isValid)
                            validRegexFiles.Add(file);
                        else
                        {
                            file.ErrorMessage = $"Regex failed for {file.GeneratedFileName}";
                            invalidRegexFiles.Add(file);
                        }
                    }

                    await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromBlobToLandingLayer", $"ValidationFileForRegex", $"ValidationFileForRegex completed.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    string inValidRegexMessage = invalidRegexFiles.Count > 0 ? $"Validation failed for {invalidRegexFiles.Count}: " : "All files are valid.";
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: null,
                        loginId: loginId,
                        message: $"Regex validation completed.{inValidRegexMessage}",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.ProcessCompleted,
                        FlpActivityLogStatusEnum.ValidationFileForRegex, null
                    );
                }

                //-------------------------------------------------------------------
                // 7) Final valid / invalid file sets
                //-------------------------------------------------------------------
                var validFiles = new List<LandingLayerValidationFile>();
                var invalidFiles = new List<LandingLayerValidationFile>();

                if (hasRegex)
                {
                    validFiles.AddRange(validRegexFiles);
                    invalidFiles.AddRange(invalidExtFiles);
                    invalidFiles.AddRange(invalidRegexFiles);
                }
                else
                {
                    validFiles.AddRange(validExtFiles);
                    invalidFiles.AddRange(invalidExtFiles);
                }

                //-------------------------------------------------------------------
                // 8) Target Databricks Storage Info
                //-------------------------------------------------------------------
                var databricksInfo =
                    await _fileLoadingProcessConfigurationService.DatabricksStorageAccountInfo(flpConfigurationId, null);

                //-------------------------------------------------------------------
                // 9) Move Valid Files → Landing Layer
                //-------------------------------------------------------------------
                string landingLayerPath = validationDetails.FileAdditionalDetails.landingLayerPath;

                if (validFiles.Any())
                {
                    flpActivityLogStatusEnum = FlpActivityLogStatusEnum.MovedFileToLandingLayer;
                    int movedFileCountToLandingLayer = 0;
                    await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromBlobToLandingLayer", $"MovedFileToLandingLayer", $"Valid files are started moving to landing layer.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: null,
                        loginId: loginId,
                        message: $"Moving file to landing layer.{landingLayerPath}",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.Processing,
                        FlpActivityLogStatusEnum.MovedFileToLandingLayer, null
                    );

                foreach (var vf in validFiles)
                {
                    var stream = await vf.BlobClient.OpenReadAsync();

                    var (moved, msg) = await MoveFileInLineageFolder(
                        stream, vf.GeneratedFileName,
                        flpRequestDto.ProcessName, flpConfigurationId, uploadFileId, landingLayerPath, dbll.securityGroupId,loginId,
                        databricksInfo);

                        if (moved)
                        {
                            movedFileCountToLandingLayer++;
                        }

                        await _landingLayerRepository.AddLandingLayerFileDetails(
                            flpConfigurationId, uploadFileId, vf.File.FileName,
                            vf.GeneratedFileName, moved, msg, vf.ErrorMessage, true);
                }

                    string errorMessage = $"Total moved files {movedFileCountToLandingLayer} to landing layer \"{landingLayerPath}\"";
                    await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromBlobToLandingLayer", $"MovedFileToLandingLayer", $"Valid files stage completed.Message {errorMessage}", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: null,
                        loginId: loginId,
                        message: $"{errorMessage}",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                                flpConfigurationId: flpConfigurationId,
                                fileUploadedId: uploadFileId,
                                FileStatusActivityEnum.ProcessCompleted,
                                FlpActivityLogStatusEnum.MovedFileToLandingLayer, null
                            );
                }

                //-------------------------------------------------------------------
                // 10) Move Invalid Files → Rejected
                //-------------------------------------------------------------------
                string rejectedPath = validationDetails.FileAdditionalDetails.rejectedLayerPath;

                if (invalidFiles.Any())
                {
                    flpActivityLogStatusEnum = FlpActivityLogStatusEnum.MovedFileToRejectedFolder;
                    int movedFileCountToRejectedFolder = 0;

                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: null,
                        loginId: loginId,
                        message: $"Moving file to rejected folder.{rejectedPath}",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.Processing,
                        FlpActivityLogStatusEnum.MovedFileToRejectedFolder, null
                    );

                    foreach (var inv in invalidFiles)
                    {
                        var stream = await inv.BlobClient.OpenReadAsync();
                       
                            var (moved, msg) = await MoveFileInLineageFolder(
                            stream, inv.GeneratedFileName,
                            flpRequestDto.ProcessName, flpConfigurationId, uploadFileId, rejectedPath, dbll.securityGroupId,loginId,
                             databricksInfo);

                        if (moved)
                        {
                            movedFileCountToRejectedFolder++;
                        }

                        await _landingLayerRepository.AddLandingLayerFileDetails(
                            flpConfigurationId, uploadFileId, inv.File.FileName,
                            inv.GeneratedFileName, moved, msg, inv.ErrorMessage,false);
                    }

                    string errorMessage = $"Total moved files {movedFileCountToRejectedFolder} to rejected folder \"{rejectedPath}\"";

                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: null,
                        loginId: loginId,
                        message: $"{errorMessage}",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.ProcessCompleted,
                        FlpActivityLogStatusEnum.MovedFileToRejectedFolder, null
                    );
                }

                flpActivityLogStatusEnum = FlpActivityLogStatusEnum.DeletedFilesFromFolder;
                await _iIFileProcessingService.AddFileProcessLosStatus(
                      tabName: null,
                      fileType: null,
                      loginId: loginId,
                      message: $"Files deleting from blob",
                      messageType: "info",
                      processId: processId,
                      processName: processName,
                      tableName: null,
                      totalRows: 0,
                      flpConfigurationId: flpConfigurationId,
                      fileUploadedId: uploadFileId,
                      FileStatusActivityEnum.Processing,
                      FlpActivityLogStatusEnum.DeletedFilesFromFolder, null
                  );
                int deletedFileCount = 0;
                foreach (var blob in blobFiles)
                {
                    try
                    {
                        var blobName = Path.GetFileName(blob.Name);
                        var containerSasUri = new Uri(FlpConfigurationHelper.GetBlobConnectionStringBySasKey(dbll.sourceStorageAccount, dbll.sasKey));

                        // 1. Construct the full, unambiguous URI for the specific blob.
                        var blobUriBuilder = new UriBuilder(containerSasUri)
                        {
                            Path = $"{dbll.sourceContainerName}/{blob.Name}"
                        };
                        var fullBlobUri = blobUriBuilder.Uri;

                        // 2. Log the exact URI that will be used for deletion.
                        //_logger.LogInformation("Attempting to delete blob using direct URI: {BlobUri}", fullBlobUri);

                        // 3. Create a BlobClient directly from the full URI.
                        var directBlobClient = new BlobClient(fullBlobUri);

                        // 4. Attempt the deletion.
                        var response = await directBlobClient.DeleteIfExistsAsync();
                        bool deleted = response.Value;

                        if (deleted)
                        {
                            deletedFileCount++;
                        }

                        string blobFileDeletedStatusMessage = $"Deleted file: {blobName} from blob storage after processing. Deletion status: {deleted}";
                        await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromBlobToLandingLayer", $"FileDeletingFromBlob", $"{blobFileDeletedStatusMessage}", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    }
                    catch (Exception ex)
                    {
                        var blobNameForLog = Path.GetFileName(blob.Name);
                        _logger.LogError(ex, "Failed to delete blob: {BlobName}", blobNameForLog);
                        await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromBlobToLandingLayer", "FileDeletingFromBlob", $"An unexpected error occurred while deleting blob {blobNameForLog}: {ex.Message}", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    }
                }
                //if (deletedFileCount == blobFiles.Count)
                    //flpActivityLogStatusEnum = FlpActivityLogStatusEnum.DeletedFilesFromFolder;
                    //await _iIFileProcessingService.AddFileProcessLosStatus(
                    //      tabName: null,
                    //      fileType: null,
                    //      loginId: loginId,
                    //      message: $"Files deleting from blob",
                    //      messageType: "info",
                    //      processId: processId,
                    //      processName: processName,
                    //      tableName: null,
                    //      totalRows: 0,
                    //      flpConfigurationId: flpConfigurationId,
                    //      fileUploadedId: uploadFileId,
                    //      FileStatusActivityEnum.Processing,
                    //      FlpActivityLogStatusEnum.DeletedFilesFromFolder, null
                    //  );
                    //int deletedFileCount = 0;
                    //foreach (var blob in blobFiles)
                    //{                 

                    //    // After moving the file, delete the source blob using SAS
                    //    var blobName = Path.GetFileName(blob.Name);
                    //    var blobServiceUriWithSas = FlpConfigurationHelper.GetBlobConnectionStringBySasKey(dbll.sourceStorageAccount, dbll.sasKey);
                    //    var blobClient = _iBlobStorageService.GetBlobClientDetailsBySasToken(blob.Name, blobServiceUriWithSas);
                    //    _logger.LogError($"Deleting blob {blobName} using SAS URI. Blob URI: {blobClient.Uri}");

                    //    bool deleted = await _iBlobStorageService.DeleteBlobAsync(blobClient);
                    //    if (deleted)
                    //    {
                    //        deletedFileCount++;
                    //    }
                    //    string blobFileDeletedStatusMessage = $"Deleted file: {blobName} from blob storage after processing. Deletion status: {deleted}";
                    //    await _landingLayerRepository.AddActivityLog("fun:ProcessFileFromBlobToLandingLayer", $"FileDeletingFromBlob", $"{blobFileDeletedStatusMessage} ", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    //}
                    if (deletedFileCount == blobFiles.Count)
                {
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: null,
                        loginId: loginId,
                        message: $"Files deleted from blob",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.ProcessCompleted,
                        FlpActivityLogStatusEnum.DeletedFilesFromFolder, null
                    );
                    flpActivityLogStatusEnum = FlpActivityLogStatusEnum.ProcessCompleted;
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: fileExtension,
                        loginId: loginId,
                        message: "Processing Completing",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.Processing,
                        FlpActivityLogStatusEnum.ProcessCompleted, null
                    );
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                        tabName: null,
                        fileType: fileExtension,
                        loginId: loginId,
                        message: "Processing Completed",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.ProcessCompleted,
                        FlpActivityLogStatusEnum.ProcessCompleted, null
                    );
                    //-------------------------------------------------------------------
                    // 11) Build Response
                    //-------------------------------------------------------------------
                    var response = new FlpProcessResponseDto
                    {
                        Message = "File Moved to Landing Layer",
                        FileUploadedId = uploadFileId,
                        TotalRows = 0,
                        DuplicateRows = 0,
                        InsertedRows = 0,
                        BlobName = null

                    };

                    return new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Completed,
                        ResponseMessage = new(),
                        Result = response
                    };
                }
                else {
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                       tabName: null,
                       fileType: null,
                       loginId: loginId,
                       message: $"Not deleted file from blob",
                       messageType: "info",
                       processId: processId,
                       processName: processName,
                       tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.Error,
                        FlpActivityLogStatusEnum.DeletedFilesFromFolder, null
                       );
                }

                return new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "Not deleted file" },
                    Result = null
                };

            }
            catch (Exception ex)
            {
                await _iIFileProcessingService.AddFileProcessLosStatus(
                    tabName: null,
                    fileType: null,
                    loginId: loginId,
                    message: ex.Message.ToString(),
                    messageType: "error",
                    processId: processId,
                    processName: processName,
                    tableName: null,
                    totalRows: 0,
                    flpConfigurationId: flpConfigurationId,
                    fileUploadedId: uploadFileId,
                    FileStatusActivityEnum.Error,
                    flpActivityLogStatusEnum, null
                );

                _logger.LogError(ex, "Error in ProcessFileFromBlobToLandingLayer for {FlpConfigurationId}", flpConfigurationId);
                return new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { ex.Message },
                    Result = null
                };
            }


            // Local helper for short error returns
            APIResponse<FlpProcessResponseDto> Error(string message) =>
                new()
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { message },
                    Result = null
                };
        }

            

        public async Task<APIResponse<FlpProcessResponseDto>> MoveUploadFilesToLayerFile(List<IFormFile> files, string processName, string flpConfigurationId, string uploadFileId, string loginId)
        {


            if (string.IsNullOrWhiteSpace(flpConfigurationId))
            {
                _logger.LogError("ProcessLandingLayerFile Error: flpConfigurationId is NULL");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "flpConfigurationId is NULL" },
                    Result = null
                });
            }

            if (string.IsNullOrWhiteSpace(uploadFileId))
            {
                _logger.LogError($"ProcessLandingLayerFile Error: Uploaded File Id is null for {flpConfigurationId}");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.InvalidParameters,
                    ResponseMessage = new List<string> { "Uploaded File Id is NULL" },
                    Result = null
                });
            }

            try
            {
                (bool ret, string message) = await MoveFileInLandingLayerFolder(files, processName, flpConfigurationId, uploadFileId, loginId);
                if (ret)
                {
                    await _fileLoadingProcessConfigurationService.UpdateFlpProcessStatus(uploadFileId, APIResultStatus.Completed);
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Completed,
                        ResponseMessage = new List<string> { "Process completed successfully" },
                        Result = null
                    });
                }
                else
                {
                    await _fileLoadingProcessConfigurationService.UpdateFlpProcessStatus(uploadFileId, APIResultStatus.Error);
                    _logger.LogError($"ProcessLandingLayerFile Error: Failed to process file from blob to landing layer for flpConfigurationId: {flpConfigurationId}");
                    return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                    {
                        ResultStatus = APIResultStatus.Error,
                        ResponseMessage = new List<string> { "Failed to process file from blob to landing layer" },
                        Result = null
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ProcessLandingLayerFile Error: Exception occurred for flpConfigurationId: {flpConfigurationId}");
                return await Task.FromResult(new APIResponse<FlpProcessResponseDto>
                {
                    ResultStatus = APIResultStatus.Error,
                    ResponseMessage = new List<string> { "An error occurred while processing the landing layer file" },
                    Result = null
                });
            }
        }



        public async Task<(bool, string)> MoveFileInLandingLayerFolder(List<IFormFile> files, string processName, string flpConfigurationId, string uploadFileId, string loginId)
        {
            FlpActivityLogStatusEnum flpActivityLogStatusEnum = FlpActivityLogStatusEnum.FileProcessCreated;
            long processId = long.Parse(DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            try
            {

                if (files == null || files.Count == 0)
                {
                    return (false, "No files provided.");
                    //return new APIResponse<string>
                    //{
                    //    ResultStatus = APIResultStatus.Error,
                    //    ResponseMessage = new List<string> { "No files provided." },
                    //    Result = null
                    //};
                }

                var fileExtension = files.Count() == 1 ? FlpConfigurationHelper.GetFileExtension(files[0].FileName) : null;

                await _landingLayerRepository.AddActivityLog("fun:MoveFileInLandingLayerFolder", $"Process Started.", $"Process Started.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);

                await _iIFileProcessingService.AddFileProcessLosStatus(
                  tabName: null,
                       fileType: fileExtension,
                       loginId: loginId,
                       message: $"File Processing Initialization for total {files.Count} files.",
                       messageType: "info",
                       processId: processId,
                       processName: processName,
                       tableName: null,
                       totalRows: 0,
                       flpConfigurationId: flpConfigurationId,
                       fileUploadedId: uploadFileId,
                       FileStatusActivityEnum.Processing,
                       FlpActivityLogStatusEnum.FileProcessCreated, null
                   );


                var validationDetails = await _landingLayerRepository.GetValidationDetailsAsync(flpConfigurationId);
                if (validationDetails == null)
                {
                    await _landingLayerRepository.AddActivityLog("fun:MoveFileInLandingLayerFolder", $"Process Started.", $"Not found validation details.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                      tabName: null,
                           fileType: fileExtension,
                           loginId: loginId,
                           message: "File Processing Initialization- Not found validation details.",
                           messageType: "info",
                           processId: processId,
                           processName: processName,
                           tableName: null,
                           totalRows: 0,
                           flpConfigurationId: flpConfigurationId,
                           fileUploadedId: uploadFileId,
                           FileStatusActivityEnum.Error,
                           FlpActivityLogStatusEnum.FileProcessCreated, null
                       );


                    return (false, "Some error occurred.");

                    
                }


                await _iIFileProcessingService.AddFileProcessLosStatus(
                 tabName: null,
                      fileType: fileExtension,
                      loginId: loginId,
                      message: $"File Processing Initialization – Details Retrieved for total {files.Count} files.",
                      messageType: "info",
                      processId: processId,
                      processName: processName,
                      tableName: null,
                      totalRows: 0,
                      flpConfigurationId: flpConfigurationId,
                      fileUploadedId: uploadFileId,
                      FileStatusActivityEnum.ProcessCompleted,
                      FlpActivityLogStatusEnum.FileProcessCreated, null
                  );


                flpActivityLogStatusEnum = FlpActivityLogStatusEnum.ValidationFileForExtension;


                await _iIFileProcessingService.AddFileProcessLosStatus(
                tabName: null,
                     fileType: null,
                     loginId: loginId,
                     message: $"File extension validation started for total {files.Count} files.",
                     messageType: "info",
                     processId: processId,
                     processName: processName,
                     tableName: null,
                     totalRows: 0,
                     flpConfigurationId: flpConfigurationId,
                     fileUploadedId: uploadFileId,
                     FileStatusActivityEnum.Processing,
                     FlpActivityLogStatusEnum.ValidationFileForExtension, null
                 );
                List<LandingLayerValidationFile> validExtensionFiles = new List<LandingLayerValidationFile>();
                List<LandingLayerValidationFile> inValidExtensionFiles = new List<LandingLayerValidationFile>();
                await _landingLayerRepository.AddActivityLog("fun:MoveFileInLandingLayerFolder", $"ValidationFileForExtension.", $"ValidationFileForExtension started.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                foreach (var file in files)
                {
                    LandingLayerValidationFile landingLayerValidationFile = new LandingLayerValidationFile();
                    (string fileName, bool isValid) = await FileValidationForExtension(file, processName, flpConfigurationId, uploadFileId, loginId, validationDetails);
                    if (isValid)
                    {
                        landingLayerValidationFile.GeneratedFileName = fileName;
                        landingLayerValidationFile.File = file;
                        validExtensionFiles.Add(landingLayerValidationFile);
                    }
                    else
                    {
                        landingLayerValidationFile.GeneratedFileName = fileName;
                        landingLayerValidationFile.File = file;
                        landingLayerValidationFile.ErrorMessage = $"File extension is not valid for file {file.FileName}";
                        inValidExtensionFiles.Add(landingLayerValidationFile);
                    }
                }

                string inValidFilesMessage = inValidExtensionFiles.Count > 0 ? $"Validation failed for {inValidExtensionFiles.Count}: " : "All files are valid.";
                await _iIFileProcessingService.AddFileProcessLosStatus(
                   tabName: null,
                        fileType: null,
                        loginId: loginId,
                        message: $"File extension validation completed for total {files.Count} files. Valid files {validExtensionFiles.Count} and Invalid files {inValidExtensionFiles.Count}",
                        messageType: "info",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.ProcessCompleted,
                        FlpActivityLogStatusEnum.ValidationFileForExtension, null
                );


                List<LandingLayerValidationFile> validRegexFiles = null;
                List<LandingLayerValidationFile> inValidRegexFiles = null;
                flpActivityLogStatusEnum = FlpActivityLogStatusEnum.ValidationFileForRegex;

                var isExistRegex = validationDetails?.RegexList?.Any() ?? false;
                if (isExistRegex)
                {
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                         tabName: null,
                              fileType: null,
                              loginId: loginId,
                              message: "Regex validation started",
                              messageType: "info",
                              processId: processId,
                              processName: processName,
                              tableName: null,
                              totalRows: 0,
                              flpConfigurationId: flpConfigurationId,
                              fileUploadedId: uploadFileId,
                              FileStatusActivityEnum.Processing,
                              FlpActivityLogStatusEnum.ValidationFileForRegex, null
                      );
                    validRegexFiles = new List<LandingLayerValidationFile>();
                    inValidRegexFiles = new List<LandingLayerValidationFile>();
                    await _landingLayerRepository.AddActivityLog("fun:MoveFileInLandingLayerFolder", $"ValidationFileForRegex", $"ValidationFileForRegex started.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    foreach (var file in validExtensionFiles.Select(e => e.File).ToList())
                    {
                        LandingLayerValidationFile landingLayerValidationFile = new LandingLayerValidationFile();
                        (string fileName, bool isValid) = await FileValidationForRegex(file, processName, flpConfigurationId, uploadFileId, loginId, validationDetails);
                        if (isValid)
                        {
                            landingLayerValidationFile.GeneratedFileName = fileName;
                            landingLayerValidationFile.File = file;
                            validRegexFiles.Add(landingLayerValidationFile);
                        }
                        else
                        {
                            landingLayerValidationFile.GeneratedFileName = fileName;
                            landingLayerValidationFile.File = file;
                            landingLayerValidationFile.ErrorMessage = $"File name is not matching with regex pattern for file {file.FileName}";
                            inValidRegexFiles.Add(landingLayerValidationFile);
                        }
                    }
                    await _landingLayerRepository.AddActivityLog("fun:MoveFileInLandingLayerFolder", $"ValidationFileForRegex", $"ValidationFileForRegex completed.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    string inValidRegexMessage = inValidRegexFiles.Count > 0 ? $"Validation failed for {inValidRegexFiles.Count}: " : "All files are valid.";
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                       tabName: null,
                            fileType: null,
                            loginId: loginId,
                            message: $"Regex validation completed.{inValidRegexMessage}",
                            messageType: "info",
                            processId: processId,
                            processName: processName,
                            tableName: null,
                            totalRows: 0,
                            flpConfigurationId: flpConfigurationId,
                            fileUploadedId: uploadFileId,
                            FileStatusActivityEnum.ProcessCompleted,
                            FlpActivityLogStatusEnum.ValidationFileForRegex, null
                    );
                }

                //Moving file based on conditions
                List<LandingLayerValidationFile> validFiles = new List<LandingLayerValidationFile>();
                List<LandingLayerValidationFile> inValidFiles = new List<LandingLayerValidationFile>();

                if (isExistRegex)
                {
                    if (validRegexFiles != null && validRegexFiles.Any())
                        validFiles.AddRange(validRegexFiles);


                    if (inValidExtensionFiles.Any())
                    {
                        inValidFiles.AddRange(inValidExtensionFiles);
                    }
                    if (inValidRegexFiles != null && inValidRegexFiles.Any())
                    {
                        inValidFiles.AddRange(inValidRegexFiles);
                    }
                }
                else
                {
                    if (validExtensionFiles != null && validExtensionFiles.Any())
                        validFiles.AddRange(validExtensionFiles);


                    if (inValidExtensionFiles.Any())
                    {
                        inValidFiles.AddRange(inValidExtensionFiles);
                    }

                }
                DatabricksStorageAccountDto4_1 databricksStorageAccountDto = await _fileLoadingProcessConfigurationService.DatabricksStorageAccountInfo(flpConfigurationId, null);
                ////Move file to landing layer folder and return success response with file name in landing layer folder as result so that it can be used in next steps of process.

                //Moved Valid files to landing layer folder
                if (validFiles.Any())
                {
                    flpActivityLogStatusEnum = FlpActivityLogStatusEnum.MovedFileToLandingLayer;

                    var landingLayerPath = validationDetails?.FileAdditionalDetails?.landingLayerPath;
                    int movedFileCountToLandingLayer = 0;
                    await _landingLayerRepository.AddActivityLog("fun:MoveFileInLandingLayerFolder", $"MovedFileToLandingLayer", $"Valid files are started moving to landing layer.", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                      tabName: null,
                           fileType: null,
                           loginId: loginId,
                           message: $"Moving file to landing layer.{landingLayerPath}",
                           messageType: "info",
                           processId: processId,
                           processName: processName,
                           tableName: null,
                           totalRows: 0,
                           flpConfigurationId: flpConfigurationId,
                           fileUploadedId: uploadFileId,
                           FileStatusActivityEnum.Processing,
                           FlpActivityLogStatusEnum.MovedFileToLandingLayer, null
                   );
                    foreach (var landingLayerValidationFile in validFiles)
                    {
                        var fileStream = landingLayerValidationFile.File.OpenReadStream();
                        string? errorMsg = landingLayerValidationFile?.ErrorMessage;
                        string originalFileName = landingLayerValidationFile.File.FileName;
                        var fileName = landingLayerValidationFile?.GeneratedFileName ?? landingLayerValidationFile?.File?.FileName ?? "";

                        (bool movedFile, string message) = await MoveFileInLineageFolder(fileStream, fileName, processName, flpConfigurationId, uploadFileId, landingLayerPath, validationDetails.FileAdditionalDetails.securityGroupId,loginId, databricksStorageAccountDto);
                        if (movedFile)
                        {
                            movedFileCountToLandingLayer++;
                        }
                        else
                        {
                            errorMsg = $"Failed to move file to landing layer for file {fileName}.Error: {message}";
                        }
                        await _landingLayerRepository.AddLandingLayerFileDetails(flpConfigurationId, uploadFileId, originalFileName, fileName, movedFile, message, errorMsg,true);
                    }

                    string errorMessage = $"Total moved files {movedFileCountToLandingLayer} to landing layer \"{landingLayerPath}\"";
                    await _landingLayerRepository.AddActivityLog("fun:MoveFileInLandingLayerFolder", $"MovedFileToLandingLayer", $"Valid files stage completed.Message {errorMessage}", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                     tabName: null,
                          fileType: null,
                          loginId: loginId,
                          message: $"{errorMessage}",
                          messageType: "info",
                          processId: processId,
                          processName: processName,
                          tableName: null,
                          totalRows: 0,
                          flpConfigurationId: flpConfigurationId,
                          fileUploadedId: uploadFileId,
                          FileStatusActivityEnum.ProcessCompleted,
                          FlpActivityLogStatusEnum.MovedFileToLandingLayer, null
                  );
                }


                //Rejected  files to failed folder
                if (inValidFiles.Any())
                {
                    flpActivityLogStatusEnum = FlpActivityLogStatusEnum.MovedFileToRejectedFolder;
                    var rejectedLayerPath = validationDetails?.FileAdditionalDetails?.rejectedLayerPath;
                    int movedFileCountToRejectedFolder = 0;

                    await _iIFileProcessingService.AddFileProcessLosStatus(
                    tabName: null,
                         fileType: null,
                         loginId: loginId,
                         message: $"Moving file to rejected folder.{rejectedLayerPath}",
                         messageType: "info",
                         processId: processId,
                         processName: processName,
                         tableName: null,
                         totalRows: 0,
                         flpConfigurationId: flpConfigurationId,
                         fileUploadedId: uploadFileId,
                         FileStatusActivityEnum.Processing,
                         FlpActivityLogStatusEnum.MovedFileToRejectedFolder, null
                     );
                    foreach (var landingLayerValidationFile in inValidFiles)
                    {
                        var fileStream = landingLayerValidationFile.File.OpenReadStream();
                        var fileName = landingLayerValidationFile?.GeneratedFileName ?? landingLayerValidationFile?.File?.FileName ?? "";
                        string? errorMsg = landingLayerValidationFile?.ErrorMessage;
                        string originalFileName = landingLayerValidationFile.File.FileName;
                        (bool movedFile, string message) = await MoveFileInLineageFolder(fileStream, fileName, processName, flpConfigurationId, uploadFileId, rejectedLayerPath, validationDetails.FileAdditionalDetails.securityGroupId,loginId, databricksStorageAccountDto);
                        if (movedFile)
                        {
                            movedFileCountToRejectedFolder++;
                        }
                        else
                        {
                            errorMsg += $"Failed to move file to rejected folder for file {fileName}.Error: {message}";
                        }
                        await _landingLayerRepository.AddLandingLayerFileDetails(flpConfigurationId, uploadFileId, originalFileName, fileName, movedFile, message, errorMsg,false);
                    }

                    string errorMessage = $"Total moved files {movedFileCountToRejectedFolder} to rejected folder \"{rejectedLayerPath}\"";

                    await _iIFileProcessingService.AddFileProcessLosStatus(
                     tabName: null,
                          fileType: null,
                          loginId: loginId,
                          message: $"{errorMessage}",
                          messageType: "info",
                          processId: processId,
                          processName: processName,
                          tableName: null,
                          totalRows: 0,
                          flpConfigurationId: flpConfigurationId,
                          fileUploadedId: uploadFileId,
                          FileStatusActivityEnum.ProcessCompleted,
                          FlpActivityLogStatusEnum.MovedFileToRejectedFolder, null
                  );
                }
                
                flpActivityLogStatusEnum = FlpActivityLogStatusEnum.ProcessCompleted;
                await _iIFileProcessingService.AddFileProcessLosStatus(
                 tabName: null,
                      fileType: fileExtension,
                      loginId: loginId,
                      message: "Processing Completing",
                      messageType: "info",
                      processId: processId,
                      processName: processName,
                      tableName: null,
                      totalRows: 0,
                      flpConfigurationId: flpConfigurationId,
                      fileUploadedId: uploadFileId,
                      FileStatusActivityEnum.Processing,
                      FlpActivityLogStatusEnum.ProcessCompleted, null
                  );
                await _iIFileProcessingService.AddFileProcessLosStatus(
                tabName: null,
                     fileType: fileExtension,
                     loginId: loginId,
                     message: "Processing Completed",
                     messageType: "info",
                     processId: processId,
                     processName: processName,
                     tableName: null,
                     totalRows: 0,
                     flpConfigurationId: flpConfigurationId,
                     fileUploadedId: uploadFileId,
                     FileStatusActivityEnum.ProcessCompleted,
                     FlpActivityLogStatusEnum.ProcessCompleted, null
                 );
                return (true, "Process completed successfully.");
            }
            catch (Exception ex)
            {

                await _iIFileProcessingService.AddFileProcessLosStatus(
                   tabName: null,
                        fileType: null,
                        loginId: "",
                        message: ex.Message.ToString(),
                        messageType: "error",
                        processId: processId,
                        processName: processName,
                        tableName: null,
                        totalRows: 0,
                        flpConfigurationId: flpConfigurationId,
                        fileUploadedId: uploadFileId,
                        FileStatusActivityEnum.Error,
                        flpActivityLogStatusEnum, null
                    );


            }
            return (false, "Some error occurred.");
            //return new APIResponse<string>
            //{
            //    ResultStatus = APIResultStatus.Error,
            //    ResponseMessage = new List<string> { "Some error occurred." },
            //    Result = null
            //};
        }




        private async Task<(string FileName, bool IsValid)> FileValidationForExtension(IFormFile file, string processName, string flpConfigurationId,string uploadFileId, string loginId, FlpValidationDetails validationDetails)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (string.IsNullOrWhiteSpace(file.FileName)) throw new ArgumentException("File must have a name.", nameof(file));

            string generatedFileName = file.FileName;
            bool isValid = false;

            try
            {
                // 1) Start activity log
                await _landingLayerRepository.AddActivityLog("fun:FileValidationForExtension", $"FileValidationForExtension.",
                    $"File Name: {file.FileName} validation started for extension",(int)ModuleTypeEnum.LandingLayer, flpConfigurationId,uploadFileId);

                // 2) Fetch config details               
                var prefix = validationDetails?.FileAdditionalDetails?.prefix;
                var dateFormat = validationDetails?.FileAdditionalDetails?.dateFormat;
                var timeFormat = validationDetails?.FileAdditionalDetails?.timeFormat;
                var extensionList = validationDetails?.ExtensionList?.Select(e=>e.fileExtension).ToList();
                // 3) Build output file name based on rules
                generatedFileName = LandingLayerHelper.BuildTargetFileName(originalFileName: file.FileName, prefix: prefix, dateFormat: dateFormat, timeFormat: timeFormat, now: DateTime.Now);// could be injected if needed

                // 4) Validate against extension list and regex list
                isValid = extensionList !=null && extensionList.Any()?LandingLayerHelper.IsExtensionAllowed(FlpConfigurationHelper.GetFileExtension(generatedFileName), extensionList): false; 
                
                await _landingLayerRepository.AddActivityLog("fun:FileValidationForExtension", $"FileValidationForExtension",
                 $"Error: File Name: {file.FileName} validation successfully completed, generatedFileName: {generatedFileName} & valid: {isValid}", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                return (generatedFileName, isValid);
            }
            catch (Exception ex)
            {
                isValid = false;
                await _landingLayerRepository.AddActivityLog("fun:FileValidationForExtension", $"FileValidationForExtension",$"Error: File Name: {file.FileName} validation :{ex.Message}", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                _logger.LogError($"Error: Landing Layer/FileValidationForExtension: {ex.Message}", ex);

            }
            return (generatedFileName, isValid);
        }

        private async Task<(string FileName, bool IsValid)> FileValidationForRegex(IFormFile file, string processName, string flpConfigurationId, string uploadFileId, string loginId, FlpValidationDetails validationDetails)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (string.IsNullOrWhiteSpace(file.FileName)) throw new ArgumentException("File must have a name.", nameof(file));

            string generatedFileName = file.FileName;
            bool isValid = false;

            try
            {
                // 1) Start activity log
                await _landingLayerRepository.AddActivityLog("fun:FileValidationForRegex", $"FileValidationForRegex",
                   $"File Name: {file.FileName} validation started for regex", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);


                // 2) Fetch config details                
                var prefix = validationDetails?.FileAdditionalDetails?.prefix;
                var dateFormat = validationDetails?.FileAdditionalDetails?.dateFormat;
                var timeFormat = validationDetails?.FileAdditionalDetails?.timeFormat;
                var landingLayerPath = validationDetails?.FileAdditionalDetails?.landingLayerPath;
                var extensionList = validationDetails?.ExtensionList?.Select(e => e.fileExtension).ToList();
                var regexList = validationDetails?.RegexList?.Select(e=>e.regex).ToList();
                // 3) Build output file name based on rules
                generatedFileName = LandingLayerHelper.BuildTargetFileName(originalFileName: file.FileName, prefix: prefix, dateFormat: dateFormat, timeFormat: timeFormat, now: DateTime.Now // could be injected if needed
                );

                string  fileNameWithoutExtention = Path.GetFileNameWithoutExtension(file.FileName);
                // 4) Validate against extension list and regex list
                // bool extensionOk = extensionList != null && extensionList.Any() ? LandingLayerHelper.IsExtensionAllowed(FlpConfigurationHelper.GetFileExtension(generatedFileName), extensionList) : true;
                isValid = LandingLayerHelper.IsNamePassingAnyRegex(fileNameWithoutExtention, regexList);               
                await _landingLayerRepository.AddActivityLog("fun:FileValidationForRegex", $"FileValidationForRegex",
                 $"File Name: {file.FileName} validation started for regex completed, generated  file Name : {generatedFileName}, isValid: {isValid}", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);

                return (generatedFileName, isValid);
            }
            catch (Exception ex)
            {
                isValid = false;
                await _landingLayerRepository.AddActivityLog("fun:FileValidationForRegex", $"FileValidationForRegex", $"Error: File Name: {file.FileName} validation regex :{ex.Message}", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);
                _logger.LogError($"Error: Landing Layer/FileValidationForRegex: {ex.Message}", ex);
               

            }

            return (generatedFileName, isValid);
        }

        private async Task<(bool, string)> MoveFileInLineageFolder(Stream fileStream, string fileName,string processName,string flpConfigurationId,string uploadFileId,
            string landingLayerStoragePath,string securityGroupId,string loginId, DatabricksStorageAccountDto4_1 databricksStorageAccountDto)
        {

            string destinationFolderPath = $"{landingLayerStoragePath}/{fileName}";
            long fileSizeInBytes = 0; // Get file size from stream if possible
            try
            {
                // Capture file size from stream if available
                if (fileStream.CanSeek)
                {
                    fileSizeInBytes = fileStream.Length;
                }
                BlobClient destinationBlob = null;                
                string destinationBlobConnectionString = "";
                string destinationBlobName = destinationFolderPath;
                await _landingLayerRepository.AddActivityLog("fun:MoveFileInLineageFolder", $"Move file stream to landing layer folder.", $"File Name: {fileName},storage account: {databricksStorageAccountDto.FlpStorageAccount}, container name: {databricksStorageAccountDto.StorageContainerName}, blobName: {destinationBlobName}, Process Name: {processName}", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);

                if (databricksStorageAccountDto.SasKeyToken)
                {
                   
                    destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionStringBySasKey(databricksStorageAccountDto.FlpStorageAccount, databricksStorageAccountDto.SasKey);
                    destinationBlob = _iBlobStorageService.GetBlobClientDetailsBySasToken(destinationBlobName, destinationBlobConnectionString);
                }
                else
                {
                    
                    destinationBlobConnectionString = FlpConfigurationHelper.GetBlobConnectionString(databricksStorageAccountDto.FlpStorageAccount, databricksStorageAccountDto.StorageAccountKey);
                    destinationBlob = _iBlobStorageService.GetBlobClientDetails(destinationBlobName, destinationBlobConnectionString, databricksStorageAccountDto.StorageContainerName);
                }
                (bool isCopiedFile, FlpProcessTempFile flpProcessTempFile) = await new BlobStorageService().CopyFileStreamToDestinationBlobAsync(fileStream, destinationBlob);
                // Log uploaded file details after successful upload
                LogUploadedFileRequest logUploadedFileRequest = new LogUploadedFileRequest
                {
                    fileName = fileName,
                    fileSize = fileSizeInBytes,
                   // uploadedDateTime = DateTime.UtcNow.ToString(),
                    uploadedBy = loginId,
                    flpConfigurationId = flpConfigurationId,
                    uploadFileId = uploadFileId,
                    securityGroupId = securityGroupId
                };                
                var res2 = await _processConfigurationServiceV4_1.LogUploadedFile(logUploadedFileRequest);

                string result = isCopiedFile ? "Success" : "Failed";
                await _landingLayerRepository.AddActivityLog("fun:MoveFileInLineageFolder", $"Move file stream to landing layer folder.", $"File Name: {fileName}, result {result}, blobName {destinationBlobName} and uri {flpProcessTempFile?.Uri}, Process Name: {processName}", (int)ModuleTypeEnum.LandingLayer, flpConfigurationId, uploadFileId);

                return (isCopiedFile, flpProcessTempFile?.Uri??"");

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: Landing Layer/MoveFileInFolder: {ex.Message}", ex);               
                return (false, ex.Message.ToString());
            }

        }

    }
}
