using Azure.Storage.Blobs;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using SMBLibrary;
using SMBLibrary.Client;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Text;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Enums.v3._0;
using Teleperformance.DataIngestion.Models.Helpers;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using FileAttributes = SMBLibrary.FileAttributes;

namespace Teleperformance.DataIngestion.DataAccess.Services.v4._0
{
    public class CsvToParquetServiceV4 : ICsvToParquetServiceV4
    {
        private readonly ILogger<CsvToParquetServiceV4> _logger;
        private readonly ISMBLibraryServices _ismbLibraryServices;
        private readonly IProcessConfigurationRepository _iIProcessConfigurationRepository;
        private readonly ICache _cache;
        private readonly IValidateSchemaService _iValidateSchemaService;
        private readonly IFlpProcessingService _iIFileProcessingService;
        private readonly IProcessConfigServiceV3 _iProcessConfigurationService;
        private readonly IProcessConfigurationRepositoryV4_1 _processConfigurationRepositoryV4_1;
        public CsvToParquetServiceV4(
           ILogger<CsvToParquetServiceV4> logger, ISMBLibraryServices ismbLibraryServices, IProcessConfigurationRepository iIProcessConfigurationRepository, ICache cache, IValidateSchemaService iValidateSchemaService, IFlpProcessingService iIFileProcessingService, IProcessConfigServiceV3 iProcessConfigurationService,IProcessConfigurationRepositoryV4_1 processConfigurationRepositoryV4_1)
        {
            _logger = logger;
            _ismbLibraryServices = ismbLibraryServices;
            _iIProcessConfigurationRepository = iIProcessConfigurationRepository;
            _cache = cache;
            _iValidateSchemaService = iValidateSchemaService;
            _iIFileProcessingService = iIFileProcessingService;
            _iProcessConfigurationService = iProcessConfigurationService;
            _processConfigurationRepositoryV4_1 = processConfigurationRepositoryV4_1;
        }
        public async Task<ParquetFileResponseDto> ConvertDataToParquetExcel(ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationResponseDto, FlpProcessTempFile fileProcessingConfig)
        {
            ParquetFileResponseDto resultResponse = null;

            try
            {
                configurationTableMappingDto.Delimiter = ",";
                string fileName = FlpConfigurationHelper.GetParquetFileName(fileProcessingConfig.Name);
                // Define the Parquet file path in the Blob Storage
                string parquetFilePath = $"{fileProcessingConfig.DestinationFolder}{fileName}";
                BlobServiceClient blobServiceClient = new BlobServiceClient(fileProcessingConfig.ParquetBlobConnectionString);
                BlobContainerClient csvContainerClient = blobServiceClient.GetBlobContainerClient(fileProcessingConfig.BlobContainerName);
                // BlobClient txtBlobClient = csvContainerClient.GetBlobClient(fileProcessingConfig.Name);
                BlobClient txtBlobClient = csvContainerClient.GetBlobClient(fileProcessingConfig.CsvFile.CsvName);
                using (var txtStream = await txtBlobClient.OpenReadAsync())
                {

                    BlobClient parquetBlobClient = csvContainerClient.GetBlobClient(parquetFilePath);
                    // Create a stream to write the Parquet file to Blob Storage
                    using (var parquetStream = await parquetBlobClient.OpenWriteAsync(overwrite: true))
                    {
                        // Convert CSV to Parquet

                        long fileSizeInBytes = txtStream.Length;
                        double fileSizeInMB = fileSizeInBytes / (1024.0 * 1024.0);
                        if(fileSizeInMB > 300)
                        {
                            throw new Exception("file is greater than 300 Mb, Please conact to administrative");
                        }


                        if (fileSizeInMB > 110)
                            resultResponse = await ConvertCsvToParquetAsyncV2(txtStream, parquetStream, configurationTableMappingDto, flpConfigurationResponseDto);
                        else
                            resultResponse = await ConvertCsvToParquetAsyncV1(txtStream, parquetStream, configurationTableMappingDto, flpConfigurationResponseDto);
                        resultResponse.ParquetFilePath = parquetFilePath;
                        resultResponse.ParquetBlobClient = parquetBlobClient;
                        resultResponse.ParquetBlobClientTemp = txtBlobClient;


                        return resultResponse;
                    }



                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to convert data to parquet Error: {ex.Message}", $"FlpConfigId: {flpConfigurationResponseDto.FlpConfigurationId}");
                resultResponse = new ParquetFileResponseDto();
                resultResponse.ErrorMessage = ex.Message.ToString();
                resultResponse.ParquetFileCreated = false;
                return resultResponse;
            }
        }

        public async Task<ParquetFileResponseDto> ConvertDataToParquet(ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationResponseDto, FlpProcessTempFile fileProcessingConfig)
        {
            ParquetFileResponseDto resultResponse = null;

            try
            {
                string fileName = FlpConfigurationHelper.GetParquetFileName(fileProcessingConfig.Name);
                // Define the Parquet file path in the Blob Storage
                string parquetFilePath = $"{fileProcessingConfig.DestinationFolder}{fileName}";
                parquetFilePath = Uri.UnescapeDataString(parquetFilePath).Replace("\\", "/") ?? "";
                BlobServiceClient blobServiceClient = new BlobServiceClient(fileProcessingConfig.ParquetBlobConnectionString);
                BlobContainerClient csvContainerClient = blobServiceClient.GetBlobContainerClient(fileProcessingConfig.BlobContainerName);
                BlobClient txtBlobClient = csvContainerClient.GetBlobClient(fileProcessingConfig.Name);
                using (var txtStream = await txtBlobClient.OpenReadAsync())
                {

                    BlobClient parquetBlobClient = csvContainerClient.GetBlobClient(parquetFilePath);
                    // Create a stream to write the Parquet file to Blob Storage
                    using (var parquetStream = await parquetBlobClient.OpenWriteAsync(overwrite: true))
                    {
                        // Convert CSV to Parquet
                       
                        long fileSizeInBytes = txtStream.Length;
                        double fileSizeInMB = fileSizeInBytes / (1024.0 * 1024.0);
                        if (fileSizeInMB > 300)
                        {
                            throw new Exception("file is greater than 300 Mb, Please contact to administrative");
                        }
                        if (fileSizeInMB > 110)
                            resultResponse = await ConvertCsvToParquetAsyncV2(txtStream, parquetStream, configurationTableMappingDto, flpConfigurationResponseDto);
                        else
                            resultResponse = await ConvertCsvToParquetAsyncV1(txtStream, parquetStream, configurationTableMappingDto, flpConfigurationResponseDto);
                        resultResponse.ParquetFilePath = parquetFilePath;
                        resultResponse.ParquetBlobClient = parquetBlobClient;
                        resultResponse.ParquetBlobClientTemp = txtBlobClient;


                        return resultResponse;
                    }



                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to convert data to parquet Error: {ex.Message}", $"FlpConfigId: {flpConfigurationResponseDto.FlpConfigurationId}");
                resultResponse = new ParquetFileResponseDto();
                resultResponse.ErrorMessage = ex.Message.ToString();
                resultResponse.ParquetFileCreated = false;
                return resultResponse;
            }
        }


        public async Task<ParquetFileResponseDto> ConvertDataToParquetOnPremSharedLocation(string txtTempPath, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationResponseDto, CheckConnectivitySMBLibraryModel destinationServerModel)
        {
            ParquetFileResponseDto resultResponse = new ParquetFileResponseDto();
            resultResponse.ParquetFileCreated = false;
            SMB2Client smbClient = new SMB2Client();
            ISMBFileStore fileStore = null;

            try
            {

                // Step 1: Generate parquet file name and path
                string fileName = FlpConfigurationHelper.GetParquetFileName(txtTempPath);
                string parquetFilePath = $"{flpConfigurationResponseDto.DestinationPath}{fileName}";
                parquetFilePath = Uri.UnescapeDataString(parquetFilePath).Replace("\\", "/") ?? "";
                // Step 2: Connect to the destination SMB server
                (smbClient, fileStore) = _ismbLibraryServices.SMBRequest(destinationServerModel, flpConfigurationResponseDto.FlpConfigurationId);

                // Step 3: Get the CSV stream from the SMB server
                using (var txtStream = _ismbLibraryServices.GetFileStream(fileStore, txtTempPath, flpConfigurationResponseDto.FlpConfigurationId))
                {
                    // Step 4: Create the destination parquet file in the remote server
                    object parquetFileHandle;
                    NTStatus status = fileStore.CreateFile(
                        out parquetFileHandle,
                        out _,
                        parquetFilePath,
                        AccessMask.GENERIC_WRITE,
                        FileAttributes.Normal,
                        ShareAccess.None,
                        CreateDisposition.FILE_CREATE, // Create file if it doesn't exist
                        CreateOptions.FILE_NON_DIRECTORY_FILE,
                        null);

                    if (status != NTStatus.STATUS_SUCCESS)
                    {
                        throw new Exception($"Failed to create file on SMB: {parquetFilePath}. Error: {status}");
                    }

                    try
                    {
                        // Step 5: Write the CSV data to the parquet file stream
                        using (var parquetStream = new SmbFileWriteStream(fileStore, parquetFileHandle))
                        {
                            long fileSizeInBytes = txtStream.Length;
                            double fileSizeInMB = fileSizeInBytes / (1024.0 * 1024.0);
                            if (fileSizeInMB > 300)
                            {
                                throw new Exception("file is greater than 300 Mb, Please contact to administrative");
                            }
                            if (fileSizeInMB >110)
                            resultResponse = await ConvertCsvToParquetAsyncV2(txtStream, parquetStream, configurationTableMappingDto, flpConfigurationResponseDto);
                            else
                                resultResponse = await ConvertCsvToParquetAsyncV1(txtStream, parquetStream, configurationTableMappingDto, flpConfigurationResponseDto);
                        }

                        // Mark the parquet file path and success status
                        resultResponse.ParquetFilePath = parquetFilePath;
                        //resultResponse.ParquetFileCreated = true;
                    }
                    finally
                    {
                        // Step 6: Close the file handle after writing
                        fileStore.CloseFile(parquetFileHandle);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to convert data to parquet Error: {ex.Message}", $"FlpConfigId: {flpConfigurationResponseDto.FlpConfigurationId}");
                resultResponse.ParquetFileCreated = false;
                resultResponse.ErrorMessage = ex.Message.ToString();
            }
            finally
            {
                // Ensure proper cleanup
                smbClient.Logoff();
                smbClient.Disconnect();
            }

            return resultResponse;
        }


        private async Task<ParquetFileResponseDto> ConvertCsvToParquetAsyncV1(Stream txtStream, Stream parquetStream, ConfigurationTableMappingDto parameterConfig, FlpConfigurationResponseDto flpConfigurationResponseDto)
        {

            string errorMessage = "";
            CsvConfiguration csvConfig = null;
            var resultResponse = new ParquetFileResponseDto();
            resultResponse.ParquetFileCreated = false;
            IEnumerable<FlpFileColumnMappingDto> configurationFileColumns = null;
            try
            {
                //Retrieving  all columns first 
                bool skipDelimiter = false;
                configurationFileColumns = await _iValidateSchemaService.GetFileColumnList(parameterConfig.FlpConfigurationId);
                if ((string.Compare(parameterConfig.Delimiter, "none", true) == 0 && configurationFileColumns.Any() && configurationFileColumns.Count() == 1) || (configurationFileColumns.Any() && configurationFileColumns.Count() == 1))
                {
                    skipDelimiter = true;
                }
                if (string.Compare(parameterConfig.Delimiter, "\\t", true) == 0)
                {
                    parameterConfig.Delimiter = FlpConfigurationHelper.EscapedTabString(parameterConfig.Delimiter.Trim());
                }
                if (!skipDelimiter)
                {

                    var isValidFile = TextAndCsvHelper.IsValidDelimiter(txtStream, parameterConfig.Delimiter);
                    if (!isValidFile)
                    {
                        throw new InvalidOperationException("Unable to determine the CSV delimiter.");
                    }
                }



                List<string> columnNameList = new List<string>();
                bool createFileColumn = false;

                csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = parameterConfig.Delimiter,
                    Quote = Convert.ToChar(parameterConfig.QuoteCharacter),
                    IgnoreBlankLines = parameterConfig.SkipEmptyLines ? true : false,
                    HasHeaderRecord = parameterConfig.IsHeaderProvided,
                    BadDataFound = null,
                    TrimOptions = TrimOptions.Trim,
                    MissingFieldFound = null  //Ignores missing fields in some rows
                };
                // Read the remaining rows into a list

            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                await _iIFileProcessingService.AddFileProcessLosStatus(
                         fileType: flpConfigurationResponseDto.FileType,
                         loginId: "",
                         message: errorMessage,
                         messageType: "error",
                         processId: flpConfigurationResponseDto.ProcessId,
                         processName: flpConfigurationResponseDto.ProcessName,
                         tableName: parameterConfig.TableName,
                         totalRows: resultResponse.TotalRows,
                         flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                         fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                         FileStatusActivityEnum.Error,
                          FlpActivityLogStatusEnum.FileSchemaValidated
                     );
                resultResponse.flpActivityLogStatusEnum = FlpActivityLogStatusEnum.FileSchemaValidated;
                return resultResponse;
            }

            Encoding encoding = FlpConfigurationHelper.GetEncoding(); // ANSI/Windows-1252 for Spanish
            using (var reader = new StreamReader(txtStream, encoding))
            using (var csv = new CsvReader(reader, csvConfig))
            {
                bool hasDuplicateOrEmptyHeader = false;
                List<string> headerRow = new List<string>();
                List<string> filesHeaders = new List<string>();
                List<string> copyFilesHeaders = new List<string>();
                var formats = await _cache.GetFormatListAsync();
                resultResponse.ColumnDataTypeList = new Dictionary<string, DataTypeDetails>();
                var customHeadersColumnsMapping = new Dictionary<string, string>();
                Dictionary<string, string> originalToTransformedColumnNameMap = new Dictionary<string, string>();
                var dedupColumnsList = FlpConfigurationHelper.SplitString(parameterConfig.OrderByColumnListForDedup, ",");
                var fileColumnMapping = new Dictionary<string, FlpFileColumnMappingDto>();
                List<string> cleanFileColumns = new List<string>();
                //var (convertDatatypeString, columnDataTypeMappings) = FlpConfigurationHelper.GetColumnDataTypeList(parameterConfig.ConvertDataTypeColumnNameList);
                // Skip initial rows
                for (int i = 0; i < parameterConfig.SkipRows; i++)
                {
                    csv.Read();
                }

                try
                {

                    if (!string.IsNullOrWhiteSpace(parameterConfig.KeyColumnList))
                    {
                        parameterConfig.KeyColumnList = parameterConfig.KeyColumnList.ToLower();
                    }
                    (bool ret, string message) = FlpConfigurationHelper.checkSelectedValidOptions(parameterConfig, parameterConfig.KeyColumnList, parameterConfig.OrderByColumnListForDedup);
                    if (!ret)
                    {
                        throw new Exception(message);
                    }

                    if (parameterConfig.IsHeaderProvided)
                    {
                        csv.Read();
                        csv.ReadHeader();
                        var getFileHeaders = csv.Context.Reader.HeaderRecord.ToList();
                        filesHeaders = getFileHeaders;
                        //clean column which is not Empty Column
                        filesHeaders = filesHeaders.Select(x => !string.IsNullOrWhiteSpace(x) ? FlpConfigurationHelper.CleanColumnName(x) : x).ToList();
                        //conversion spanish
                        if (parameterConfig.SpanishToEnglish)
                        {

                            filesHeaders = await TextAndCsvHelper.ConvertSpanishToEnglish(filesHeaders, _iProcessConfigurationService);

                        }

                        //order to roman
                        if (parameterConfig.OrdinalToRoman)
                        {
                            filesHeaders = filesHeaders.Select(cell => !string.IsNullOrWhiteSpace(cell.ToString()) ? FlpConfigurationHelper.ConvertToRoman(cell.ToString()) : cell.ToString()).ToList();
                        }
                        //duplicate search
                        hasDuplicateOrEmptyHeader = FlpConfigurationHelper.ContainsDuplicatesOrEmptyHeader(filesHeaders);
                        if (hasDuplicateOrEmptyHeader)
                        {
                            filesHeaders = FlpConfigurationHelper.CreateCustomHeader(filesHeaders);

                            filesHeaders = filesHeaders.Select(x => FlpConfigurationHelper.CleanColumnName(x)).ToList();


                        }
                        else
                        {
                            filesHeaders = filesHeaders.Select(x => FlpConfigurationHelper.CleanColumnName(x)).ToList();
                        }


                        if (filesHeaders.Any())
                        {
                            copyFilesHeaders = new List<string>(filesHeaders);
                            bool exists = filesHeaders.Contains("divalidationrowno");
                            if (exists)
                            {
                                copyFilesHeaders.Remove("divalidationrowno");
                            }


                            cleanFileColumns = filesHeaders;//Use created custom header list
                            fileColumnMapping = await _iValidateSchemaService.ValidateFileSchema(parameterConfig.FlpConfigurationId, parameterConfig.ValidateFileSchema, parameterConfig.IsHeaderProvided, copyFilesHeaders, flpConfigurationResponseDto.ProcessName, parameterConfig.TableName, configurationFileColumns);
                            if (flpConfigurationResponseDto.UIValidation || flpConfigurationResponseDto.BEValidation)
                            {
                                var flpFileColumnMappingDto = FlpConfigurationHelperV4_1.AddRowNumberInColumnList();
                                fileColumnMapping.Add(flpFileColumnMappingDto.FileColumn, flpFileColumnMappingDto);

                            }
                            customHeadersColumnsMapping = FlpConfigurationHelper.MapHeadersToColumns(
                                                                                hasDuplicateOrEmptyHeader,
                                                                                filesHeaders,
                                                                                getFileHeaders,
                                                                                fileColumnMapping,
                                                                                mappingDto => mappingDto.DbColumn
                                                                            );

                            // If you need a list of headers:
                            headerRow = customHeadersColumnsMapping.Values.ToList();


                        }
                        else
                        {
                            throw new Exception("Unable to proceed file : column not cleaned");
                        }
                    }
                    else
                    {

                        if (flpConfigurationResponseDto.UIValidation || flpConfigurationResponseDto.BEValidation)
                        {

                            csv.Read();
                            var firstRow = csv.Parser.RawRecord.Split(parameterConfig.Delimiter);
                            var filesCustomHeaders = Enumerable.Range(1, firstRow.Length).Select(i => $"Field{i}").ToList();
                            if (filesCustomHeaders.Any())
                            {

                                //chang field to col
                                // Updated with "Col" in the list
                                var updatedHeaders = Enumerable.Range(0, firstRow.Length - 1).Select(i => $"col{i}").ToList();

                                //Check roman numbers -> file Header will converted into roman numbers
                                if (parameterConfig.OrdinalToRoman)
                                {
                                    updatedHeaders = updatedHeaders.Select(x => FlpConfigurationHelper.ConvertToRoman(x)).ToList();
                                }
                                cleanFileColumns = filesCustomHeaders;//Use created custom header list
                                fileColumnMapping = await _iValidateSchemaService.ValidateFileSchema(parameterConfig.FlpConfigurationId, parameterConfig.ValidateFileSchema, parameterConfig.IsHeaderProvided, updatedHeaders, flpConfigurationResponseDto.ProcessName, parameterConfig.TableName, configurationFileColumns);

                                if (flpConfigurationResponseDto.UIValidation || flpConfigurationResponseDto.BEValidation)
                                {
                                    var flpFileColumnMappingDto = FlpConfigurationHelperV4_1.AddRowNumberInColumnList();
                                    fileColumnMapping.Add(flpFileColumnMappingDto.FileColumn, flpFileColumnMappingDto);
                                    filesHeaders = fileColumnMapping.Values.Select(x => x.DbColumn).ToList();
                                    bool exists = updatedHeaders.Contains("divalidationrowno");
                                    if (!exists)
                                    {
                                        updatedHeaders.Add("divalidationrowno");
                                    }

                                }
                                customHeadersColumnsMapping = FlpConfigurationHelper.MapHeadersToColumns(
                                                                                       hasDuplicateOrEmptyHeader,
                                                                                       updatedHeaders,
                                                                                       filesCustomHeaders,
                                                                                       fileColumnMapping,
                                                                                       mappingDto => mappingDto.DbColumn);
                                // If you need a list of headers:
                                headerRow = customHeadersColumnsMapping.Values.ToList();
                                //filesHeaders = updatedHeaders;

                            }
                        }
                        else
                        {

                            csv.Read();
                            var firstRow = csv.Parser.RawRecord.Split(parameterConfig.Delimiter);
                            var filesCustomHeaders = Enumerable.Range(1, firstRow.Length).Select(i => $"Field{i}").ToList();
                            if (filesCustomHeaders.Any())
                            {

                                //chang field to col
                                // Updated with "Col" in the list
                                var updatedHeaders = Enumerable.Range(0, firstRow.Length).Select(i => $"col{i}").ToList();

                                //Check roman numbers -> file Header will converted into roman numbers
                                if (parameterConfig.OrdinalToRoman)
                                {
                                    updatedHeaders = updatedHeaders.Select(x => FlpConfigurationHelper.ConvertToRoman(x)).ToList();
                                }
                                cleanFileColumns = filesCustomHeaders;//Use created custom header list
                                fileColumnMapping = await _iValidateSchemaService.ValidateFileSchema(parameterConfig.FlpConfigurationId, parameterConfig.ValidateFileSchema, parameterConfig.IsHeaderProvided, updatedHeaders, flpConfigurationResponseDto.ProcessName, parameterConfig.TableName, configurationFileColumns);
                                customHeadersColumnsMapping = FlpConfigurationHelper.MapHeadersToColumns(
                                                                                       hasDuplicateOrEmptyHeader,
                                                                                       updatedHeaders,
                                                                                       filesCustomHeaders,
                                                                                       fileColumnMapping,
                                                                                       mappingDto => mappingDto.DbColumn);
                                // If you need a list of headers:
                                headerRow = customHeadersColumnsMapping.Values.ToList();

                            }
                        }

                    }
                    //custom header not  created 
                    if (headerRow != null && headerRow.Any() && headerRow.Count == customHeadersColumnsMapping.Keys.Count)
                    {
                        await _iIFileProcessingService.AddFileProcessLosStatus(
                            fileType: flpConfigurationResponseDto.FileType,
                            loginId: "",
                            message: $"File schema is validated",
                            messageType: "info",
                            processId: flpConfigurationResponseDto.ProcessId,
                            processName: flpConfigurationResponseDto.ProcessName,
                            tableName: parameterConfig.TableName,
                            totalRows: 0,
                            flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                            fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                            FileStatusActivityEnum.ProcessCompleted,
                            FlpActivityLogStatusEnum.FileSchemaValidated
                            );
                    }
                    else
                    {
                        errorMessage = headerRow != null && headerRow.Any() ? "file header does not match with existing database column" : "Not found file header";
                        await _iIFileProcessingService.AddFileProcessLosStatus(
                                 fileType: flpConfigurationResponseDto.FileType,
                                 loginId: "",
                                 message: errorMessage,
                                 messageType: "error",
                                 processId: flpConfigurationResponseDto.ProcessId,
                                 processName: flpConfigurationResponseDto.ProcessName,
                                 tableName: parameterConfig.TableName,
                                 totalRows: resultResponse.TotalRows,
                                 flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                                 fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                                 FileStatusActivityEnum.Error,
                                  FlpActivityLogStatusEnum.FileSchemaValidated
                             );
                        resultResponse.flpActivityLogStatusEnum = FlpActivityLogStatusEnum.FileSchemaValidated;
                        return resultResponse;
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                             fileType: flpConfigurationResponseDto.FileType,
                             loginId: "",
                             message: errorMessage,
                             messageType: "error",
                             processId: flpConfigurationResponseDto.ProcessId,
                             processName: flpConfigurationResponseDto.ProcessName,
                             tableName: parameterConfig.TableName,
                             totalRows: resultResponse.TotalRows,
                             flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                             fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                             FileStatusActivityEnum.Error,
                              FlpActivityLogStatusEnum.FileSchemaValidated
                         );
                    resultResponse.flpActivityLogStatusEnum = FlpActivityLogStatusEnum.FileSchemaValidated;
                    return resultResponse;
                }

                //Schema validation is completed...
                if (flpConfigurationResponseDto.UIValidation || flpConfigurationResponseDto.BEValidation)
                {
                    if (filesHeaders.Any())
                        await _processConfigurationRepositoryV4_1.AddFileHeaders(parameterConfig.FlpConfigurationId, flpConfigurationResponseDto.UploadedFileId, string.Join(",", filesHeaders), parameterConfig.TabName);
                }

                //Conversion is started
                await _iIFileProcessingService.AddFileProcessLosStatus(
                          fileType: flpConfigurationResponseDto.FileType,
                          loginId: "",
                          message: $"File conversion process in progress",
                          messageType: "info",
                          processId: flpConfigurationResponseDto.ProcessId,
                          processName: flpConfigurationResponseDto.ProcessName,
                          tableName: parameterConfig.TableName,//flpConfigurationRequestDto.TableName,
                          totalRows: 0,
                          flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                          fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                          FileStatusActivityEnum.Processing,
                          FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation);

                // Transform column names and map them                
                var fields = new DataField[headerRow.Count];
                for (int i = 0; i < headerRow.Count; i++)
                {
                    DataTypeDetails dataTypeDetails = new DataTypeDetails();
                    var header = headerRow[i];
                    string format = "";
                    var getHeaderDetails = headerRow[i];
                    FlpFileColumnMappingDto flpFileColumnMappingDto = fileColumnMapping.Values.FirstOrDefault(x => x.DbColumn == header);
                    if (flpFileColumnMappingDto != null)
                    {

                        var dataTypeId = flpFileColumnMappingDto.DataTypeId;
                        var value = FlpConfigurationHelper.GetDateTypeValue(dataTypeId);
                        dataTypeDetails.DataType = value;
                        dataTypeDetails.DataTypeFormat = string.Empty;
                        if (dataTypeId == (int)DatatypeNamesEnum.DATE || dataTypeId == (int)DatatypeNamesEnum.DATETIME || dataTypeId == (int)DatatypeNamesEnum.TIME)
                        {
                            var formatId = flpFileColumnMappingDto.FormatId;
                            format = formats?.FirstOrDefault(fv => fv.formatId == formatId).apiFormat ?? "";
                            if (string.IsNullOrWhiteSpace(format))
                                throw new Exception("Format doesn't exit for configuration " + parameterConfig.FlpConfigurationId);
                            dataTypeDetails.DataTypeFormat = format;
                        }
                        fields[i] = dataTypeId switch
                        {
                            (int)DatatypeNamesEnum.STRING => new DataField<string?>(header),
                            (int)DatatypeNamesEnum.INT => new DataField<int?>(header),
                            (int)DatatypeNamesEnum.LONG => new DataField<long?>(header),
                            (int)DatatypeNamesEnum.FLOAT => new DataField<float?>(header),
                            (int)DatatypeNamesEnum.DOUBLE => new DataField<double?>(header),
                            (int)DatatypeNamesEnum.BOOL => new DataField<bool?>(header),
                            (int)DatatypeNamesEnum.DATETIME => new DataField<DateTime?>(header),
                            (int)DatatypeNamesEnum.DATE => new DataField<DateOnly?>(header),
                            (int)DatatypeNamesEnum.TIME => new DataField<TimeOnly?>(header),
                            _ => new DataField<string?>(header), // default to string if the data type is unrecognized
                        };
                    }
                    resultResponse.ColumnDataTypeList[header] = dataTypeDetails;
                }

                var schema = new ParquetSchema(fields);

                using (var parquetWriter = await ParquetWriter.CreateAsync(schema, parquetStream))
                {
                    if (parameterConfig.ParquetCompression?.ToUpper() == "GZIP")
                    {
                        parquetWriter.CompressionMethod = CompressionMethod.Gzip;
                        parquetWriter.CompressionLevel = System.IO.Compression.CompressionLevel.Optimal;
                    }

                    using (ParquetRowGroupWriter groupWriter = parquetWriter.CreateRowGroup())
                    {
                        var columns = headerRow.Select(header => new List<object>()).ToList();
                        var rows = new List<dynamic>();
                        var uniqueKeys = new HashSet<string>();
                        var tempRows = new Dictionary<string, dynamic>();
                        var uniqueRowsWithDedup = new Dictionary<string, List<List<object>>>();
                        var keyColumnList = FlpConfigurationHelper.SplitString(parameterConfig.KeyColumnList, ",");
                        int incrementRowsCount = 0;
                        if (!parameterConfig.IsHeaderProvided && !string.IsNullOrWhiteSpace(parameterConfig.ColumnNameList))
                        {
                            (rows, uniqueKeys, tempRows) = TextAndCsvHelper.GetFirstRows(csv, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueKeys, tempRows, rows, keyColumnList);
                            incrementRowsCount++;
                        }
                        //Get all csv records into dynamic list
                        var csvRecords = new List<dynamic>();
                        if (hasDuplicateOrEmptyHeader)
                        {
                            while (csv.Read())
                            {
                                dynamic record = new ExpandoObject(); //Use ExpandoObject to create dynamic entries
                                var recordDict = (IDictionary<string, object>)record;
                                int i = 0;
                                foreach (var header in cleanFileColumns)
                                {
                                    //string cleanHeader = FlpConfigurationHelper.CleanColumnName(header);
                                    if (customHeadersColumnsMapping.TryGetValue(header, out var mappedValue) && !string.IsNullOrWhiteSpace(mappedValue))
                                    {
                                        recordDict[mappedValue] = csv.GetField(i); //Map the custom header to the value
                                    }
                                    //recordDict[kvp] = csv.GetField(i);
                                    i++;
                                }


                                if (parameterConfig.SkipEmptyLines)
                                {
                                    if (TextAndCsvHelper.IsEmptyLine(record))
                                    {
                                        continue;
                                    }
                                }
                                csvRecords.Add(record); //Add each dynamic record to the list
                            }
                        }
                        else
                        {
                            csvRecords = csv.GetRecords<dynamic>().ToList();
                        }

                        int totalRecordsCount = csvRecords.Count;
                        // Step 3: Skip footer rows
                        int actualRecordsCount = totalRecordsCount - parameterConfig.SkipFooterRows;
                        resultResponse.TotalRows = actualRecordsCount + incrementRowsCount;

                        if (parameterConfig.IgnoreDuplicateRows)
                        {
                            foreach (var keyValuePair in customHeadersColumnsMapping)
                            {
                                originalToTransformedColumnNameMap.Add(FlpConfigurationHelper.CleanColumnName(keyValuePair.Key), keyValuePair.Value);
                            }
                        }

                        if (dedupColumnsList.Any())
                        {
                            rows = TextAndCsvHelper.GetRowsFromCsvReaderWithDedupColumn(csvRecords, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueRowsWithDedup, rows, actualRecordsCount, keyColumnList, resultResponse.ColumnDataTypeList);
                        }
                        else
                        {
                            rows = TextAndCsvHelper.GetRowsFromCsvReader(csvRecords, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueKeys, tempRows, rows, actualRecordsCount, keyColumnList);
                        }


                        //Order by descending & Ignore Duplicate Rows
                        if (parameterConfig.IgnoreDuplicateRows && !parameterConfig.KeepFirstRow && !dedupColumnsList.Any())
                        {
                            rows = tempRows.Values.ToList();
                            // resultResponse.DuplicateRows = (resultResponse.TotalRows - rows.Count);
                        }

                        //Dedup column opted and ignore duplicate rows

                        if (dedupColumnsList.Any())
                        {

                            // Initialize a new dictionary to store deduplicated rows with unique keys
                            var deduplicatedDict = new Dictionary<string, List<object>>();


                            foreach (var kvp in uniqueRowsWithDedup)
                            {
                                var key = kvp.Key;  // Get the unique key for the current entry
                                var rowData = kvp.Value;

                                IOrderedEnumerable<List<object>> orderedRows = null;
                                // Multi-column sorting without ConvertValue
                                if (parameterConfig.KeepFirstRow)
                                {
                                    orderedRows = rowData.OrderBy(row =>
                                    {
                                        return dedupColumnsList.Select(col =>
                                        {
                                            string columnName = originalToTransformedColumnNameMap[col.ToString()];
                                            return TextAndCsvHelper.GetDictionaryValue(row, columnName);
                                        }).ToArray();
                                    }, new MultiColumnComparer(false));
                                }
                                else
                                {
                                    orderedRows = rowData.OrderByDescending(row =>
                                    {
                                        return dedupColumnsList.Select(col =>
                                        {
                                            string columnName = originalToTransformedColumnNameMap[col.ToString()];
                                            return TextAndCsvHelper.GetDictionaryValue(row, columnName);
                                        }).ToArray();
                                    }, new MultiColumnComparer(true));



                                }




                                // Take only the first row after sorting (deduplication)
                                var deduplicatedRow = parameterConfig.KeepFirstRow ? orderedRows?.FirstOrDefault() : orderedRows?.LastOrDefault();
                                if (deduplicatedRow != null)
                                {
                                    deduplicatedDict[key] = deduplicatedRow;
                                }
                            }

                            // Now, deduplicatedDict contains each key with only one deduplicated row
                            // You can proceed to use deduplicatedDict as needed

                            foreach (var rowData in deduplicatedDict.Values)
                            {

                                for (int col = 0; col < rowData.Count; col++)
                                {
                                    //columns[col].Add(rowData[col]);
                                    rows.Add(rowData[col]);

                                }
                            }
                            // resultResponse.DuplicateRows = (uniqueRowsWithDedup.Values.Count() - deduplicatedDict.Values.Count());

                        }
                        foreach (var row in rows)
                        {
                            var rowDict = (IDictionary<string, object>)row;
                            for (int i = 0; i < headerRow.Count; i++)
                            {
                                var header = headerRow[i];
                                object value = null;
                                value = rowDict.ContainsKey(header) ? rowDict[header] : null;
                                columns[i].Add(value?.ToString() ?? string.Empty);
                            }

                        }
                        for (int i = 0; i < columns.Count; i++)
                        {
                            var dataField = fields[i];
                            if (i == 0)
                                resultResponse.InsertedRows = columns[0].Count;
                            FlpFileColumnMappingDto flpFileColumnMappingDto = fileColumnMapping.Values.FirstOrDefault(x => x.DbColumn == dataField.Name);
                            if (flpFileColumnMappingDto != null)
                            {

                                var dataTypeId = flpFileColumnMappingDto.DataTypeId;
                                var value = FlpConfigurationHelper.GetDateTypeValue(dataTypeId);
                                string format = "";
                                List<string> dateFormats = new List<string>();
                                if (dataTypeId == (int)DatatypeNamesEnum.DATE || dataTypeId == (int)DatatypeNamesEnum.DATETIME || dataTypeId == (int)DatatypeNamesEnum.TIME)
                                {
                                    var formatId = flpFileColumnMappingDto.FormatId;
                                    format = formats?.FirstOrDefault(fv => fv.formatId == formatId).apiFormat ?? "";
                                    if (string.IsNullOrWhiteSpace(format))
                                        throw new Exception("Format doesn't exit for configuration " + parameterConfig.FlpConfigurationId);
                                    if (!string.IsNullOrWhiteSpace(format))
                                    {
                                        //  dateFormats.Add(format);

                                        if (dataTypeId == (int)DatatypeNamesEnum.DATE)
                                        {
                                            //var formatParts = format.Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Length > 0 ? f[0].ToString() : f).ToList();
                                            //dateFormats = formats
                                            //    .Where(x => formatParts.Any() && x.apiFormat.StartsWith(formatParts[0]) && x.formatDataTypeId == (int)DatatypeNamesEnum.DATE)
                                            //    .Select(x => x.apiFormat).ToList();

                                            dateFormats = formats
                                              .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.DATE)
                                              .Select(x => x.apiFormat).ToList();


                                        }
                                        else if (dataTypeId == (int)DatatypeNamesEnum.DATETIME)
                                        {
                                            //var formatParts = format.Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Length > 0 ? f[0].ToString() : f).ToList(); // Take first character .ToArray();

                                            //dateFormats = formats
                                            //    .Where(x => formatParts.Any() && x.apiFormat.StartsWith(formatParts[0]) && x.formatDataTypeId == (int)DatatypeNamesEnum.DATETIME)
                                            //    .Select(x => x.apiFormat).ToList();

                                            dateFormats = formats
                                               .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.DATETIME)
                                               .Select(x => x.apiFormat).ToList();

                                        }
                                        else
                                        {
                                            dateFormats = formats
                                                .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.TIME)
                                                .Select(x => x.apiFormat).ToList();

                                        }
                                    }
                                }


                                var dataColumn = dataTypeId switch
                                {
                                    (int)DatatypeNamesEnum.INT => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        int intValue;
                                        return int.TryParse(x?.ToString(), out intValue) ? (int?)intValue : null;
                                    }).ToArray()),
                                    //(int)DatatypeNamesEnum.LONG => new DataColumn(dataField, columns[i].Select(x =>
                                    //{
                                    //    long longValue;
                                    //    return long.TryParse(x?.ToString(), out longValue) ? (long?)longValue : null;
                                    //}).ToArray()),
                                    (int)DatatypeNamesEnum.LONG => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        if (x == null) return (long?)null;

                                        double doubleValue;
                                        if (double.TryParse(x.ToString(), System.Globalization.NumberStyles.Float,
                                                            System.Globalization.CultureInfo.InvariantCulture, out doubleValue))
                                        {
                                            return (long?)doubleValue; // Convert double to long safely
                                        }

                                        return null;
                                    }).ToArray()),

                                    (int)DatatypeNamesEnum.FLOAT => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        float floatValue;
                                        return float.TryParse(x?.ToString(), out floatValue) ? (float?)floatValue : null;
                                    }).ToArray()),
                                    (int)DatatypeNamesEnum.DOUBLE => new DataColumn(dataField, columns[i].ToArray().Select(x =>
                                    {
                                        double doubleValue;
                                        return double.TryParse(x?.ToString(), out doubleValue) ? (double?)doubleValue : null;
                                    }).ToArray()),
                                    (int)DatatypeNamesEnum.BOOL => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        bool boolValue;
                                        return bool.TryParse(x?.ToString(), out boolValue) ? (bool?)boolValue : null;
                                    }).ToArray()),
                                    (int)DatatypeNamesEnum.DATETIME => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        DateTime dateTimeValue;
                                        return DateTime.TryParseExact(x?.ToString(), dateFormats.ToArray(),
                                               CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTimeValue)
                                               ? (DateTime?)dateTimeValue : null;
                                    }).ToArray()),
                                    (int)DatatypeNamesEnum.DATE => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        if (x == null || string.IsNullOrWhiteSpace(x.ToString()))
                                            return null;

                                        DateOnly dateOnlyValue;
                                        return DateOnly.TryParseExact(x?.ToString(), dateFormats.ToArray(),
                                               CultureInfo.InvariantCulture, DateTimeStyles.None, out dateOnlyValue)
                                               ? (DateOnly?)dateOnlyValue : null;
                                    }).ToArray()),
                                    (int)DatatypeNamesEnum.TIME => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        TimeOnly timeOnlyValue;
                                        return TimeOnly.TryParseExact(x?.ToString(), dateFormats.ToArray(),
                                               CultureInfo.InvariantCulture, DateTimeStyles.None, out timeOnlyValue)
                                               ? (TimeOnly?)timeOnlyValue : null;
                                    }).ToArray()),
                                    _ => new DataColumn(dataField, columns[i].Select(x => x?.ToString() ?? null).ToArray()), // default to string if the data type is unrecognized
                                };

                                await groupWriter.WriteColumnAsync(dataColumn);
                            }

                        }

                        resultResponse.ParquetFileCreated = true;
                    }
                }
            }
            return resultResponse;
        }




        private async Task<ParquetFileResponseDto> ConvertCsvToParquetAsyncV2(Stream txtStream, Stream parquetStream, ConfigurationTableMappingDto parameterConfig, FlpConfigurationResponseDto flpConfigurationResponseDto)
        {

            string errorMessage = "";
            CsvConfiguration csvConfig = null;
            var resultResponse = new ParquetFileResponseDto();
            resultResponse.ParquetFileCreated = false;
            IEnumerable<FlpFileColumnMappingDto> configurationFileColumns = null;
            try
            {
                //Retrieving  all columns first 
                bool skipDelimiter = false;
                configurationFileColumns = await _iValidateSchemaService.GetFileColumnList(parameterConfig.FlpConfigurationId);
                if ((string.Compare(parameterConfig.Delimiter, "none", true) == 0 && configurationFileColumns.Any() && configurationFileColumns.Count() == 1) || (configurationFileColumns.Any() && configurationFileColumns.Count() == 1))
                {
                    skipDelimiter = true;
                }
                if (string.Compare(parameterConfig.Delimiter, "\\t", true) == 0)
                {
                    parameterConfig.Delimiter = FlpConfigurationHelper.EscapedTabString(parameterConfig.Delimiter.Trim());
                }
                if (!skipDelimiter)
                {

                    var isValidFile = TextAndCsvHelper.IsValidDelimiter(txtStream, parameterConfig.Delimiter);
                    if (!isValidFile)
                    {
                        throw new InvalidOperationException("Unable to determine the CSV delimiter.");
                    }
                }



                List<string> columnNameList = new List<string>();
                bool createFileColumn = false;

                csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = parameterConfig.Delimiter,
                    Quote = Convert.ToChar(parameterConfig.QuoteCharacter),
                    IgnoreBlankLines = parameterConfig.SkipEmptyLines ? true : false,
                    HasHeaderRecord = parameterConfig.IsHeaderProvided,
                    BadDataFound = null,
                    TrimOptions = TrimOptions.Trim,
                    MissingFieldFound = null  //Ignores missing fields in some rows
                };
                // Read the remaining rows into a list

            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                await _iIFileProcessingService.AddFileProcessLosStatus(
                         fileType: flpConfigurationResponseDto.FileType,
                         loginId: "",
                         message: errorMessage,
                         messageType: "error",
                         processId: flpConfigurationResponseDto.ProcessId,
                         processName: flpConfigurationResponseDto.ProcessName,
                         tableName: parameterConfig.TableName,
                         totalRows: resultResponse.TotalRows,
                         flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                         fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                         FileStatusActivityEnum.Error,
                          FlpActivityLogStatusEnum.FileSchemaValidated
                     );
                resultResponse.flpActivityLogStatusEnum = FlpActivityLogStatusEnum.FileSchemaValidated;
                return resultResponse;
            }

            Encoding encoding = FlpConfigurationHelper.GetEncoding(); // ANSI/Windows-1252 for Spanish
            using (var reader = new StreamReader(txtStream, encoding))
            using (var csv = new CsvReader(reader, csvConfig))
            {
                bool hasDuplicateOrEmptyHeader = false;
                List<string> headerRow = new List<string>();
                List<string> filesHeaders = new List<string>();
                List<string> copyFilesHeaders = new List<string>();
                var formats = await _cache.GetFormatListAsync();
                resultResponse.ColumnDataTypeList = new Dictionary<string, DataTypeDetails>();
                var customHeadersColumnsMapping = new Dictionary<string, string>();
                Dictionary<string, string> originalToTransformedColumnNameMap = new Dictionary<string, string>();
                var dedupColumnsList = FlpConfigurationHelper.SplitString(parameterConfig.OrderByColumnListForDedup, ",");
                var fileColumnMapping = new Dictionary<string, FlpFileColumnMappingDto>();
                List<string> cleanFileColumns = new List<string>();
                //var (convertDatatypeString, columnDataTypeMappings) = FlpConfigurationHelper.GetColumnDataTypeList(parameterConfig.ConvertDataTypeColumnNameList);
                // Skip initial rows
                for (int i = 0; i < parameterConfig.SkipRows; i++)
                {
                    csv.Read();
                }

                try
                {

                    if (!string.IsNullOrWhiteSpace(parameterConfig.KeyColumnList))
                    {
                        parameterConfig.KeyColumnList = parameterConfig.KeyColumnList.ToLower();
                    }
                    (bool ret, string message) = FlpConfigurationHelper.checkSelectedValidOptions(parameterConfig, parameterConfig.KeyColumnList, parameterConfig.OrderByColumnListForDedup);
                    if (!ret)
                    {
                        throw new Exception(message);
                    }

                    if (parameterConfig.IsHeaderProvided)
                    {
                        csv.Read();
                        csv.ReadHeader();
                        var getFileHeaders = csv.Context.Reader.HeaderRecord.ToList();
                        filesHeaders = getFileHeaders;
                        //clean column which is not Empty Column
                        filesHeaders = filesHeaders.Select(x => !string.IsNullOrWhiteSpace(x) ? FlpConfigurationHelper.CleanColumnName(x) : x).ToList();
                        //conversion spanish
                        if (parameterConfig.SpanishToEnglish)
                        {

                            filesHeaders = await TextAndCsvHelper.ConvertSpanishToEnglish(filesHeaders, _iProcessConfigurationService);

                        }

                        //order to roman
                        if (parameterConfig.OrdinalToRoman)
                        {
                            filesHeaders = filesHeaders.Select(cell => !string.IsNullOrWhiteSpace(cell.ToString()) ? FlpConfigurationHelper.ConvertToRoman(cell.ToString()) : cell.ToString()).ToList();
                        }
                        //duplicate search
                        hasDuplicateOrEmptyHeader = FlpConfigurationHelper.ContainsDuplicatesOrEmptyHeader(filesHeaders);
                        if (hasDuplicateOrEmptyHeader)
                        {
                            filesHeaders = FlpConfigurationHelper.CreateCustomHeader(filesHeaders);

                            filesHeaders = filesHeaders.Select(x => FlpConfigurationHelper.CleanColumnName(x)).ToList();


                        }
                        else
                        {
                            filesHeaders = filesHeaders.Select(x => FlpConfigurationHelper.CleanColumnName(x)).ToList();
                        }


                        if (filesHeaders.Any())
                        {
                            copyFilesHeaders = new List<string>(filesHeaders);
                            bool exists = filesHeaders.Contains("divalidationrowno");
                            if (exists)
                            {
                                copyFilesHeaders.Remove("divalidationrowno");
                            }

                            cleanFileColumns = filesHeaders;//Use created custom header list
                            fileColumnMapping = await _iValidateSchemaService.ValidateFileSchema(parameterConfig.FlpConfigurationId, parameterConfig.ValidateFileSchema, parameterConfig.IsHeaderProvided, copyFilesHeaders, flpConfigurationResponseDto.ProcessName, parameterConfig.TableName, configurationFileColumns);
                            if (flpConfigurationResponseDto.UIValidation || flpConfigurationResponseDto.BEValidation)
                            {
                                var flpFileColumnMappingDto = FlpConfigurationHelperV4_1.AddRowNumberInColumnList();
                                fileColumnMapping.Add(flpFileColumnMappingDto.FileColumn, flpFileColumnMappingDto);

                            }
                            customHeadersColumnsMapping = FlpConfigurationHelper.MapHeadersToColumns(
                                                                                hasDuplicateOrEmptyHeader,
                                                                                filesHeaders,
                                                                                getFileHeaders,
                                                                                fileColumnMapping,
                                                                                mappingDto => mappingDto.DbColumn
                                                                            );

                            // If you need a list of headers:
                            headerRow = customHeadersColumnsMapping.Values.ToList();


                        }
                        else
                        {
                            throw new Exception("Unable to proceed file : column not cleaned");
                        }
                    }
                    else
                    {


                        if (flpConfigurationResponseDto.UIValidation || flpConfigurationResponseDto.BEValidation)
                        {

                            csv.Read();
                            var firstRow = csv.Parser.RawRecord.Split(parameterConfig.Delimiter);
                            var filesCustomHeaders = Enumerable.Range(1, firstRow.Length).Select(i => $"Field{i}").ToList();
                            if (filesCustomHeaders.Any())
                            {

                                //chang field to col
                                // Updated with "Col" in the list
                                var updatedHeaders = Enumerable.Range(0, firstRow.Length - 1).Select(i => $"col{i}").ToList();

                                //Check roman numbers -> file Header will converted into roman numbers
                                if (parameterConfig.OrdinalToRoman)
                                {
                                    updatedHeaders = updatedHeaders.Select(x => FlpConfigurationHelper.ConvertToRoman(x)).ToList();
                                }
                                cleanFileColumns = filesCustomHeaders;//Use created custom header list
                                fileColumnMapping = await _iValidateSchemaService.ValidateFileSchema(parameterConfig.FlpConfigurationId, parameterConfig.ValidateFileSchema, parameterConfig.IsHeaderProvided, updatedHeaders, flpConfigurationResponseDto.ProcessName, parameterConfig.TableName, configurationFileColumns);

                                if (flpConfigurationResponseDto.UIValidation || flpConfigurationResponseDto.BEValidation)
                                {
                                    var flpFileColumnMappingDto = FlpConfigurationHelperV4_1.AddRowNumberInColumnList();
                                    fileColumnMapping.Add(flpFileColumnMappingDto.FileColumn, flpFileColumnMappingDto);
                                    filesHeaders = fileColumnMapping.Values.Select(x => x.DbColumn).ToList();
                                    bool exists = updatedHeaders.Contains("divalidationrowno");
                                    if (!exists)
                                    {
                                        updatedHeaders.Add("divalidationrowno");
                                    }

                                }
                                customHeadersColumnsMapping = FlpConfigurationHelper.MapHeadersToColumns(
                                                                                       hasDuplicateOrEmptyHeader,
                                                                                       updatedHeaders,
                                                                                       filesCustomHeaders,
                                                                                       fileColumnMapping,
                                                                                       mappingDto => mappingDto.DbColumn);
                                // If you need a list of headers:
                                headerRow = customHeadersColumnsMapping.Values.ToList();
                                //filesHeaders = updatedHeaders;

                            }
                        }
                        else
                        {

                            csv.Read();
                            var firstRow = csv.Parser.RawRecord.Split(parameterConfig.Delimiter);
                            var filesCustomHeaders = Enumerable.Range(1, firstRow.Length).Select(i => $"Field{i}").ToList();
                            if (filesCustomHeaders.Any())
                            {

                                //chang field to col
                                // Updated with "Col" in the list
                                var updatedHeaders = Enumerable.Range(0, firstRow.Length).Select(i => $"col{i}").ToList();

                                //Check roman numbers -> file Header will converted into roman numbers
                                if (parameterConfig.OrdinalToRoman)
                                {
                                    updatedHeaders = updatedHeaders.Select(x => FlpConfigurationHelper.ConvertToRoman(x)).ToList();
                                }
                                cleanFileColumns = filesCustomHeaders;//Use created custom header list
                                fileColumnMapping = await _iValidateSchemaService.ValidateFileSchema(parameterConfig.FlpConfigurationId, parameterConfig.ValidateFileSchema, parameterConfig.IsHeaderProvided, updatedHeaders, flpConfigurationResponseDto.ProcessName, parameterConfig.TableName, configurationFileColumns);
                                customHeadersColumnsMapping = FlpConfigurationHelper.MapHeadersToColumns(
                                                                                       hasDuplicateOrEmptyHeader,
                                                                                       updatedHeaders,
                                                                                       filesCustomHeaders,
                                                                                       fileColumnMapping,
                                                                                       mappingDto => mappingDto.DbColumn);
                                // If you need a list of headers:
                                headerRow = customHeadersColumnsMapping.Values.ToList();

                            }
                        }

                    }
                    //custom header not  created 
                    if (headerRow != null && headerRow.Any() && headerRow.Count == customHeadersColumnsMapping.Keys.Count)
                    {
                        await _iIFileProcessingService.AddFileProcessLosStatus(
                            fileType: flpConfigurationResponseDto.FileType,
                            loginId: "",
                            message: $"File schema is validated",
                            messageType: "info",
                            processId: flpConfigurationResponseDto.ProcessId,
                            processName: flpConfigurationResponseDto.ProcessName,
                            tableName: parameterConfig.TableName,
                            totalRows: 0,
                            flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                            fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                            FileStatusActivityEnum.ProcessCompleted,
                            FlpActivityLogStatusEnum.FileSchemaValidated
                            );
                    }
                    else
                    {
                        errorMessage = headerRow != null && headerRow.Any() ? "file header does not match with existing database column" : "Not found file header";
                        await _iIFileProcessingService.AddFileProcessLosStatus(
                                 fileType: flpConfigurationResponseDto.FileType,
                                 loginId: "",
                                 message: errorMessage,
                                 messageType: "error",
                                 processId: flpConfigurationResponseDto.ProcessId,
                                 processName: flpConfigurationResponseDto.ProcessName,
                                 tableName: parameterConfig.TableName,
                                 totalRows: resultResponse.TotalRows,
                                 flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                                 fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                                 FileStatusActivityEnum.Error,
                                  FlpActivityLogStatusEnum.FileSchemaValidated
                             );
                        resultResponse.flpActivityLogStatusEnum = FlpActivityLogStatusEnum.FileSchemaValidated;
                        return resultResponse;
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                             fileType: flpConfigurationResponseDto.FileType,
                             loginId: "",
                             message: errorMessage,
                             messageType: "error",
                             processId: flpConfigurationResponseDto.ProcessId,
                             processName: flpConfigurationResponseDto.ProcessName,
                             tableName: parameterConfig.TableName,
                             totalRows: resultResponse.TotalRows,
                             flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                             fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                             FileStatusActivityEnum.Error,
                              FlpActivityLogStatusEnum.FileSchemaValidated
                         );
                    resultResponse.flpActivityLogStatusEnum = FlpActivityLogStatusEnum.FileSchemaValidated;
                    return resultResponse;
                }


                //Schema validation is completed...
                if (flpConfigurationResponseDto.UIValidation || flpConfigurationResponseDto.BEValidation)
                {
                    if (filesHeaders.Any())
                        await _processConfigurationRepositoryV4_1.AddFileHeaders(parameterConfig.FlpConfigurationId, flpConfigurationResponseDto.UploadedFileId, string.Join(",", filesHeaders), parameterConfig.TabName);
                }                   

                //Conversion is started
                await _iIFileProcessingService.AddFileProcessLosStatus(
                          fileType: flpConfigurationResponseDto.FileType,
                          loginId: "",
                          message: $"File conversion process in progress",
                          messageType: "info",
                          processId: flpConfigurationResponseDto.ProcessId,
                          processName: flpConfigurationResponseDto.ProcessName,
                          tableName: parameterConfig.TableName,//flpConfigurationRequestDto.TableName,
                          totalRows: 0,
                          flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                          fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                          FileStatusActivityEnum.Processing,
                          FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation);

                // Transform column names and map them                
                var fields = new DataField[headerRow.Count];
                for (int i = 0; i < headerRow.Count; i++)
                {
                    DataTypeDetails dataTypeDetails = new DataTypeDetails();
                    var header = headerRow[i];
                    string format = "";
                    var getHeaderDetails = headerRow[i];
                    FlpFileColumnMappingDto flpFileColumnMappingDto = fileColumnMapping.Values.FirstOrDefault(x => x.DbColumn == header);
                    if (flpFileColumnMappingDto != null)
                    {

                        var dataTypeId = flpFileColumnMappingDto.DataTypeId;
                        var value = FlpConfigurationHelper.GetDateTypeValue(dataTypeId);
                        dataTypeDetails.DataType = value;
                        dataTypeDetails.DataTypeFormat = string.Empty;
                        if (dataTypeId == (int)DatatypeNamesEnum.DATE || dataTypeId == (int)DatatypeNamesEnum.DATETIME || dataTypeId == (int)DatatypeNamesEnum.TIME)
                        {
                            var formatId = flpFileColumnMappingDto.FormatId;
                            format = formats?.FirstOrDefault(fv => fv.formatId == formatId).apiFormat ?? "";
                            if (string.IsNullOrWhiteSpace(format))
                                throw new Exception("Format doesn't exit for configuration " + parameterConfig.FlpConfigurationId);
                            dataTypeDetails.DataTypeFormat = format;
                        }
                        fields[i] = dataTypeId switch
                        {
                            (int)DatatypeNamesEnum.STRING => new DataField<string?>(header),
                            (int)DatatypeNamesEnum.INT => new DataField<int?>(header),
                            (int)DatatypeNamesEnum.LONG => new DataField<long?>(header),
                            (int)DatatypeNamesEnum.FLOAT => new DataField<float?>(header),
                            (int)DatatypeNamesEnum.DOUBLE => new DataField<double?>(header),
                            (int)DatatypeNamesEnum.BOOL => new DataField<bool?>(header),
                            (int)DatatypeNamesEnum.DATETIME => new DataField<DateTime?>(header),
                            (int)DatatypeNamesEnum.DATE => new DataField<DateOnly?>(header),
                            (int)DatatypeNamesEnum.TIME => new DataField<TimeOnly?>(header),
                            _ => new DataField<string?>(header), // default to string if the data type is unrecognized
                        };
                    }
                    resultResponse.ColumnDataTypeList[header] = dataTypeDetails;
                }

                var schema = new ParquetSchema(fields);

                using (var parquetWriter = await ParquetWriter.CreateAsync(schema, parquetStream))
                {
                    if (parameterConfig.ParquetCompression?.ToUpper() == "GZIP")
                    {
                        parquetWriter.CompressionMethod = CompressionMethod.Gzip;
                        parquetWriter.CompressionLevel = System.IO.Compression.CompressionLevel.Optimal;
                    }

                    using (ParquetRowGroupWriter groupWriter = parquetWriter.CreateRowGroup())
                    {
                        var columns = headerRow.Select(header => new List<object>()).ToList();
                        var rows = new List<dynamic>();
                        var uniqueKeys = new HashSet<string>();
                        var tempRows = new Dictionary<string, dynamic>();
                        var uniqueRowsWithDedup = new Dictionary<string, List<List<object>>>();
                        var keyColumnList = FlpConfigurationHelper.SplitString(parameterConfig.KeyColumnList, ",");
                        int incrementRowsCount = 0;
                        if (!parameterConfig.IsHeaderProvided && !string.IsNullOrWhiteSpace(parameterConfig.ColumnNameList))
                        {
                            (rows, uniqueKeys, tempRows) = TextAndCsvHelper.GetFirstRows(csv, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueKeys, tempRows, rows, keyColumnList);
                            incrementRowsCount++;
                        }
                        //Get all csv records into dynamic list
                        var csvRecords = new List<dynamic>();
                        if (hasDuplicateOrEmptyHeader)
                        {
                            while (csv.Read())
                            {
                                dynamic record = new ExpandoObject(); //Use ExpandoObject to create dynamic entries
                                var recordDict = (IDictionary<string, object>)record;
                                int i = 0;
                                foreach (var header in cleanFileColumns)
                                {
                                    //string cleanHeader = FlpConfigurationHelper.CleanColumnName(header);
                                    if (customHeadersColumnsMapping.TryGetValue(header, out var mappedValue) && !string.IsNullOrWhiteSpace(mappedValue))
                                    {
                                        recordDict[mappedValue] = csv.GetField(i); //Map the custom header to the value
                                    }
                                    //recordDict[kvp] = csv.GetField(i);
                                    i++;
                                }


                                if (parameterConfig.SkipEmptyLines)
                                {
                                    if (TextAndCsvHelper.IsEmptyLine(record))
                                    {
                                        continue;
                                    }
                                }
                                csvRecords.Add(record); //Add each dynamic record to the list
                            }
                        }
                        else
                        {
                            // csvRecords = csv.GetRecords<dynamic>().ToList();
                            foreach (var record in csv.GetRecords<dynamic>())
                            {
                                // Process record here, or add to batch for Parquet writing
                                csvRecords.Add(record); // Add each dynamic record to the list
                            }
                        }

                        int totalRecordsCount = csvRecords.Count;
                        // Step 3: Skip footer rows
                        int actualRecordsCount = totalRecordsCount - parameterConfig.SkipFooterRows;
                        resultResponse.TotalRows = actualRecordsCount + incrementRowsCount;

                        if (parameterConfig.IgnoreDuplicateRows)
                        {
                            foreach (var keyValuePair in customHeadersColumnsMapping)
                            {
                                originalToTransformedColumnNameMap.Add(FlpConfigurationHelper.CleanColumnName(keyValuePair.Key), keyValuePair.Value);
                            }
                        }

                        if (dedupColumnsList.Any())
                        {
                            rows = TextAndCsvHelper.GetRowsFromCsvReaderWithDedupColumn(csvRecords, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueRowsWithDedup, rows, actualRecordsCount, keyColumnList, resultResponse.ColumnDataTypeList);
                        }
                        else
                        {
                            rows = TextAndCsvHelper.GetRowsFromCsvReader(csvRecords, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueKeys, tempRows, rows, actualRecordsCount, keyColumnList);
                        }


                        //Order by descending & Ignore Duplicate Rows
                        if (parameterConfig.IgnoreDuplicateRows && !parameterConfig.KeepFirstRow && !dedupColumnsList.Any())
                        {
                            rows = tempRows.Values.ToList();
                            // resultResponse.DuplicateRows = (resultResponse.TotalRows - rows.Count);
                        }

                        //Dedup column opted and ignore duplicate rows

                        if (dedupColumnsList.Any())
                        {

                            // Initialize a new dictionary to store deduplicated rows with unique keys
                            var deduplicatedDict = new Dictionary<string, List<object>>();


                            foreach (var kvp in uniqueRowsWithDedup)
                            {
                                var key = kvp.Key;  // Get the unique key for the current entry
                                var rowData = kvp.Value;

                                IOrderedEnumerable<List<object>> orderedRows = null;
                                // Multi-column sorting without ConvertValue
                                if (parameterConfig.KeepFirstRow)
                                {
                                    orderedRows = rowData.OrderBy(row =>
                                    {
                                        return dedupColumnsList.Select(col =>
                                        {
                                            string columnName = originalToTransformedColumnNameMap[col.ToString()];
                                            return TextAndCsvHelper.GetDictionaryValue(row, columnName);
                                        }).ToArray();
                                    }, new MultiColumnComparer(false));
                                }
                                else
                                {
                                    orderedRows = rowData.OrderByDescending(row =>
                                    {
                                        return dedupColumnsList.Select(col =>
                                        {
                                            string columnName = originalToTransformedColumnNameMap[col.ToString()];
                                            return TextAndCsvHelper.GetDictionaryValue(row, columnName);
                                        }).ToArray();
                                    }, new MultiColumnComparer(true));



                                }




                                // Take only the first row after sorting (deduplication)
                                var deduplicatedRow = parameterConfig.KeepFirstRow ? orderedRows?.FirstOrDefault() : orderedRows?.LastOrDefault();
                                if (deduplicatedRow != null)
                                {
                                    deduplicatedDict[key] = deduplicatedRow;
                                }
                            }

                            // Now, deduplicatedDict contains each key with only one deduplicated row
                            // You can proceed to use deduplicatedDict as needed

                            foreach (var rowData in deduplicatedDict.Values)
                            {

                                for (int col = 0; col < rowData.Count; col++)
                                {
                                    //columns[col].Add(rowData[col]);
                                    rows.Add(rowData[col]);

                                }
                            }
                            // resultResponse.DuplicateRows = (uniqueRowsWithDedup.Values.Count() - deduplicatedDict.Values.Count());

                        }
                        foreach (var row in rows)
                        {
                            var rowDict = (IDictionary<string, object>)row;
                            for (int i = 0; i < headerRow.Count; i++)
                            {
                                var header = headerRow[i];
                                object value = null;
                                value = rowDict.ContainsKey(header) ? rowDict[header] : null;
                                columns[i].Add(value?.ToString() ?? string.Empty);
                            }

                        }
                        for (int i = 0; i < columns.Count; i++)
                        {
                            var dataField = fields[i];
                            if (i == 0)
                                resultResponse.InsertedRows = columns[0].Count;
                            FlpFileColumnMappingDto flpFileColumnMappingDto = fileColumnMapping.Values.FirstOrDefault(x => x.DbColumn == dataField.Name);
                            if (flpFileColumnMappingDto != null)
                            {

                                var dataTypeId = flpFileColumnMappingDto.DataTypeId;
                                var value = FlpConfigurationHelper.GetDateTypeValue(dataTypeId);
                                string format = "";
                                List<string> dateFormats = new List<string>();
                                if (dataTypeId == (int)DatatypeNamesEnum.DATE || dataTypeId == (int)DatatypeNamesEnum.DATETIME || dataTypeId == (int)DatatypeNamesEnum.TIME)
                                {
                                    var formatId = flpFileColumnMappingDto.FormatId;
                                    format = formats?.FirstOrDefault(fv => fv.formatId == formatId).apiFormat ?? "";
                                    if (string.IsNullOrWhiteSpace(format))
                                        throw new Exception("Format doesn't exit for configuration " + parameterConfig.FlpConfigurationId);
                                    if (!string.IsNullOrWhiteSpace(format))
                                    {
                                        //  dateFormats.Add(format);

                                        if (dataTypeId == (int)DatatypeNamesEnum.DATE)
                                        {
                                            //var formatParts = format.Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Length > 0 ? f[0].ToString() : f).ToList();
                                            //dateFormats = formats
                                            //    .Where(x => formatParts.Any() && x.apiFormat.StartsWith(formatParts[0]) && x.formatDataTypeId == (int)DatatypeNamesEnum.DATE)
                                            //    .Select(x => x.apiFormat).ToList();

                                            dateFormats = formats
                                              .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.DATE)
                                              .Select(x => x.apiFormat).ToList();


                                        }
                                        else if (dataTypeId == (int)DatatypeNamesEnum.DATETIME)
                                        {
                                            //var formatParts = format.Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Length > 0 ? f[0].ToString() : f).ToList(); // Take first character .ToArray();

                                            //dateFormats = formats
                                            //    .Where(x => formatParts.Any() && x.apiFormat.StartsWith(formatParts[0]) && x.formatDataTypeId == (int)DatatypeNamesEnum.DATETIME)
                                            //    .Select(x => x.apiFormat).ToList();

                                            dateFormats = formats
                                               .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.DATETIME)
                                               .Select(x => x.apiFormat).ToList();

                                        }
                                        else
                                        {
                                            dateFormats = formats
                                                .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.TIME)
                                                .Select(x => x.apiFormat).ToList();

                                        }
                                    }
                                }


                                var dataColumn = dataTypeId switch
                                {
                                    (int)DatatypeNamesEnum.INT => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        int intValue;
                                        return int.TryParse(x?.ToString(), out intValue) ? (int?)intValue : null;
                                    }).ToArray()),
                                    //(int)DatatypeNamesEnum.LONG => new DataColumn(dataField, columns[i].Select(x =>
                                    //{
                                    //    long longValue;
                                    //    return long.TryParse(x?.ToString(), out longValue) ? (long?)longValue : null;
                                    //}).ToArray()),
                                    (int)DatatypeNamesEnum.LONG => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        if (x == null) return (long?)null;

                                        double doubleValue;
                                        if (double.TryParse(x.ToString(), System.Globalization.NumberStyles.Float,
                                                            System.Globalization.CultureInfo.InvariantCulture, out doubleValue))
                                        {
                                            return (long?)doubleValue; // Convert double to long safely
                                        }

                                        return null;
                                    }).ToArray()),

                                    (int)DatatypeNamesEnum.FLOAT => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        float floatValue;
                                        return float.TryParse(x?.ToString(), out floatValue) ? (float?)floatValue : null;
                                    }).ToArray()),
                                    (int)DatatypeNamesEnum.DOUBLE => new DataColumn(dataField, columns[i].ToArray().Select(x =>
                                    {
                                        double doubleValue;
                                        return double.TryParse(x?.ToString(), out doubleValue) ? (double?)doubleValue : null;
                                    }).ToArray()),
                                    (int)DatatypeNamesEnum.BOOL => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        bool boolValue;
                                        return bool.TryParse(x?.ToString(), out boolValue) ? (bool?)boolValue : null;
                                    }).ToArray()),
                                    (int)DatatypeNamesEnum.DATETIME => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        DateTime dateTimeValue;
                                        return DateTime.TryParseExact(x?.ToString(), dateFormats.ToArray(),
                                               CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTimeValue)
                                               ? (DateTime?)dateTimeValue : null;
                                    }).ToArray()),
                                    (int)DatatypeNamesEnum.DATE => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        if (x == null || string.IsNullOrWhiteSpace(x.ToString()))
                                            return null;

                                        DateOnly dateOnlyValue;
                                        return DateOnly.TryParseExact(x?.ToString(), dateFormats.ToArray(),
                                               CultureInfo.InvariantCulture, DateTimeStyles.None, out dateOnlyValue)
                                               ? (DateOnly?)dateOnlyValue : null;
                                    }).ToArray()),
                                    (int)DatatypeNamesEnum.TIME => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        TimeOnly timeOnlyValue;
                                        return TimeOnly.TryParseExact(x?.ToString(), dateFormats.ToArray(),
                                               CultureInfo.InvariantCulture, DateTimeStyles.None, out timeOnlyValue)
                                               ? (TimeOnly?)timeOnlyValue : null;
                                    }).ToArray()),
                                    _ => new DataColumn(dataField, columns[i].Select(x => x?.ToString() ?? null).ToArray()), // default to string if the data type is unrecognized
                                };

                                await groupWriter.WriteColumnAsync(dataColumn);
                            }

                        }

                        resultResponse.ParquetFileCreated = true;
                    }
                }
            }
            return resultResponse;
        }




        private async Task<ParquetFileResponseDto> ConvertCsvToParquetAsyncV3(Stream txtStream, Stream parquetStream, ConfigurationTableMappingDto parameterConfig, FlpConfigurationResponseDto flpConfigurationResponseDto)
        {

            string errorMessage = "";
            CsvConfiguration csvConfig = null;
            var resultResponse = new ParquetFileResponseDto();
            resultResponse.ParquetFileCreated = false;
            IEnumerable<FlpFileColumnMappingDto> configurationFileColumns = null;
            try
            {
                
                //Retrieving  all columns first 
                bool skipDelimiter = false;
                configurationFileColumns = await _iValidateSchemaService.GetFileColumnList(parameterConfig.FlpConfigurationId);
                if ((string.Compare(parameterConfig.Delimiter, "none", true) == 0 && configurationFileColumns.Any() && configurationFileColumns.Count() == 1) || (configurationFileColumns.Any() && configurationFileColumns.Count() == 1))
                {
                    skipDelimiter = true;
                }
                if (string.Compare(parameterConfig.Delimiter, "\\t", true) == 0)
                {
                    parameterConfig.Delimiter = FlpConfigurationHelper.EscapedTabString(parameterConfig.Delimiter.Trim());
                }
                if (!skipDelimiter)
                {

                    var isValidFile = TextAndCsvHelper.IsValidDelimiter(txtStream, parameterConfig.Delimiter);
                    if (!isValidFile)
                    {
                        throw new InvalidOperationException("Unable to determine the CSV delimiter.");
                    }
                }



                List<string> columnNameList = new List<string>();
                bool createFileColumn = false;

                csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = parameterConfig.Delimiter,
                    Quote = Convert.ToChar(parameterConfig.QuoteCharacter),
                    IgnoreBlankLines = parameterConfig.SkipEmptyLines ? true : false,
                    HasHeaderRecord = parameterConfig.IsHeaderProvided,
                    BadDataFound = null,
                    TrimOptions = TrimOptions.Trim,
                    MissingFieldFound = null  //Ignores missing fields in some rows
                };
                // Read the remaining rows into a list

            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                await _iIFileProcessingService.AddFileProcessLosStatus(
                         fileType: flpConfigurationResponseDto.FileType,
                         loginId: "",
                         message: errorMessage,
                         messageType: "error",
                         processId: flpConfigurationResponseDto.ProcessId,
                         processName: flpConfigurationResponseDto.ProcessName,
                         tableName: parameterConfig.TableName,
                         totalRows: resultResponse.TotalRows,
                         flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                         fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                         FileStatusActivityEnum.Error,
                          FlpActivityLogStatusEnum.FileSchemaValidated
                     );
                resultResponse.flpActivityLogStatusEnum = FlpActivityLogStatusEnum.FileSchemaValidated;
                return resultResponse;
            }

            Encoding encoding = FlpConfigurationHelper.GetEncoding(); // ANSI/Windows-1252 for Spanish
            using (var reader = new StreamReader(txtStream, encoding))
            using (var csv = new CsvReader(reader, csvConfig))
            {
                bool hasDuplicateOrEmptyHeader = false;
                List<string> headerRow = new List<string>();
                List<string> filesHeaders = new List<string>();
                var formats = await _cache.GetFormatListAsync();
                resultResponse.ColumnDataTypeList = new Dictionary<string, DataTypeDetails>();
                var customHeadersColumnsMapping = new Dictionary<string, string>();
                Dictionary<string, string> originalToTransformedColumnNameMap = new Dictionary<string, string>();
                var dedupColumnsList = FlpConfigurationHelper.SplitString(parameterConfig.OrderByColumnListForDedup, ",");
                var fileColumnMapping = new Dictionary<string, FlpFileColumnMappingDto>();
                List<string> cleanFileColumns = new List<string>();
                //var (convertDatatypeString, columnDataTypeMappings) = FlpConfigurationHelper.GetColumnDataTypeList(parameterConfig.ConvertDataTypeColumnNameList);
                // Skip initial rows
                for (int i = 0; i < parameterConfig.SkipRows; i++)
                {
                    csv.Read();
                }

                try
                {

                    if (!string.IsNullOrWhiteSpace(parameterConfig.KeyColumnList))
                    {
                        parameterConfig.KeyColumnList = parameterConfig.KeyColumnList.ToLower();
                    }
                    (bool ret, string message) = FlpConfigurationHelper.checkSelectedValidOptions(parameterConfig, parameterConfig.KeyColumnList, parameterConfig.OrderByColumnListForDedup);
                    if (!ret)
                    {
                        throw new Exception(message);
                    }

                    if (parameterConfig.IsHeaderProvided)
                    {
                        csv.Read();
                        csv.ReadHeader();
                        var getFileHeaders = csv.Context.Reader.HeaderRecord.ToList();
                        filesHeaders = getFileHeaders;
                        //clean column which is not Empty Column
                        filesHeaders = filesHeaders.Select(x => !string.IsNullOrWhiteSpace(x) ? FlpConfigurationHelper.CleanColumnName(x) : x).ToList();
                        //conversion spanish
                        if (parameterConfig.SpanishToEnglish)
                        {

                            filesHeaders = await TextAndCsvHelper.ConvertSpanishToEnglish(filesHeaders, _iProcessConfigurationService);

                        }

                        //order to roman
                        if (parameterConfig.OrdinalToRoman)
                        {
                            filesHeaders = filesHeaders.Select(cell => !string.IsNullOrWhiteSpace(cell.ToString()) ? FlpConfigurationHelper.ConvertToRoman(cell.ToString()) : cell.ToString()).ToList();
                        }
                        //duplicate search
                        hasDuplicateOrEmptyHeader = FlpConfigurationHelper.ContainsDuplicatesOrEmptyHeader(filesHeaders);
                        if (hasDuplicateOrEmptyHeader)
                        {
                            filesHeaders = FlpConfigurationHelper.CreateCustomHeader(filesHeaders);

                            filesHeaders = filesHeaders.Select(x => FlpConfigurationHelper.CleanColumnName(x)).ToList();


                        }
                        else
                        {
                            filesHeaders = filesHeaders.Select(x => FlpConfigurationHelper.CleanColumnName(x)).ToList();
                        }


                        if (filesHeaders.Any())
                        {


                            cleanFileColumns = filesHeaders;//Use created custom header list
                            fileColumnMapping = await _iValidateSchemaService.ValidateFileSchema(parameterConfig.FlpConfigurationId, parameterConfig.ValidateFileSchema, parameterConfig.IsHeaderProvided, filesHeaders, flpConfigurationResponseDto.ProcessName, parameterConfig.TableName, configurationFileColumns);

                            customHeadersColumnsMapping = FlpConfigurationHelper.MapHeadersToColumns(
                                                                                hasDuplicateOrEmptyHeader,
                                                                                filesHeaders,
                                                                                getFileHeaders,
                                                                                fileColumnMapping,
                                                                                mappingDto => mappingDto.DbColumn
                                                                            );

                            // If you need a list of headers:
                            headerRow = customHeadersColumnsMapping.Values.ToList();


                        }
                        else
                        {
                            throw new Exception("Unable to proceed file : column not cleaned");
                        }
                    }
                    else
                    {


                        if (flpConfigurationResponseDto.UIValidation || flpConfigurationResponseDto.BEValidation)
                        {

                            csv.Read();
                            var firstRow = csv.Parser.RawRecord.Split(parameterConfig.Delimiter);
                            var filesCustomHeaders = Enumerable.Range(1, firstRow.Length).Select(i => $"Field{i}").ToList();
                            if (filesCustomHeaders.Any())
                            {

                                //chang field to col
                                // Updated with "Col" in the list
                                var updatedHeaders = Enumerable.Range(0, firstRow.Length - 1).Select(i => $"col{i}").ToList();

                                //Check roman numbers -> file Header will converted into roman numbers
                                if (parameterConfig.OrdinalToRoman)
                                {
                                    updatedHeaders = updatedHeaders.Select(x => FlpConfigurationHelper.ConvertToRoman(x)).ToList();
                                }
                                cleanFileColumns = filesCustomHeaders;//Use created custom header list
                                fileColumnMapping = await _iValidateSchemaService.ValidateFileSchema(parameterConfig.FlpConfigurationId, parameterConfig.ValidateFileSchema, parameterConfig.IsHeaderProvided, updatedHeaders, flpConfigurationResponseDto.ProcessName, parameterConfig.TableName, configurationFileColumns);

                                if (flpConfigurationResponseDto.UIValidation || flpConfigurationResponseDto.BEValidation)
                                {
                                    var flpFileColumnMappingDto = FlpConfigurationHelperV4_1.AddRowNumberInColumnList();
                                    fileColumnMapping.Add(flpFileColumnMappingDto.FileColumn, flpFileColumnMappingDto);
                                    bool exists = updatedHeaders.Contains("divalidationrowno");
                                    if (!exists)
                                    {
                                        updatedHeaders.Add("divalidationrowno");
                                    }

                                }
                                customHeadersColumnsMapping = FlpConfigurationHelper.MapHeadersToColumns(
                                                                                       hasDuplicateOrEmptyHeader,
                                                                                       updatedHeaders,
                                                                                       filesCustomHeaders,
                                                                                       fileColumnMapping,
                                                                                       mappingDto => mappingDto.DbColumn);
                                // If you need a list of headers:
                                headerRow = customHeadersColumnsMapping.Values.ToList();
                                filesHeaders = updatedHeaders;

                            }
                        }
                        else
                        {

                            csv.Read();
                            var firstRow = csv.Parser.RawRecord.Split(parameterConfig.Delimiter);
                            var filesCustomHeaders = Enumerable.Range(1, firstRow.Length).Select(i => $"Field{i}").ToList();
                            if (filesCustomHeaders.Any())
                            {

                                //chang field to col
                                // Updated with "Col" in the list
                                var updatedHeaders = Enumerable.Range(0, firstRow.Length).Select(i => $"col{i}").ToList();

                                //Check roman numbers -> file Header will converted into roman numbers
                                if (parameterConfig.OrdinalToRoman)
                                {
                                    updatedHeaders = updatedHeaders.Select(x => FlpConfigurationHelper.ConvertToRoman(x)).ToList();
                                }
                                cleanFileColumns = filesCustomHeaders;//Use created custom header list
                                fileColumnMapping = await _iValidateSchemaService.ValidateFileSchema(parameterConfig.FlpConfigurationId, parameterConfig.ValidateFileSchema, parameterConfig.IsHeaderProvided, updatedHeaders, flpConfigurationResponseDto.ProcessName, parameterConfig.TableName, configurationFileColumns);
                                customHeadersColumnsMapping = FlpConfigurationHelper.MapHeadersToColumns(
                                                                                       hasDuplicateOrEmptyHeader,
                                                                                       updatedHeaders,
                                                                                       filesCustomHeaders,
                                                                                       fileColumnMapping,
                                                                                       mappingDto => mappingDto.DbColumn);
                                // If you need a list of headers:
                                headerRow = customHeadersColumnsMapping.Values.ToList();

                            }
                        }

                    }
                    //custom header not  created 
                    if (headerRow != null && headerRow.Any() && headerRow.Count == customHeadersColumnsMapping.Keys.Count)
                    {
                        await _iIFileProcessingService.AddFileProcessLosStatus(
                            fileType: flpConfigurationResponseDto.FileType,
                            loginId: "",
                            message: $"File schema is validated",
                            messageType: "info",
                            processId: flpConfigurationResponseDto.ProcessId,
                            processName: flpConfigurationResponseDto.ProcessName,
                            tableName: parameterConfig.TableName,
                            totalRows: 0,
                            flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                            fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                            FileStatusActivityEnum.ProcessCompleted,
                            FlpActivityLogStatusEnum.FileSchemaValidated
                            );
                    }
                    else
                    {
                        errorMessage = headerRow != null && headerRow.Any() ? "file header does not match with existing database column" : "Not found file header";
                        await _iIFileProcessingService.AddFileProcessLosStatus(
                                 fileType: flpConfigurationResponseDto.FileType,
                                 loginId: "",
                                 message: errorMessage,
                                 messageType: "error",
                                 processId: flpConfigurationResponseDto.ProcessId,
                                 processName: flpConfigurationResponseDto.ProcessName,
                                 tableName: parameterConfig.TableName,
                                 totalRows: resultResponse.TotalRows,
                                 flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                                 fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                                 FileStatusActivityEnum.Error,
                                  FlpActivityLogStatusEnum.FileSchemaValidated
                             );
                        resultResponse.flpActivityLogStatusEnum = FlpActivityLogStatusEnum.FileSchemaValidated;
                        return resultResponse;
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                             fileType: flpConfigurationResponseDto.FileType,
                             loginId: "",
                             message: errorMessage,
                             messageType: "error",
                             processId: flpConfigurationResponseDto.ProcessId,
                             processName: flpConfigurationResponseDto.ProcessName,
                             tableName: parameterConfig.TableName,
                             totalRows: resultResponse.TotalRows,
                             flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                             fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                             FileStatusActivityEnum.Error,
                              FlpActivityLogStatusEnum.FileSchemaValidated
                         );
                    resultResponse.flpActivityLogStatusEnum = FlpActivityLogStatusEnum.FileSchemaValidated;
                    return resultResponse;
                }

                //Schema validation is completed...

                //Conversion is started
                await _iIFileProcessingService.AddFileProcessLosStatus(
                          fileType: flpConfigurationResponseDto.FileType,
                          loginId: "",
                          message: $"File conversion process in progress",
                          messageType: "info",
                          processId: flpConfigurationResponseDto.ProcessId,
                          processName: flpConfigurationResponseDto.ProcessName,
                          tableName: parameterConfig.TableName,//flpConfigurationRequestDto.TableName,
                          totalRows: 0,
                          flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                          fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                          FileStatusActivityEnum.Processing,
                          FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation);

                // Transform column names and map them                
                var fields = new DataField[headerRow.Count];
                for (int i = 0; i < headerRow.Count; i++)
                {
                    DataTypeDetails dataTypeDetails = new DataTypeDetails();
                    var header = headerRow[i];
                    string format = "";
                    var getHeaderDetails = headerRow[i];
                    FlpFileColumnMappingDto flpFileColumnMappingDto = fileColumnMapping.Values.FirstOrDefault(x => x.DbColumn == header);
                    if (flpFileColumnMappingDto != null)
                    {

                        var dataTypeId = flpFileColumnMappingDto.DataTypeId;
                        var value = FlpConfigurationHelper.GetDateTypeValue(dataTypeId);
                        dataTypeDetails.DataType = value;
                        dataTypeDetails.DataTypeFormat = string.Empty;
                        if (dataTypeId == (int)DatatypeNamesEnum.DATE || dataTypeId == (int)DatatypeNamesEnum.DATETIME || dataTypeId == (int)DatatypeNamesEnum.TIME)
                        {
                            var formatId = flpFileColumnMappingDto.FormatId;
                            format = formats?.FirstOrDefault(fv => fv.formatId == formatId).apiFormat ?? "";
                            if (string.IsNullOrWhiteSpace(format))
                                throw new Exception("Format doesn't exit for configuration " + parameterConfig.FlpConfigurationId);
                            dataTypeDetails.DataTypeFormat = format;
                        }
                        fields[i] = dataTypeId switch
                        {
                            (int)DatatypeNamesEnum.STRING => new DataField<string?>(header),
                            (int)DatatypeNamesEnum.INT => new DataField<int?>(header),
                            (int)DatatypeNamesEnum.LONG => new DataField<long?>(header),
                            (int)DatatypeNamesEnum.FLOAT => new DataField<float?>(header),
                            (int)DatatypeNamesEnum.DOUBLE => new DataField<double?>(header),
                            (int)DatatypeNamesEnum.BOOL => new DataField<bool?>(header),
                            (int)DatatypeNamesEnum.DATETIME => new DataField<DateTime?>(header),
                            (int)DatatypeNamesEnum.DATE => new DataField<DateOnly?>(header),
                            (int)DatatypeNamesEnum.TIME => new DataField<TimeOnly?>(header),
                            _ => new DataField<string?>(header), // default to string if the data type is unrecognized
                        };
                    }
                    resultResponse.ColumnDataTypeList[header] = dataTypeDetails;
                }

                var schema = new ParquetSchema(fields);

                using (var parquetWriter = await ParquetWriter.CreateAsync(schema, parquetStream))
                {
                    if (parameterConfig.ParquetCompression?.ToUpper() == "GZIP")
                    {
                        parquetWriter.CompressionMethod = CompressionMethod.Gzip;
                        parquetWriter.CompressionLevel = System.IO.Compression.CompressionLevel.Optimal;
                    }

                    using (ParquetRowGroupWriter groupWriter = parquetWriter.CreateRowGroup())
                    {
                        int totalRecordsCount = 0;
                        var columns = headerRow.Select(header => new List<object>()).ToList();
                        var rows = new List<dynamic>();
                        var uniqueKeys = new HashSet<string>();
                        var tempRows = new Dictionary<string, dynamic>();
                        var uniqueRowsWithDedup = new Dictionary<string, List<List<object>>>();
                        var keyColumnList = FlpConfigurationHelper.SplitString(parameterConfig.KeyColumnList, ",");
                        int incrementRowsCount = 0;
                        if (!parameterConfig.IsHeaderProvided && !string.IsNullOrWhiteSpace(parameterConfig.ColumnNameList))
                        {
                            (rows, uniqueKeys, tempRows) = TextAndCsvHelper.GetFirstRows(csv, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueKeys, tempRows, rows, keyColumnList);
                            incrementRowsCount++;
                        }
                        // csvRecords = csv.GetRecords<dynamic>().ToList();
                        if (parameterConfig.IgnoreDuplicateRows)
                        {
                            foreach (var keyValuePair in customHeadersColumnsMapping)
                            {
                                originalToTransformedColumnNameMap.Add(FlpConfigurationHelper.CleanColumnName(keyValuePair.Key), keyValuePair.Value);
                            }
                        }


                        //Get all csv records into dynamic list
                        var csvRecords = new List<dynamic>();
                        var batchSize = 100000; // Define the batch size
                        var batch = new List<dynamic>();
                        var rows1 = new List<dynamic>();
                        if (hasDuplicateOrEmptyHeader)
                        {
                            while (csv.Read())
                            {
                                dynamic record = new ExpandoObject(); //Use ExpandoObject to create dynamic entries
                                var recordDict = (IDictionary<string, object>)record;
                                int i = 0;
                                foreach (var header in cleanFileColumns)
                                {
                                    if (customHeadersColumnsMapping.TryGetValue(header, out var mappedValue) && !string.IsNullOrWhiteSpace(mappedValue))
                                    {
                                        recordDict[mappedValue] = csv.GetField(i); //Map the custom header to the value
                                    }
                                    i++;
                                }
                                if (parameterConfig.SkipEmptyLines)
                                {
                                    if (TextAndCsvHelper.IsEmptyLine(record))
                                    {
                                        continue;
                                    }
                                }
                                csvRecords.Add(record); //Add each dynamic record to the list
                            }
                            totalRecordsCount = csvRecords.Count;                          

                        }
                        else
                        {                          
                           
                            while (csv.Read())
                            {
                                dynamic record = csv.GetRecord<dynamic>();
                                batch.Add(record);

                                if (batch.Count >= batchSize)
                                {
                                    if (dedupColumnsList.Any())
                                    {
                                        (rows, uniqueRowsWithDedup) = TextAndCsvHelper.GetRowsFromCsvReaderWithDedupColumnV2(batch, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueRowsWithDedup, keyColumnList, resultResponse.ColumnDataTypeList);
                                    }
                                    else
                                    {
                                        rows1 = new List<dynamic>();
                                        (rows1, uniqueKeys,tempRows) = TextAndCsvHelper.GetRowsFromCsvReaderV2(batch, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueKeys, tempRows, keyColumnList);
                                        if (rows1.Any())
                                        {
                                            rows.AddRange(rows1);
                                        }
                                    }

                                    // Clear the batch to free memory
                                    batch.Clear();
                                    totalRecordsCount += batch.Count;
                                }
                            }

                            // Process any remaining records in the batch
                            if (batch.Count > 0)
                            {
                                if (dedupColumnsList.Any())
                                {
                                    (rows, uniqueRowsWithDedup) = TextAndCsvHelper.GetRowsFromCsvReaderWithDedupColumnV2(batch, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueRowsWithDedup, keyColumnList, resultResponse.ColumnDataTypeList);
                                }
                                else
                                {
                                    rows1 = new List<dynamic>();
                                    (rows1, uniqueKeys, tempRows) = TextAndCsvHelper.GetRowsFromCsvReaderV2(batch, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueKeys, tempRows,  keyColumnList);
                                    if(rows1.Any())
                                    {
                                        rows.AddRange(rows1);
                                    }
                                }
                                totalRecordsCount += batch.Count;
                            }
                        }

                        //int totalRecordsCount = csvRecords.Count;
                        // Step 3: Skip footer rows
                        int actualRecordsCount = totalRecordsCount - parameterConfig.SkipFooterRows;
                        resultResponse.TotalRows = actualRecordsCount + incrementRowsCount;

                        //if (parameterConfig.IgnoreDuplicateRows)
                        //{
                        //    foreach (var keyValuePair in customHeadersColumnsMapping)
                        //    {
                        //        originalToTransformedColumnNameMap.Add(FlpConfigurationHelper.CleanColumnName(keyValuePair.Key), keyValuePair.Value);
                        //    }
                        //}

                        //if (dedupColumnsList.Any())
                        //{
                        //    rows = TextAndCsvHelper.GetRowsFromCsvReaderWithDedupColumn(csvRecords, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueRowsWithDedup, rows, actualRecordsCount, keyColumnList, resultResponse.ColumnDataTypeList);
                        //}
                        //else
                        //{
                        //    rows = TextAndCsvHelper.GetRowsFromCsvReader(csvRecords, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueKeys, tempRows, rows, actualRecordsCount, keyColumnList);
                        //}


                        //Order by descending & Ignore Duplicate Rows
                        if (parameterConfig.IgnoreDuplicateRows && !parameterConfig.KeepFirstRow && !dedupColumnsList.Any())
                        {
                            rows = tempRows.Values.ToList();
                            // resultResponse.DuplicateRows = (resultResponse.TotalRows - rows.Count);
                        }

                        //Dedup column opted and ignore duplicate rows

                        if (dedupColumnsList.Any())
                        {

                            // Initialize a new dictionary to store deduplicated rows with unique keys
                            var deduplicatedDict = new Dictionary<string, List<object>>();


                            foreach (var kvp in uniqueRowsWithDedup)
                            {
                                var key = kvp.Key;  // Get the unique key for the current entry
                                var rowData = kvp.Value;

                                IOrderedEnumerable<List<object>> orderedRows = null;
                                // Multi-column sorting without ConvertValue
                                if (parameterConfig.KeepFirstRow)
                                {
                                    orderedRows = rowData.OrderBy(row =>
                                    {
                                        return dedupColumnsList.Select(col =>
                                        {
                                            string columnName = originalToTransformedColumnNameMap[col.ToString()];
                                            return TextAndCsvHelper.GetDictionaryValue(row, columnName);
                                        }).ToArray();
                                    }, new MultiColumnComparer(false));
                                }
                                else
                                {
                                    orderedRows = rowData.OrderByDescending(row =>
                                    {
                                        return dedupColumnsList.Select(col =>
                                        {
                                            string columnName = originalToTransformedColumnNameMap[col.ToString()];
                                            return TextAndCsvHelper.GetDictionaryValue(row, columnName);
                                        }).ToArray();
                                    }, new MultiColumnComparer(true));



                                }




                                // Take only the first row after sorting (deduplication)
                                var deduplicatedRow = parameterConfig.KeepFirstRow ? orderedRows?.FirstOrDefault() : orderedRows?.LastOrDefault();
                                if (deduplicatedRow != null)
                                {
                                    deduplicatedDict[key] = deduplicatedRow;
                                }
                            }

                            // Now, deduplicatedDict contains each key with only one deduplicated row
                            // You can proceed to use deduplicatedDict as needed

                            foreach (var rowData in deduplicatedDict.Values)
                            {

                                for (int col = 0; col < rowData.Count; col++)
                                {
                                    //columns[col].Add(rowData[col]);
                                    rows.Add(rowData[col]);

                                }
                            }
                            // resultResponse.DuplicateRows = (uniqueRowsWithDedup.Values.Count() - deduplicatedDict.Values.Count());

                        }
                        foreach (var row in rows)
                        {
                            var rowDict = (IDictionary<string, object>)row;
                            for (int i = 0; i < headerRow.Count; i++)
                            {
                                var header = headerRow[i];
                                object value = null;
                                value = rowDict.ContainsKey(header) ? rowDict[header] : null;
                                columns[i].Add(value?.ToString() ?? string.Empty);
                            }

                        }
                        for (int i = 0; i < columns.Count; i++)
                        {
                            var dataField = fields[i];
                            FlpFileColumnMappingDto flpFileColumnMappingDto = fileColumnMapping.Values.FirstOrDefault(x => x.DbColumn == dataField.Name);
                            if (flpFileColumnMappingDto != null)
                            {

                                var dataTypeId = flpFileColumnMappingDto.DataTypeId;
                                var value = FlpConfigurationHelper.GetDateTypeValue(dataTypeId);
                                string format = "";
                                List<string> dateFormats = new List<string>();
                                if (dataTypeId == (int)DatatypeNamesEnum.DATE || dataTypeId == (int)DatatypeNamesEnum.DATETIME || dataTypeId == (int)DatatypeNamesEnum.TIME)
                                {
                                    var formatId = flpFileColumnMappingDto.FormatId;
                                    format = formats?.FirstOrDefault(fv => fv.formatId == formatId).apiFormat ?? "";
                                    if (string.IsNullOrWhiteSpace(format))
                                        throw new Exception("Format doesn't exit for configuration " + parameterConfig.FlpConfigurationId);
                                    if (!string.IsNullOrWhiteSpace(format))
                                    {
                                        //  dateFormats.Add(format);

                                        if (dataTypeId == (int)DatatypeNamesEnum.DATE)
                                        {
                                            //var formatParts = format.Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Length > 0 ? f[0].ToString() : f).ToList();
                                            //dateFormats = formats
                                            //    .Where(x => formatParts.Any() && x.apiFormat.StartsWith(formatParts[0]) && x.formatDataTypeId == (int)DatatypeNamesEnum.DATE)
                                            //    .Select(x => x.apiFormat).ToList();

                                            dateFormats = formats
                                              .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.DATE)
                                              .Select(x => x.apiFormat).ToList();


                                        }
                                        else if (dataTypeId == (int)DatatypeNamesEnum.DATETIME)
                                        {
                                            //var formatParts = format.Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Length > 0 ? f[0].ToString() : f).ToList(); // Take first character .ToArray();

                                            //dateFormats = formats
                                            //    .Where(x => formatParts.Any() && x.apiFormat.StartsWith(formatParts[0]) && x.formatDataTypeId == (int)DatatypeNamesEnum.DATETIME)
                                            //    .Select(x => x.apiFormat).ToList();

                                            dateFormats = formats
                                               .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.DATETIME)
                                               .Select(x => x.apiFormat).ToList();

                                        }
                                        else
                                        {
                                            dateFormats = formats
                                                .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.TIME)
                                                .Select(x => x.apiFormat).ToList();

                                        }
                                    }
                                }


                                var dataColumn = dataTypeId switch
                                {
                                    (int)DatatypeNamesEnum.INT => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        int intValue;
                                        return int.TryParse(x?.ToString(), out intValue) ? (int?)intValue : null;
                                    }).ToArray()),
                                    //(int)DatatypeNamesEnum.LONG => new DataColumn(dataField, columns[i].Select(x =>
                                    //{
                                    //    long longValue;
                                    //    return long.TryParse(x?.ToString(), out longValue) ? (long?)longValue : null;
                                    //}).ToArray()),
                                    (int)DatatypeNamesEnum.LONG => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        if (x == null) return (long?)null;

                                        double doubleValue;
                                        if (double.TryParse(x.ToString(), System.Globalization.NumberStyles.Float,
                                                            System.Globalization.CultureInfo.InvariantCulture, out doubleValue))
                                        {
                                            return (long?)doubleValue; // Convert double to long safely
                                        }

                                        return null;
                                    }).ToArray()),

                                    (int)DatatypeNamesEnum.FLOAT => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        float floatValue;
                                        return float.TryParse(x?.ToString(), out floatValue) ? (float?)floatValue : null;
                                    }).ToArray()),
                                    (int)DatatypeNamesEnum.DOUBLE => new DataColumn(dataField, columns[i].ToArray().Select(x =>
                                    {
                                        double doubleValue;
                                        return double.TryParse(x?.ToString(), out doubleValue) ? (double?)doubleValue : null;
                                    }).ToArray()),
                                    (int)DatatypeNamesEnum.BOOL => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        bool boolValue;
                                        return bool.TryParse(x?.ToString(), out boolValue) ? (bool?)boolValue : null;
                                    }).ToArray()),
                                    (int)DatatypeNamesEnum.DATETIME => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        DateTime dateTimeValue;
                                        return DateTime.TryParseExact(x?.ToString(), dateFormats.ToArray(),
                                               CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTimeValue)
                                               ? (DateTime?)dateTimeValue : null;
                                    }).ToArray()),
                                    (int)DatatypeNamesEnum.DATE => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        if (x == null || string.IsNullOrWhiteSpace(x.ToString()))
                                            return null;

                                        DateOnly dateOnlyValue;
                                        return DateOnly.TryParseExact(x?.ToString(), dateFormats.ToArray(),
                                               CultureInfo.InvariantCulture, DateTimeStyles.None, out dateOnlyValue)
                                               ? (DateOnly?)dateOnlyValue : null;
                                    }).ToArray()),
                                    (int)DatatypeNamesEnum.TIME => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        TimeOnly timeOnlyValue;
                                        return TimeOnly.TryParseExact(x?.ToString(), dateFormats.ToArray(),
                                               CultureInfo.InvariantCulture, DateTimeStyles.None, out timeOnlyValue)
                                               ? (TimeOnly?)timeOnlyValue : null;
                                    }).ToArray()),
                                    _ => new DataColumn(dataField, columns[i].Select(x => x?.ToString() ?? null).ToArray()), // default to string if the data type is unrecognized
                                };

                                await groupWriter.WriteColumnAsync(dataColumn);
                            }

                        }

                        resultResponse.ParquetFileCreated = true;
                    }
                }
            }
            return resultResponse;
        }

       

    }

    

}
