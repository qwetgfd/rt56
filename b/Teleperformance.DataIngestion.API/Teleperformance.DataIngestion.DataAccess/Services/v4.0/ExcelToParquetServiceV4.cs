using Azure.Storage.Blobs;
using CsvHelper;
using CsvHelper.Configuration;
using DocumentFormat.OpenXml.Drawing.Charts;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NPOI.HSSF.UserModel;
using NPOI.OpenXml4Net.OPC.Internal;
using NPOI.OpenXmlFormats.Dml.Diagram;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using SMBLibrary;
using SMBLibrary.Client;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v4._1;
using Teleperformance.DataIngestion.DataAccess.Repository.v4._1;
using Teleperformance.DataIngestion.DataAccess.Services.v3._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Enums.v3._0;
using Teleperformance.DataIngestion.Models.Helpers;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using Teleperformance.DataIngestion.Models.Models.v4._1;
using FileAttributes = SMBLibrary.FileAttributes;

namespace Teleperformance.DataIngestion.DataAccess.Services.v4._0
{
    public class ExcelToParquetServiceV4 : IExcelToParquetServiceV4
    {
        private readonly ILogger<ExcelToParquetServiceV4> _logger;
        private readonly ISMBLibraryServices _ismbLibraryServices;
        private readonly IProcessConfigurationRepository _iIProcessConfigurationRepository;
        private readonly ICache _cache;
        private readonly IValidateSchemaService _iValidateSchemaService;
        private readonly IFlpProcessingService _iIFileProcessingService;
        private readonly IProcessConfigServiceV3 _iProcessConfigurationService;
        private readonly ICsvToParquetServiceV4_1 _icsvToParquetService;
        private readonly IProcessConfigurationRepositoryV4_1 _processConfigurationRepositoryV4_1;
        public ExcelToParquetServiceV4(
           ILogger<ExcelToParquetServiceV4> logger, ISMBLibraryServices ismbLibraryServices, IProcessConfigurationRepository iIProcessConfigurationRepository, ICache cache, IValidateSchemaService iValidateSchemaService, IFlpProcessingService iIFileProcessingService, IProcessConfigServiceV3 iProcessConfigurationService,  ICsvToParquetServiceV4_1 icsvToParquetService, IProcessConfigurationRepositoryV4_1 processConfigurationRepositoryV4_1)
        {
            _logger = logger;
            _ismbLibraryServices = ismbLibraryServices;
            _iIProcessConfigurationRepository = iIProcessConfigurationRepository;
            _cache = cache;
            _iValidateSchemaService = iValidateSchemaService;
            _iIFileProcessingService = iIFileProcessingService;
            _iProcessConfigurationService = iProcessConfigurationService;
            _icsvToParquetService = icsvToParquetService;
            _processConfigurationRepositoryV4_1 = processConfigurationRepositoryV4_1;
        }


        public async Task<ParquetFileResponseDto> ConvertDataToParquet(ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationResponseDto, FlpProcessTempFile fileProcessingConfig)
        {
            ParquetFileResponseDto resultResponse = null;
            try
            {
                //var sourceFileDetails = parameterConfig.BlobClients;

                // Get the file name from the CSV path
                string fileName = FlpConfigurationHelper.GetParquetFileName(fileProcessingConfig.Name);
                string parquetFilePath = $"{fileProcessingConfig.DestinationFolder}{fileName}";
                parquetFilePath = Uri.UnescapeDataString(parquetFilePath).Replace("\\", "/") ?? "";
                BlobServiceClient blobServiceClient = new BlobServiceClient(fileProcessingConfig.ParquetBlobConnectionString);
                BlobContainerClient csvContainerClient = blobServiceClient.GetBlobContainerClient(fileProcessingConfig.BlobContainerName);
                BlobClient csvBlobClient = csvContainerClient.GetBlobClient(fileProcessingConfig.Name);
                string fileExtension = FlpConfigurationHelper.GetFileExtension(fileProcessingConfig.Name);

                // Download the CSV file as a stream
                using (var excelStream = await csvBlobClient.OpenReadAsync())
                {
                    // Get the blob client for the Parquet file
                    BlobClient parquetBlobClient = csvContainerClient.GetBlobClient(parquetFilePath);
                    // Create a stream to write the Parquet file to Blob Storage
                    using (var parquetStream = await parquetBlobClient.OpenWriteAsync(overwrite: true))
                    {

                        // Convert CSV to Parquet
                        long fileSizeInBytes = excelStream.Length;
                        double fileSizeInMB = fileSizeInBytes / (1024.0 * 1024.0);
                        int fileSize = Convert.ToInt32(Environment.GetEnvironmentVariable("TPDataIngestionExcelFileSizeForCsvHelper"));
                        if (fileSizeInMB > fileSize)
                        {
                            _logger.LogInformation($"Converting Excel to Parquet. File Size: {fileSizeInMB} MB, FlpConfigurationId: {flpConfigurationResponseDto.FlpConfigurationId}, TabName: {configurationTableMappingDto.TabName}, TableName: {configurationTableMappingDto.TableName}");

                            //// Create a memory stream to hold the CSV output
                            //using var csvStream = new MemoryStream();

                            //// Convert XLSB to CSV (specific sheet)
                            //ExcelHelper.excelSheetToCsv(excelStream, csvStream, configurationTableMappingDto.TabName);

                            //// Reset position of CSV stream before reading
                            //csvStream.Position = 0;

                            var csvStream = await ExcelHelper.ConvertExcelStreamToCsvStreamAsync(excelStream, configurationTableMappingDto.TabName);
                            _logger.LogInformation($"Converting Excel to Parquet. File Size: {fileSizeInMB} MB, FlpConfigurationId: {flpConfigurationResponseDto.FlpConfigurationId}, TabName: {configurationTableMappingDto.TabName}, TableName: {configurationTableMappingDto.TableName}");

                            var response = await _icsvToParquetService.ConvertCsvToParquetAsyncV2(csvStream, parquetStream, configurationTableMappingDto, flpConfigurationResponseDto);
                            // Reset the memory stream position to the beginning
                            resultResponse = new ParquetFileResponseDtoV4_1();
                            resultResponse.ParquetFilePath = parquetFilePath;
                            resultResponse.ParquetBlobClient = parquetBlobClient;
                            resultResponse.ParquetBlobClientTemp = csvBlobClient;
                            resultResponse.TotalRows = response.TotalRows;
                            resultResponse.DuplicateRows = 0;
                            resultResponse.InsertedRows = response.InsertedRows;
                            resultResponse.ParquetFileCreated = response.ParquetFileCreated;
                            resultResponse.ColumnDataTypeList = response.ColumnDataTypeList;
                            resultResponse.flpActivityLogStatusEnum = FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation;
                        }
                        else
                        {
                            // Convert CSV to Parquet
                            (bool ret, int totalRows, int insertedRows, int duplicateRows, Dictionary<string, DataTypeDetails> dict, FlpActivityLogStatusEnum logStatusEnum) = await ConvertExcelToParquetAsync(excelStream, parquetStream, configurationTableMappingDto, flpConfigurationResponseDto, fileExtension);
                            // Reset the memory stream position to the beginning
                            resultResponse = new ParquetFileResponseDto();
                            resultResponse.ParquetFilePath = parquetFilePath;
                            resultResponse.ParquetBlobClient = parquetBlobClient;
                            resultResponse.ParquetBlobClientTemp = csvBlobClient;
                            resultResponse.TotalRows = totalRows;
                            resultResponse.DuplicateRows = duplicateRows;
                            resultResponse.InsertedRows = insertedRows;
                            resultResponse.ParquetFileCreated = ret;
                            resultResponse.ColumnDataTypeList = dict;
                            resultResponse.flpActivityLogStatusEnum = logStatusEnum;
                        }

                          

                        return resultResponse;
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to convert data to parquet Error: {ex.Message}", $"FlpConfigurationId: {flpConfigurationResponseDto.FlpConfigurationId}");
                resultResponse = new ParquetFileResponseDto();
                resultResponse.ParquetFileCreated = false;
                resultResponse.ErrorMessage = ex.Message;
                return resultResponse;
            }
        }

        public async Task<ParquetFileResponseDto> ConvertDataToParquetOnPremSharedLocation(string csvTempPath, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto flpConfigurationResponseDto, CheckConnectivitySMBLibraryModel destinationServerModel)
        {
            ParquetFileResponseDto resultResponse = new ParquetFileResponseDto();
            resultResponse.ParquetFileCreated = false;
            SMB2Client smbClient = new SMB2Client();
            ISMBFileStore fileStore = null;
            try
            {
                // Step 1: Generate parquet file name and path
                string fileName = FlpConfigurationHelper.GetParquetFileName(csvTempPath);
                string parquetFilePath = $"{flpConfigurationResponseDto.DestinationPath}{fileName}";
                parquetFilePath = Uri.UnescapeDataString(parquetFilePath).Replace("\\", "/") ?? "";
                string fileExtension = FlpConfigurationHelper.GetFileExtension(csvTempPath);

                // Step 2: Connect to the destination SMB server
                (smbClient, fileStore) = _ismbLibraryServices.SMBRequest(destinationServerModel, flpConfigurationResponseDto.FlpConfigurationId);

                // Step 3: Get the CSV stream from the SMB server
                using (var csvStream = _ismbLibraryServices.GetFileStream(fileStore, csvTempPath, flpConfigurationResponseDto.FlpConfigurationId))
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
                            (bool ret, int totalRows,int insertedRows, int duplicateRows, Dictionary<string, DataTypeDetails> dict, FlpActivityLogStatusEnum logStatusEnum) = await ConvertExcelToParquetAsync(csvStream, parquetStream, configurationTableMappingDto, flpConfigurationResponseDto, fileExtension);
                            resultResponse.ParquetFileCreated = ret;
                            resultResponse.TotalRows = totalRows;
                            resultResponse.DuplicateRows = duplicateRows;
                            resultResponse.InsertedRows = insertedRows;
                            resultResponse.flpActivityLogStatusEnum = logStatusEnum;
                        }

                        // Mark the parquet file path and success status
                        resultResponse.ParquetFilePath = parquetFilePath;
                        resultResponse.ParquetFileCreated = true;
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
                resultResponse = new ParquetFileResponseDto();
                resultResponse.ParquetFileCreated = false;
                resultResponse.ErrorMessage = ex.Message;
            }
            finally
            {
                // Ensure proper cleanup
                smbClient.Logoff();
                smbClient.Disconnect();
            }

            return resultResponse;
        }



        private async Task<(bool success, int recordCount,int insertedRows, int duplicateRows, Dictionary<string, DataTypeDetails>, FlpActivityLogStatusEnum)> ConvertExcelToParquetAsync(Stream excelStream, Stream parquetStream, ConfigurationTableMappingDto parameterConfig, FlpConfigurationResponseDto flpConfigurationResponseDto, string fileExtention)
        {
            int recordCount = 0;
            int duplicateRows = 0;
            int insertedRows = 0;
            bool success = false;
            // create file schema            
            IWorkbook workbook;
            IRow firstRow = null;
            int firstColNum = 0;
            int lastColNum = 0;
            List<string> filesHeaders = new List<string>();
            List<string> copyFielesHeaders = new List<string>();
            var fileColumnMapping = new Dictionary<string, FlpFileColumnMappingDto>();
            var fields = new List<DataField>();
            IEnumerable<DateTimeFormats?> formats = null;
            var columnIndices = new Dictionary<string, int>();
            var columnsFormat = new Dictionary<int, DataTypeDetails>();
            var columnDataTypeList = new Dictionary<string, DataTypeDetails>();
            XLSBWorkbookModel xlsbWorkbookModel = null;
            //IWorkbook workbook = WorkbookFactory.Create(excelStream);
            //// Load workbook based on file extension (.xls or .xlsx)
            double fileSize = 0;


            if (string.Compare(fileExtention, ".xls", true) == 0)
            {
                workbook = new HSSFWorkbook(excelStream); // Use HSSF for .xls files
            }
            else if (string.Compare(fileExtention, ".xlsb", true) == 0)
            {
                xlsbWorkbookModel = ExcelHelper.ConvertXLSBtoXLSX(excelStream,null); // Convert .xlsb to .xlsx
                workbook = xlsbWorkbookModel.workbook; // Use the converted workbook
            }
            else
            {
                workbook = new XSSFWorkbook(excelStream); // Use XSSF for .xlsx files

            }
            var sheet = workbook.GetSheetAt(0); // Get the first sheet
            int firstRowNum = string.Compare(fileExtention, ".xlsb", true) == 0 ? xlsbWorkbookModel.emptyRowCount : sheet.FirstRowNum;
            int lastRowNum = sheet.LastRowNum;
            var headerRowsStarted = parameterConfig.SkipRows;
            int emptyHeaderRowsIndex = 0;
            //// 1. Find first non-empty row for header

            while (emptyHeaderRowsIndex <= lastRowNum)
            {
                var row = sheet.GetRow(emptyHeaderRowsIndex);
                if (row != null && !IsRowEmpty(row, row.FirstCellNum, row.LastCellNum - 1))
                    break;
                emptyHeaderRowsIndex++;
            }
            headerRowsStarted = headerRowsStarted + emptyHeaderRowsIndex;


            bool hasDuplicateOrEmptyHeader = false;
            List<string> columnNameList = new List<string>();
            List<string> sheetFormatList = new List<string>();
            
            //var (convertDatatypeString, columnDataTypeMappings) = FlpConfigurationHelper.GetColumnDataTypeList(parameterConfig.ConvertDataTypeColumnNameList);            
            var uniqueColumns = FlpConfigurationHelper.SplitString(parameterConfig.KeyColumnList, ",");
            var dedupColumnsList = FlpConfigurationHelper.SplitString(parameterConfig.OrderByColumnListForDedup, ",");

            try
            {
               
               
                firstRow = sheet.GetRow(headerRowsStarted);
                if (firstRow == null && !parameterConfig.IsHeaderProvided)
                {
                    throw new Exception("Unable to process file,Please delete first empty rows If in case Has Header false");
                }

                if (firstRow == null)
                {
                    throw new Exception("Unable to process file,Please delete first empty rows");
                }
                firstColNum = firstRow.FirstCellNum;
                lastColNum = firstRow.LastCellNum - 1;
                formats = await _cache.GetFormatListAsync();

                (bool ret, string message) = FlpConfigurationHelper.checkSelectedValidOptions(parameterConfig, parameterConfig.KeyColumnList, parameterConfig.OrderByColumnListForDedup);
                if (!ret)
                {
                    throw new Exception(message);
                }
                IRow headerRow = sheet.GetRow(headerRowsStarted);

                if (headerRow == null)
                {
                    throw new Exception("Header row not found!");
                }

                int actualColumnCount = headerRow.LastCellNum; // This includes empty cells

                // Optional: Trim trailing empty columns
                while (lastColNum > 0 && string.IsNullOrWhiteSpace(headerRow.GetCell(actualColumnCount - 1)?.ToString()))
                {
                    actualColumnCount--;
                }
                lastColNum = actualColumnCount - 1; // Adjust lastColNum to the last non-empty cell



                bool foundFirstNonEmpty = true;
                // Assume: headerRow is already set, and firstColNum, lastColNum, headerRowsStarted, lastRowNum are defined

                //To getting rows from the specific index
                var startingRows = ((headerRowsStarted + 1) - parameterConfig.SkipRows);
                for (int col = 0; col <= lastColNum; col++)
                {
                    // var cell = headerRow.GetCell(col);
                    var headerCell = headerRow.GetCell(col, MissingCellPolicy.RETURN_BLANK_AS_NULL);
                    string columnName = headerCell != null ? FlpConfigurationHelper.CleanColumnName(headerCell.ToString()) : string.Empty;

                    // Use LINQ to check if any cell in this column (excluding the header row) is non-empty
                    bool hasValue = Enumerable.Range(startingRows, lastRowNum - startingRows)
                        .Select(rowIdx => sheet.GetRow(rowIdx)?.GetCell(col, MissingCellPolicy.RETURN_BLANK_AS_NULL))
                        .Any(cell => !FlpConfigurationHelper.IsCellEmpty(cell));
                    // If the column name empty then we will continue fine & find the next empty column
                    if (string.IsNullOrWhiteSpace(columnName) && !hasValue)
                    {
                        continue;
                    }
                    else
                    {
                        //as we got the value then will go from the outside the loop
                        firstColNum = col;
                        foundFirstNonEmpty = true;
                        break;
                    }

                }

                if (parameterConfig.IsHeaderProvided)
                {
                    
                    for (int col = firstColNum; col <= lastColNum; col++)
                    {                        
                        var headerCell = headerRow.GetCell(col, MissingCellPolicy.RETURN_BLANK_AS_NULL);
                        string columnName = headerCell != null ? FlpConfigurationHelper.CleanColumnName(headerCell.ToString()) : string.Empty;

                        // Use LINQ to check if any cell in this column (excluding the header row) is non-empty
                        bool hasValue = Enumerable.Range(startingRows, lastRowNum - startingRows)
                            .Select(rowIdx => sheet.GetRow(rowIdx)?.GetCell(col, MissingCellPolicy.RETURN_BLANK_AS_NULL))
                            .Any(cell => !FlpConfigurationHelper.IsCellEmpty(cell));

                        filesHeaders.Add(columnName);
                    }
                    //
                    //filesHeaders = headerRow.Cells.Select(cell => cell != null ? FlpConfigurationHelper.CleanColumnName(cell.ToString()) : string.Empty ).ToList();
                    /*hasDuplicateOrEmptyHeader = FlpConfigurationHelper.ContainsDuplicatesOrEmptyHeader(filesHeaders);
                    if (hasDuplicateOrEmptyHeader)
                    {
                        filesHeaders = FlpConfigurationHelper.CreateCustomHeader(filesHeaders);
                    }*/
                    if (filesHeaders.Any())
                    {
                        if (parameterConfig.SpanishToEnglish)
                        {
                            filesHeaders = await TextAndCsvHelper.ConvertSpanishToEnglish(filesHeaders, _iProcessConfigurationService);


                            if (parameterConfig.OrdinalToRoman)
                            {
                                filesHeaders = filesHeaders.Select(cell => !string.IsNullOrWhiteSpace(cell.ToString()) ? FlpConfigurationHelper.ConvertToRoman(cell.ToString()) : cell.ToString()).ToList();
                            }

                            hasDuplicateOrEmptyHeader = FlpConfigurationHelper.ContainsDuplicatesOrEmptyHeader(filesHeaders);
                            if (hasDuplicateOrEmptyHeader)
                            {
                                filesHeaders = FlpConfigurationHelper.CreateCustomHeader(filesHeaders);
                            }


                        }
                        else
                        {
                            if (parameterConfig.OrdinalToRoman)
                            {
                                filesHeaders = filesHeaders.Select(cell => !string.IsNullOrWhiteSpace(cell.ToString()) ? FlpConfigurationHelper.ConvertToRoman(cell.ToString()) : cell.ToString()).ToList();
                            }
                            hasDuplicateOrEmptyHeader = FlpConfigurationHelper.ContainsDuplicatesOrEmptyHeader(filesHeaders);
                            if (hasDuplicateOrEmptyHeader)
                            {
                                filesHeaders = FlpConfigurationHelper.CreateCustomHeader(filesHeaders);
                            }

                        }


                        copyFielesHeaders = new List<string>(filesHeaders);
                        bool exists = filesHeaders.Contains("divalidationrowno");
                        if ((flpConfigurationResponseDto.UIValidation || flpConfigurationResponseDto.BEValidation) && !parameterConfig.ValidateFileSchema && exists)
                        {
                            copyFielesHeaders.Remove("divalidationrowno");
                        }

                        fileColumnMapping = await _iValidateSchemaService.ValidateFileSchema(parameterConfig.FlpConfigurationId, parameterConfig.ValidateFileSchema, parameterConfig.IsHeaderProvided, copyFielesHeaders, flpConfigurationResponseDto.ProcessName, parameterConfig.TableName);

                        if (flpConfigurationResponseDto.UIValidation || flpConfigurationResponseDto.BEValidation)
                        {
                            var flpFileColumnMappingDto = FlpConfigurationHelperV4_1.AddRowNumberInColumnList();
                            fileColumnMapping.Add(flpFileColumnMappingDto.FileColumn, flpFileColumnMappingDto);
                        }

                        foreach (KeyValuePair<string, FlpFileColumnMappingDto> ele in fileColumnMapping)
                        {
                            DataTypeDetails dataTypeDetails = new DataTypeDetails();
                            string dbColumn = ele.Value.DbColumn;
                            string fileColumn = ele.Key;
                            string format = string.Empty;
                            var dataTypeId = ele.Value.DataTypeId;
                            var dataType = FlpConfigurationHelper.GetDateTypeValue(dataTypeId);
                            dataTypeDetails.DataType = dataType;

                            if (dataTypeId == (int)DatatypeNamesEnum.DATE || dataTypeId == (int)DatatypeNamesEnum.DATETIME || dataTypeId == (int)DatatypeNamesEnum.TIME)
                            {
                                var formatId = ele.Value.FormatId;
                                format = formats?.FirstOrDefault(fv => fv.formatId == formatId).apiFormat ?? "";
                                if (string.IsNullOrWhiteSpace(format))
                                    throw new Exception("Format doesn't exit for configuration " + parameterConfig.FlpConfigurationId);
                                dataTypeDetails.DataTypeFormat = format;
                            }

                            fields.Add(FlpConfigurationHelper.CreateDataField(dataTypeId, dbColumn));

                            // Find column index
                            int colIndex = filesHeaders.FindIndex(header =>
                                string.Equals(header.Trim(), fileColumn, StringComparison.OrdinalIgnoreCase));

                            if (colIndex == -1)
                            {
                                throw new Exception($"Header '{fileColumn}' not found in the provided file.");
                            }

                            //Update unique (Key column)
                            int uniqeListIndex = uniqueColumns.FindIndex(inx =>
                                string.Equals(inx.Trim(), fileColumn, StringComparison.OrdinalIgnoreCase));
                            if (uniqeListIndex != -1)
                            {
                                uniqueColumns[uniqeListIndex] = dbColumn;
                            }

                            //Dedup column list 
                            int dedupListIndex = dedupColumnsList.FindIndex(inx =>
                               string.Equals(inx.Trim(), fileColumn, StringComparison.OrdinalIgnoreCase));
                            if (dedupListIndex != -1)
                            {
                                dedupColumnsList[dedupListIndex] = dbColumn;
                            }
                            if (firstColNum > 0)
                            {
                                colIndex = colIndex + firstColNum;
                            }
                            columnIndices[dbColumn] = colIndex;
                            columnDataTypeList[dbColumn] = dataTypeDetails;
                            columnsFormat[colIndex] = dataTypeDetails;

                        }


                    }
                    else
                    {
                        throw new Exception("Not found filesHeaders " + parameterConfig.FlpConfigurationId);
                    }
                }
                else
                {



                    List<string> flpCustomHeaderList = new List<string>();
                    //Create Custom Header 
                    int index = 0;
                    filesHeaders = new List<string>();

                    //bool exists = filesHeaders.Contains("divalidationrowno");
                    //if ((flpConfigurationResponseDto.UIValidation || flpConfigurationResponseDto.BEValidation) && !parameterConfig.ValidateFileSchema && exists)
                    //{
                    //    copyFielesHeaders.Remove("divalidationrowno");
                    //}
                    if ((flpConfigurationResponseDto.UIValidation || flpConfigurationResponseDto.BEValidation))
                    {
                        //Create Custom Header 

                        for (int col = firstColNum; col <= lastColNum - 1; col++)
                        {

                            string customColName = $"col{index}";//FlpConfigurationHelper.CleanColumnName($"col{col}");
                            flpCustomHeaderList.Add(customColName);
                            index++;
                        }

                        if (flpCustomHeaderList.Any())
                        {

                            if (parameterConfig.OrdinalToRoman)
                            {
                                flpCustomHeaderList = flpCustomHeaderList.Select(cell => FlpConfigurationHelper.ConvertToRoman(cell.ToString())).ToList();
                            }
                            if (parameterConfig.SpanishToEnglish)
                            {
                                fileColumnMapping = await _iValidateSchemaService.ValidateFileSchema(parameterConfig.FlpConfigurationId, parameterConfig.ValidateFileSchema, parameterConfig.IsHeaderProvided, flpCustomHeaderList, flpConfigurationResponseDto.ProcessName, parameterConfig.TableName);
                            }
                            else
                            {
                                fileColumnMapping = await _iValidateSchemaService.ValidateFileSchema(parameterConfig.FlpConfigurationId, parameterConfig.ValidateFileSchema, parameterConfig.IsHeaderProvided, flpCustomHeaderList, flpConfigurationResponseDto.ProcessName, parameterConfig.TableName);
                            }

                            if (flpConfigurationResponseDto.UIValidation || flpConfigurationResponseDto.BEValidation)
                            {
                                var flpFileColumnMappingDto = FlpConfigurationHelperV4_1.AddRowNumberInColumnList();
                                fileColumnMapping.Add(flpFileColumnMappingDto.FileColumn, flpFileColumnMappingDto);
                                flpCustomHeaderList.Add(flpFileColumnMappingDto.FileColumn);
                                filesHeaders = fileColumnMapping.Select(x => x.Value.DbColumn).ToList();

                            }
                            foreach (KeyValuePair<string, FlpFileColumnMappingDto> ele in fileColumnMapping)
                            {
                                DataTypeDetails dataTypeDetails = new DataTypeDetails();
                                string dbColumn = ele.Value.DbColumn;
                                string fileColumn = ele.Key;
                                string format = string.Empty;
                                var dataTypeId = ele.Value.DataTypeId;
                                var dataType = FlpConfigurationHelper.GetDateTypeValue(dataTypeId);
                                dataTypeDetails.DataType = dataType;

                                if (dataTypeId == (int)DatatypeNamesEnum.DATE || dataTypeId == (int)DatatypeNamesEnum.DATETIME || dataTypeId == (int)DatatypeNamesEnum.TIME)
                                {
                                    var formatId = ele.Value.FormatId;
                                    format = formats?.FirstOrDefault(fv => fv.formatId == formatId).apiFormat ?? "";
                                    if (string.IsNullOrWhiteSpace(format))
                                        throw new Exception("Format doesn't exit for configuration " + parameterConfig.FlpConfigurationId);
                                    dataTypeDetails.DataTypeFormat = format;
                                }

                                fields.Add(FlpConfigurationHelper.CreateDataField(dataTypeId, dbColumn));
                                // Find column index
                                int colIndex = flpCustomHeaderList.FindIndex(header =>
                                    string.Equals(header.Trim(), fileColumn, StringComparison.OrdinalIgnoreCase));

                                string v = fileColumn;

                                if (colIndex < 0)
                                {
                                    throw new Exception($"Header '{fileColumn}' not found in the provided file.");
                                }

                                //Update unique (Key column)
                                int uniqeListIndex = uniqueColumns.FindIndex(inx =>
                                    string.Equals(inx.Trim(), fileColumn, StringComparison.OrdinalIgnoreCase));
                                if (uniqeListIndex != -1)
                                {
                                    uniqueColumns[uniqeListIndex] = dbColumn;
                                }

                                //Dedup column list 
                                int dedupListIndex = dedupColumnsList.FindIndex(inx =>
                                   string.Equals(inx.Trim(), fileColumn, StringComparison.OrdinalIgnoreCase));
                                if (dedupListIndex != -1)
                                {
                                    dedupColumnsList[dedupListIndex] = dbColumn;
                                }
                                if (firstColNum > 0)
                                {
                                    colIndex = colIndex + firstColNum;
                                }
                                columnIndices[dbColumn] = colIndex;
                                columnDataTypeList[dbColumn] = dataTypeDetails;
                                columnsFormat[colIndex] = dataTypeDetails;

                            }
                        }
                        else
                        {
                            throw new Exception("Not found filesHeaders " + parameterConfig.FlpConfigurationId);
                        }
                    }
                    else
                    {
                        for (int col = firstColNum; col <= lastColNum; col++)
                        {

                            string customColName = $"col{index}";//FlpConfigurationHelper.CleanColumnName($"col{col}");
                            flpCustomHeaderList.Add(customColName);
                            index++;
                        }

                        if (flpCustomHeaderList.Any())
                        {

                            if (parameterConfig.OrdinalToRoman)
                            {
                                flpCustomHeaderList = flpCustomHeaderList.Select(cell => FlpConfigurationHelper.ConvertToRoman(cell.ToString())).ToList();
                            }
                            if (parameterConfig.SpanishToEnglish)
                            {

                                fileColumnMapping = await _iValidateSchemaService.ValidateFileSchema(parameterConfig.FlpConfigurationId, parameterConfig.ValidateFileSchema, parameterConfig.IsHeaderProvided, flpCustomHeaderList, flpConfigurationResponseDto.ProcessName, parameterConfig.TableName);

                            }
                            else
                            {
                                fileColumnMapping = await _iValidateSchemaService.ValidateFileSchema(parameterConfig.FlpConfigurationId, parameterConfig.ValidateFileSchema, parameterConfig.IsHeaderProvided, flpCustomHeaderList, flpConfigurationResponseDto.ProcessName, parameterConfig.TableName);
                            }
                            foreach (KeyValuePair<string, FlpFileColumnMappingDto> ele in fileColumnMapping)
                            {
                                DataTypeDetails dataTypeDetails = new DataTypeDetails();
                                string dbColumn = ele.Value.DbColumn;
                                string fileColumn = ele.Key;
                                string format = string.Empty;
                                var dataTypeId = ele.Value.DataTypeId;
                                var dataType = FlpConfigurationHelper.GetDateTypeValue(dataTypeId);
                                dataTypeDetails.DataType = dataType;

                                if (dataTypeId == (int)DatatypeNamesEnum.DATE || dataTypeId == (int)DatatypeNamesEnum.DATETIME || dataTypeId == (int)DatatypeNamesEnum.TIME)
                                {
                                    var formatId = ele.Value.FormatId;
                                    format = formats?.FirstOrDefault(fv => fv.formatId == formatId).apiFormat ?? "";
                                    if (string.IsNullOrWhiteSpace(format))
                                        throw new Exception("Format doesn't exit for configuration " + parameterConfig.FlpConfigurationId);
                                    dataTypeDetails.DataTypeFormat = format;
                                }

                                fields.Add(FlpConfigurationHelper.CreateDataField(dataTypeId, dbColumn));
                                // Find column index
                                int colIndex = flpCustomHeaderList.FindIndex(header =>
                                    string.Equals(header.Trim(), fileColumn, StringComparison.OrdinalIgnoreCase));

                                string v = fileColumn;

                                if (colIndex < 0)
                                {
                                    throw new Exception($"Header '{fileColumn}' not found in the provided file.");
                                }

                                //Update unique (Key column)
                                int uniqeListIndex = uniqueColumns.FindIndex(inx =>
                                    string.Equals(inx.Trim(), fileColumn, StringComparison.OrdinalIgnoreCase));
                                if (uniqeListIndex != -1)
                                {
                                    uniqueColumns[uniqeListIndex] = dbColumn;
                                }

                                //Dedup column list 
                                int dedupListIndex = dedupColumnsList.FindIndex(inx =>
                                   string.Equals(inx.Trim(), fileColumn, StringComparison.OrdinalIgnoreCase));
                                if (dedupListIndex != -1)
                                {
                                    dedupColumnsList[dedupListIndex] = dbColumn;
                                }
                                if (firstColNum > 0)
                                {
                                    colIndex = colIndex + firstColNum;
                                }
                                columnIndices[dbColumn] = colIndex;
                                columnDataTypeList[dbColumn] = dataTypeDetails;
                                columnsFormat[colIndex] = dataTypeDetails;

                            }
                        }
                        else
                        {
                            throw new Exception("Not found filesHeaders " + parameterConfig.FlpConfigurationId);
                        }
                    }
                       
                }

                //Schema is validated 
                if (fields.Any() && fields.Count == fileColumnMapping.Keys.Count)
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
                    string errorMessage = fields != null && fields.Any() ? "file header does not match with existing database column" : "Not found file header";
                    await _iIFileProcessingService.AddFileProcessLosStatus(
                             fileType: flpConfigurationResponseDto.FileType,
                             loginId: "",
                             message: errorMessage,
                             messageType: "error",
                             processId: flpConfigurationResponseDto.ProcessId,
                             processName: flpConfigurationResponseDto.ProcessName,
                             tableName: parameterConfig.TableName,
                             totalRows: 0,
                             flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                             fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                             FileStatusActivityEnum.Error,
                              FlpActivityLogStatusEnum.FileSchemaValidated
                         );

                    success = false;
                    return (success, recordCount,insertedRows, duplicateRows, columnDataTypeList, FlpActivityLogStatusEnum.FileSchemaValidated);
                }


            }
            catch (Exception ex)
            {

                string errorMessage = $"{ex.Message}";
                await _iIFileProcessingService.AddFileProcessLosStatus(
                         fileType: flpConfigurationResponseDto.FileType,
                         loginId: "",
                         message: errorMessage,
                         messageType: "error",
                         processId: flpConfigurationResponseDto.ProcessId,
                         processName: flpConfigurationResponseDto.ProcessName,
                         tableName: parameterConfig.TableName,
                         totalRows: 0,
                         flpConfigurationId: flpConfigurationResponseDto.FlpConfigurationId,
                         fileUploadedId: flpConfigurationResponseDto.UploadedFileId,
                         FileStatusActivityEnum.Error,
                          FlpActivityLogStatusEnum.FileSchemaValidated
                     );

                success = false;
                return (success, recordCount,insertedRows, duplicateRows, columnDataTypeList, FlpActivityLogStatusEnum.FileSchemaValidated);
            }

            //Schema validation is completed...
            if (filesHeaders.Any())
                await _processConfigurationRepositoryV4_1.AddFileHeaders(parameterConfig.FlpConfigurationId, flpConfigurationResponseDto.UploadedFileId, string.Join(",", filesHeaders), parameterConfig.TabName);



            // Create Parquet schema
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


            var schema = new ParquetSchema(fields.ToArray());

            using (var parquetWriter = await ParquetWriter.CreateAsync(schema, parquetStream))
            {

                if (parameterConfig.ParquetCompression?.ToUpper() == "GZIP")
                {
                    parquetWriter.CompressionMethod = CompressionMethod.Gzip;
                    parquetWriter.CompressionLevel = System.IO.Compression.CompressionLevel.Optimal;
                }
                int chunkSize = 10000; // Chunk size
                int headerRows = headerRowsStarted + (parameterConfig.IsHeaderProvided ? 1 : 0);
                // int headerRows = headerRowsStarted + (parameterConfig.IsHeaderProvided ? 1 : 0);
                int startingRows = firstRowNum + headerRows;// headerRowsStarted + parameterConfig.SkipRows;
                int lastRows = lastRowNum - parameterConfig.SkipFooterRows;
                var columns = new List<List<object>>();
                var duplicateRowList = new List<List<object>>();
                for (int col = 0; col <= lastColNum; col++)
                {
                    columns.Add(new List<object>());
                }
                var uniqueRows = new Dictionary<string, List<string>>();
                var uniqueRowsWithDedup = new Dictionary<string, List<List<object>>>();
                if (emptyHeaderRowsIndex > 0)
                {
                    startingRows = startingRows - emptyHeaderRowsIndex;
                }
                int rowCountError = 0;
                for (int startRow = startingRows; startRow <= lastRows; startRow += chunkSize)
                {

                    try
                    {

                        int endRow = Math.Min(startRow + chunkSize - 1, lastRows);
                        recordCount += endRow - startRow + 1;


                        if (parameterConfig.IgnoreDuplicateRows)
                        {

                            for (int row = startRow; row <= endRow; row++)
                            {
                                if (parameterConfig.SkipEmptyLines)
                                {
                                    if (IsRowEmpty(sheet.GetRow(row), firstColNum, lastColNum))
                                    {
                                        recordCount--;
                                        continue;
                                    }
                                }

                                var rowKeyParts = new List<string>();
                                if (dedupColumnsList.Any())
                                {

                                    var rowData = new List<object>();
                                    for (int col = firstColNum; col <= lastColNum; col++)
                                    {
                                        rowCountError = row;
                                        if (rowCountError == 23632)
                                        {

                                        }

                                        object value = sheet.GetRow(row)?.GetCell(col); // Handle potential null valuesvalues
                                                                                        //string value1 = sheet.GetRow(row)?.GetCell(col)?.ToString() ?? string.Empty; // Handle potential null valuesvalues

                                        if (value != null && columnIndices.Values.Contains(col))
                                        {

                                            try
                                            {

                                                var colInxValue = columnIndices.Values.ToList().FindIndex(a => a == col);
                                                var dataField = schema.GetDataFields()[colInxValue];
                                                var columnsKey = columnIndices.Values.FirstOrDefault(x => x == col);
                                                // bool? isKeyColumn = columnIndices.ContainsKey(dataField?.Name?.Trim()??"");
                                                bool? isKeyColumn = uniqueColumns.Any(x => x?.Trim() == dataField?.Name?.Trim());
                                                if (columnsFormat.TryGetValue(columnsKey, out DataTypeDetails DataTypeDetails))
                                                {
                                                    string dataType = DataTypeDetails.DataType;
                                                    var cell = sheet.GetRow(row)?.GetCell(col);

                                                    if (cell?.CellType == CellType.Numeric)
                                                    {
                                                        // Check if the cell is formatted as a percentage
                                                        string formatString = cell.CellStyle.GetDataFormatString();
                                                        if (formatString.Contains("%"))
                                                        {
                                                            // Convert numeric value to percentage string
                                                            value = (cell.NumericCellValue * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
                                                            // Console.WriteLine(formatted); // Output: "90%"
                                                        }

                                                    }
                                                    if (cell?.CellType == CellType.Formula)
                                                    {
                                                        value = cell.StringCellValue;

                                                    }
                                                    else if (cell != null && cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
                                                    {
                                                        if ((DataTypeDetails.DataType == DataTypeOptionsEnum.DATETIME.GetDescription() ||
                                                            DataTypeDetails.DataType == DataTypeOptionsEnum.DATE.GetDescription() ||
                                                            DataTypeDetails.DataType == DataTypeOptionsEnum.TIME.GetDescription())
                                                            && !string.IsNullOrEmpty(DataTypeDetails.DataTypeFormat))
                                                        {
                                                            value = FlpConfigurationHelper.GetCellValue(sheet, cell, DataTypeDetails.DataType, DataTypeDetails.DataTypeFormat, fileExtention);
                                                        }
                                                        else
                                                        {


                                                            var style = cell.CellStyle;
                                                            var fmt = style?.GetDataFormatString();

                                                            var info = ExcelFormatInspector.Inspect(cell);
                                                            if (info != null && info.IsBuiltInFormat && !string.IsNullOrWhiteSpace(info.BuiltinFormatString) && info.IsCustomFormat == false
                                                                && fmt.Contains("d") && fmt.Contains("m") && fmt.Contains("yy") && !fmt.Contains("yyyy"))
                                                            {

                                                                var evaluator = workbook.GetCreationHelper().CreateFormulaEvaluator();
                                                                value = ExcelDisplayText.FormatWithFourDigitYear(cell, evaluator, CultureInfo.CurrentCulture);

                                                                if (value == null)
                                                                {
                                                                    var formatter = new DataFormatter(CultureInfo.CurrentCulture); // or a specific culture
                                                                    value = formatter.FormatCellValue(cell);
                                                                }


                                                            }
                                                            else
                                                            {
                                                                var formatter = new DataFormatter(CultureInfo.CurrentCulture); // or a specific culture
                                                                value = formatter.FormatCellValue(cell);
                                                                if (value == null)
                                                                    value = FlpConfigurationHelper.GetDateTimeCellStringValue(sheet, cell, DataTypeDetails.DataType, DataTypeDetails.DataTypeFormat, fileExtention);

                                                            }

                                                        }

                                                    }
                                                    else if (cell != null && !string.IsNullOrWhiteSpace(value?.ToString()) && DateTime.TryParse(value?.ToString(), out DateTime result))
                                                    {
                                                        //value = FlpConfigurationHelper.GetCellValue(sheet, cell, DataTypeDetails.DataType, DataTypeDetails.DataTypeFormat, fileExtention);
                                                        if ((DataTypeDetails.DataType == DataTypeOptionsEnum.DATETIME.GetDescription() ||
                                                       DataTypeDetails.DataType == DataTypeOptionsEnum.DATE.GetDescription() ||
                                                       DataTypeDetails.DataType == DataTypeOptionsEnum.TIME.GetDescription())
                                                       && !string.IsNullOrEmpty(DataTypeDetails.DataTypeFormat))
                                                        {
                                                            value = FlpConfigurationHelper.GetCellValue(sheet, cell, DataTypeDetails.DataType, DataTypeDetails.DataTypeFormat, fileExtention);
                                                        }
                                                        else
                                                        {


                                                            var style = cell.CellStyle;
                                                            var fmt = style?.GetDataFormatString();

                                                            var info = ExcelFormatInspector.Inspect(cell);
                                                            if (info != null && info.IsBuiltInFormat && !string.IsNullOrWhiteSpace(info.BuiltinFormatString) && info.IsCustomFormat == false
                                                                && fmt.Contains("d") && fmt.Contains("m") && fmt.Contains("yy") && !fmt.Contains("yyyy"))
                                                            {

                                                                var evaluator = workbook.GetCreationHelper().CreateFormulaEvaluator();
                                                                value = ExcelDisplayText.FormatWithFourDigitYear(cell, evaluator, CultureInfo.CurrentCulture);

                                                                if (value == null)
                                                                {
                                                                    var formatter = new DataFormatter(CultureInfo.CurrentCulture); // or a specific culture
                                                                    value = formatter.FormatCellValue(cell);
                                                                }


                                                            }
                                                            else
                                                            {
                                                                var formatter = new DataFormatter(CultureInfo.CurrentCulture); // or a specific culture
                                                                value = formatter.FormatCellValue(cell);
                                                                if (value == null)
                                                                    value = FlpConfigurationHelper.GetCellValue(sheet, cell, DataTypeDetails.DataType, DataTypeDetails.DataTypeFormat, fileExtention);
                                                            }



                                                        }
                                                    }
                                                    sheetFormatList = new List<string>();
                                                    if (DataTypeDetails.DataType == "date")
                                                    {
                                                        // var formatParts = format.Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Length > 0 ? f[0].ToString() : f).ToList(); // Take first character .ToArray();                                               

                                                        sheetFormatList = formats
                                                            .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.DATE)
                                                            .Select(x => x.apiFormat).ToList();

                                                    }
                                                    else if (DataTypeDetails.DataType == "datetime")
                                                    {

                                                        sheetFormatList = formats
                                                            .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.DATETIME)
                                                            .Select(x => x.apiFormat).ToList();
                                                    }
                                                    else if (DataTypeDetails.DataType == "time")
                                                    {
                                                        sheetFormatList = formats
                                                            .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.TIME)
                                                            .Select(x => x.apiFormat).ToList();
                                                        var dateTimeFormatList = formats
                                                      .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.DATETIME)
                                                      .Select(x => x.apiFormat).ToList();
                                                        if (sheetFormatList.Any() && dateTimeFormatList.Any())
                                                        {
                                                            sheetFormatList.AddRange(dateTimeFormatList);
                                                        }

                                                    }
                                                    value = DataTypeDetails.DataType switch
                                                    {
                                                        "int" => int.TryParse(value?.ToString(), out int intValue) ? intValue : null,
                                                        "long" => long.TryParse(value.ToString(), out long longValue) ? longValue : null,
                                                        "float" => float.TryParse(value.ToString(), out float floatValue) ? floatValue : null,
                                                        "double" => double.TryParse(value.ToString(), out double doubleValue) ? doubleValue : null,
                                                        "bool" => bool.TryParse(value.ToString(), out bool boolValue) ? boolValue : null,
                                                        // For handling DateTime with custom formats
                                                        "datetime" => DateTime.TryParseExact(value.ToString(),
                                                                        sheetFormatList.ToArray(),
                                                                       System.Globalization.CultureInfo.InvariantCulture,
                                                                       System.Globalization.DateTimeStyles.None,
                                                                       out DateTime datetimeValue) ? datetimeValue : (DateTime?)null,

                                                        // For handling DateOnly with custom formats
                                                        "date" => DateOnly.TryParseExact(value.ToString(),
                                                                    sheetFormatList.ToArray(),
                                                                    System.Globalization.CultureInfo.InvariantCulture,
                                                                    System.Globalization.DateTimeStyles.None,
                                                                    out DateOnly dateOnlyValue) ? (DateOnly?)dateOnlyValue : null,

                                                        // For handling TimeOnly with custom formats
                                                        "time" => TimeOnly.TryParseExact(value.ToString(),
                                                                    sheetFormatList.ToArray(),
                                                                    System.Globalization.CultureInfo.InvariantCulture,
                                                                    System.Globalization.DateTimeStyles.None,
                                                                    out TimeOnly timeOnlyValue) ? (TimeOnly?)timeOnlyValue : null,

                                                        _ => value.ToString()
                                                    };
                                                }



                                                if (isKeyColumn == true)
                                                {
                                                    rowKeyParts.Add(value?.ToString() ?? "");
                                                }

                                            }
                                            catch (Exception ex)
                                            {

                                                throw new Exception(ex.Message.ToString());
                                            }
                                        }

                                        rowData.Add(value);
                                    }
                                    string rowKey = string.Join("|", rowKeyParts);
                                    if (uniqueRowsWithDedup.ContainsKey(rowKey))
                                    {
                                        uniqueRowsWithDedup[rowKey].Add(rowData);
                                    }
                                    else
                                    {
                                        // If the key does not exist, create a new entry
                                        uniqueRowsWithDedup[rowKey] = new List<List<object>> { rowData };
                                    }
                                }
                                else
                                {


                                    var rowData = new List<string>();
                                    for (int col = firstColNum; col <= lastColNum; col++)
                                    {
                                        rowCountError = row;
                                        if (rowCountError == 21435)
                                        {

                                        }
                                        //var cell = sheet.GetRow(row)?.GetCell(col);
                                        string cellValue = string.Empty;
                                        string colDataType = DataTypeOptionsEnum.STRING.GetDescription();
                                        string dateFormat = string.Empty;
                                        bool isKeyColumn = false;
                                        if (columnIndices.Values.Contains(col))
                                        {
                                            var colInxValue = columnIndices.Values.ToList().FindIndex(a => a == col);
                                            var dataField = schema.GetDataFields()[colInxValue];
                                            //isKeyColumn  = columnIndices.ContainsKey(dataField.Name.Trim()); 
                                            isKeyColumn = uniqueColumns.Any(x => x?.Trim() == dataField?.Name?.Trim());
                                            var columnsKey = columnIndices.Values.FirstOrDefault(x => x == col);
                                            string format = string.Empty;
                                            if (columnsFormat.TryGetValue(columnsKey, out DataTypeDetails DataTypeDetails))
                                            {
                                                colDataType = DataTypeDetails.DataType;
                                                dateFormat = DataTypeDetails.DataTypeFormat;
                                            }
                                        }

                                        var cell = sheet.GetRow(row)?.GetCell(col);
                                        cellValue = cell?.ToString() ?? string.Empty; // In case of null value

                                        if (cell?.CellType == CellType.Numeric)
                                        {
                                            // Check if the cell is formatted as a percentage
                                            string formatString = cell.CellStyle.GetDataFormatString();
                                            if (formatString.Contains("%"))
                                            {
                                                // Convert numeric value to percentage string
                                                cellValue = (cell.NumericCellValue * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
                                                // Console.WriteLine(formatted); // Output: "90%"
                                            }

                                        }

                                        if (cell?.CellType == CellType.Formula)
                                        {
                                            cellValue = cell.StringCellValue;

                                        }
                                        else if (cell != null && cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
                                        {
                                            if ((colDataType == DataTypeOptionsEnum.DATETIME.GetDescription() ||
                                                colDataType == DataTypeOptionsEnum.DATE.GetDescription() ||
                                                colDataType == DataTypeOptionsEnum.TIME.GetDescription())
                                                && !string.IsNullOrEmpty(dateFormat))
                                            {
                                                cellValue = FlpConfigurationHelper.GetCellValue(sheet, cell, colDataType, dateFormat, fileExtention);
                                            }
                                            else
                                            {
                                                var style = cell.CellStyle;
                                                var fmt = style?.GetDataFormatString();

                                                var info = ExcelFormatInspector.Inspect(cell);
                                                if (info != null && info.IsBuiltInFormat && !string.IsNullOrWhiteSpace(info.BuiltinFormatString) && info.IsCustomFormat == false
                                                    && fmt.Contains("d") && fmt.Contains("m") && fmt.Contains("yy") && !fmt.Contains("yyyy"))
                                                {

                                                    var evaluator = workbook.GetCreationHelper().CreateFormulaEvaluator();
                                                    cellValue = ExcelDisplayText.FormatWithFourDigitYear(cell, evaluator, CultureInfo.CurrentCulture);

                                                    if (string.IsNullOrWhiteSpace(cellValue))
                                                    {
                                                        var formatter = new DataFormatter(CultureInfo.CurrentCulture); // or a specific culture
                                                        cellValue = formatter.FormatCellValue(cell);
                                                    }


                                                }
                                                else
                                                {
                                                    var formatter = new DataFormatter(CultureInfo.CurrentCulture); // or a specific culture
                                                    cellValue = formatter.FormatCellValue(cell);
                                                    if (string.IsNullOrWhiteSpace(cellValue))
                                                        cellValue = FlpConfigurationHelper.GetDateTimeCellStringValue(sheet, cell, colDataType, dateFormat, fileExtention);
                                                }

                                            }

                                        }
                                        else if (cell != null && !string.IsNullOrWhiteSpace(cellValue) && DateTime.TryParse(cellValue, out DateTime result))
                                        {


                                            if ((colDataType == DataTypeOptionsEnum.DATETIME.GetDescription() ||
                                               colDataType == DataTypeOptionsEnum.DATE.GetDescription() ||
                                               colDataType == DataTypeOptionsEnum.TIME.GetDescription())
                                               && !string.IsNullOrEmpty(dateFormat))
                                            {
                                                cellValue = FlpConfigurationHelper.GetCellValue(sheet, cell, colDataType, dateFormat, fileExtention);
                                            }
                                            else
                                            {
                                                var style = cell.CellStyle;
                                                var fmt = style?.GetDataFormatString();

                                                var info = ExcelFormatInspector.Inspect(cell);
                                                if (info != null && info.IsBuiltInFormat && !string.IsNullOrWhiteSpace(info.BuiltinFormatString) && info.IsCustomFormat == false
                                                    && fmt.Contains("d") && fmt.Contains("m") && fmt.Contains("yy") && !fmt.Contains("yyyy"))
                                                {

                                                    var evaluator = workbook.GetCreationHelper().CreateFormulaEvaluator();
                                                    cellValue = ExcelDisplayText.FormatWithFourDigitYear(cell, evaluator, CultureInfo.CurrentCulture);

                                                    if (string.IsNullOrWhiteSpace(cellValue))
                                                    {
                                                        var formatter = new DataFormatter(CultureInfo.CurrentCulture); // or a specific culture
                                                        cellValue = formatter.FormatCellValue(cell);
                                                    }


                                                }
                                                else
                                                {
                                                    var formatter = new DataFormatter(CultureInfo.CurrentCulture); // or a specific culture
                                                    cellValue = formatter.FormatCellValue(cell);
                                                    if (string.IsNullOrWhiteSpace(cellValue))
                                                        cellValue = FlpConfigurationHelper.GetDateTimeCellStringValue(sheet, cell, colDataType, dateFormat, fileExtention);
                                                }

                                            }
                                        }

                                        if (isKeyColumn)
                                        {
                                            rowKeyParts.Add(cellValue);
                                        }
                                        rowData.Add(cellValue);

                                    }
                                    string rowKey = string.Join("|", rowKeyParts);
                                    if (!uniqueRows.ContainsKey(rowKey))
                                    {
                                        uniqueRows[rowKey] = rowData;
                                    }
                                    else if (!parameterConfig.KeepFirstRow)
                                    {
                                        uniqueRows[rowKey] = rowData;
                                    }
                                }

                            }


                        }
                        else
                        {
                            columns = new List<List<object>>();
                            for (int col = 0; col <= lastColNum; col++)
                            {
                                columns.Add(new List<object>());
                            }

                            for (int row = startRow; row <= endRow; row++)
                            {

                                rowCountError = row;
                                if (rowCountError == 108760)
                                {

                                }
                                if (parameterConfig.SkipEmptyLines)
                                {
                                    if (IsRowEmpty(sheet.GetRow(row), firstColNum, lastColNum))
                                    {
                                        recordCount--;
                                        continue;
                                    }
                                }


                                //for (int col = firstColNum; col <= lastColNum; col++)
                                //{
                                //    string header = sheet.GetRow(1).GetCell(col).ToString();
                                //    string cellValue = sheet.GetRow(row).GetCell(col).ToString();                               
                                //    object convertedValue = ConvertCellValue(cellValue, columnDataTypeMappings, header);


                                //    columns[col - firstColNum].Add(convertedValue);
                                //}


                                var rowData = new List<string>();
                                for (int col = firstColNum; col <= lastColNum; col++)
                                {
                                    string cellValue = string.Empty;
                                    string colDataType = DataTypeOptionsEnum.STRING.GetDescription();
                                    string dateFormat = string.Empty;
                                    if (columnIndices.Values.Contains(col))
                                    {
                                        var colInxValue = columnIndices.Values.ToList().FindIndex(a => a == col);
                                        var dataField = schema.GetDataFields()[colInxValue];
                                        string format = string.Empty;
                                        var columnsKey = columnIndices.Values.FirstOrDefault(x => x == col);
                                        if (columnsFormat.TryGetValue(columnsKey, out DataTypeDetails DataTypeDetails))
                                        {
                                            colDataType = DataTypeDetails.DataType;
                                            dateFormat = DataTypeDetails.DataTypeFormat;
                                        }
                                    }
                                    var cell = sheet.GetRow(row)?.GetCell(col);
                                    cellValue = cell?.ToString() ?? string.Empty; // In case of null value
                                                                                  // ... load your IWorkbook, ISheet, and get ICell 'cell' ...



                                    // If the cell has a formula and you want the evaluated result:                                  


                                    if (cell?.CellType == CellType.Numeric)
                                    {
                                        // Check if the cell is formatted as a percentage
                                        string formatString = cell.CellStyle.GetDataFormatString();
                                        if (formatString.Contains("%"))
                                        {
                                            // Convert numeric value to percentage string
                                            cellValue = (cell.NumericCellValue * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
                                            // Console.WriteLine(formatted); // Output: "90%"
                                        }

                                    }

                                    if (cell?.CellType == CellType.Formula)
                                    {

                                        cellValue = cell.StringCellValue;

                                    }
                                    else if (cell != null && cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
                                    {
                                        if ((colDataType == DataTypeOptionsEnum.DATETIME.GetDescription() ||
                                            colDataType == DataTypeOptionsEnum.DATE.GetDescription() ||
                                            colDataType == DataTypeOptionsEnum.TIME.GetDescription())
                                            && !string.IsNullOrEmpty(dateFormat))
                                        {
                                            cellValue = FlpConfigurationHelper.GetCellValue(sheet, cell, colDataType, dateFormat, fileExtention);
                                        }
                                        else
                                        {


                                            var style = cell.CellStyle;
                                            var fmt = style?.GetDataFormatString();

                                            var info = ExcelFormatInspector.Inspect(cell);
                                            if (info != null && info.IsBuiltInFormat && !string.IsNullOrWhiteSpace(info.BuiltinFormatString) && info.IsCustomFormat == false
                                                && fmt.Contains("d") && fmt.Contains("m") && fmt.Contains("yy") && !fmt.Contains("yyyy"))
                                            {

                                                var evaluator = workbook.GetCreationHelper().CreateFormulaEvaluator();
                                                cellValue = ExcelDisplayText.FormatWithFourDigitYear(cell, evaluator, CultureInfo.CurrentCulture);

                                                if (string.IsNullOrWhiteSpace(cellValue))
                                                {
                                                    var formatter = new DataFormatter(CultureInfo.CurrentCulture); // or a specific culture
                                                    cellValue = formatter.FormatCellValue(cell);
                                                }


                                            }
                                            else
                                            {
                                                var formatter = new DataFormatter(CultureInfo.CurrentCulture); // or a specific culture
                                                cellValue = formatter.FormatCellValue(cell);
                                                if (string.IsNullOrWhiteSpace(cellValue))
                                                    cellValue = FlpConfigurationHelper.GetDateTimeCellStringValue(sheet, cell, colDataType, dateFormat, fileExtention);
                                            }

                                        }

                                    }
                                    else if (cell != null && !string.IsNullOrWhiteSpace(cellValue) && DateTime.TryParse(cellValue, out DateTime result))
                                    {
                                        if ((colDataType == DataTypeOptionsEnum.DATETIME.GetDescription() ||
                                            colDataType == DataTypeOptionsEnum.DATE.GetDescription() ||
                                            colDataType == DataTypeOptionsEnum.TIME.GetDescription())
                                            && !string.IsNullOrEmpty(dateFormat))
                                        {
                                            cellValue = FlpConfigurationHelper.GetCellValue(sheet, cell, colDataType, dateFormat, fileExtention);
                                        }
                                        else
                                        {
                                            var style = cell.CellStyle;
                                            var fmt = style?.GetDataFormatString();

                                            var info = ExcelFormatInspector.Inspect(cell);
                                            if (info != null && info.IsBuiltInFormat && !string.IsNullOrWhiteSpace(info.BuiltinFormatString) && info.IsCustomFormat == false
                                                && fmt.Contains("d") && fmt.Contains("m") && fmt.Contains("yy") && !fmt.Contains("yyyy"))
                                            {

                                                var evaluator = workbook.GetCreationHelper().CreateFormulaEvaluator();
                                                cellValue = ExcelDisplayText.FormatWithFourDigitYear(cell, evaluator, CultureInfo.CurrentCulture);

                                                if (string.IsNullOrWhiteSpace(cellValue))
                                                {
                                                    var formatter = new DataFormatter(CultureInfo.CurrentCulture); // or a specific culture
                                                    cellValue = formatter.FormatCellValue(cell);
                                                }


                                            }
                                            else
                                            {
                                                var formatter = new DataFormatter(CultureInfo.CurrentCulture); // or a specific culture
                                                cellValue = formatter.FormatCellValue(cell);
                                                if (string.IsNullOrWhiteSpace(cellValue))
                                                    cellValue = FlpConfigurationHelper.GetDateTimeCellStringValue(sheet, cell, colDataType, dateFormat, fileExtention);
                                            }
                                        }

                                    }
                                    columns[col].Add(cellValue);

                                }
                            }

                            using (var rowGroupWriter = parquetWriter.CreateRowGroup())
                            {
                                int rowsCount = 0;
                                for (int col = 0; col < columns.Count; col++)
                                {
                                    if (columnIndices.Values.Contains(col))
                                    {

                                        try
                                        {

                                            var value = columnIndices.Values.ToList().FindIndex(a => a == col);
                                            var dataField = schema.GetDataFields()[value];
                                            var dataArray = columns[col].ToArray();
                                            DataColumn dataColumn;
                                            string format = string.Empty;
                                            var columnsKey = columnIndices.Values.FirstOrDefault(x => x == col);

                                            if (columnsFormat.TryGetValue(columnsKey, out DataTypeDetails dataTypeDetails))
                                            {
                                                format = dataTypeDetails.DataTypeFormat;

                                                if (dataTypeDetails.DataType == "date")
                                                {
                                                    var formatParts = format.Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Length > 0 ? f[0].ToString() : f).ToList(); // Take first character .ToArray();                                               

                                                    sheetFormatList = formats
                                                        .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.DATE)
                                                        .Select(x => x.apiFormat).ToList();

                                                }
                                                else if (dataTypeDetails.DataType == "datetime")
                                                {

                                                    sheetFormatList = formats
                                                        .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.DATETIME)
                                                        .Select(x => x.apiFormat).ToList();
                                                }
                                                else if (dataTypeDetails.DataType == "time")
                                                {
                                                    sheetFormatList = formats
                                                        .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.TIME)
                                                        .Select(x => x.apiFormat).ToList();
                                                    var dateTimeFormatList = formats
                                                  .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.DATETIME)
                                                  .Select(x => x.apiFormat).ToList();
                                                    if (sheetFormatList.Any() && dateTimeFormatList.Any())
                                                    {
                                                        sheetFormatList.AddRange(dateTimeFormatList);
                                                    }

                                                }
                                            }
                                            if (rowsCount == 0)
                                                insertedRows += columns[col].Count;

                                            //DataColumn dataColumn;

                                            dataColumn = ConvertArrayInValidDataType(dataField, dataArray, format, dedupColumnsList.Any(), sheetFormatList);
                                            // Write the chunk of data
                                            await rowGroupWriter.WriteColumnAsync(dataColumn);
                                            rowsCount++;
                                        }
                                        catch (Exception ex)
                                        {

                                            throw new Exception(ex.Message.ToString());
                                        }
                                    }

                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {

                        throw new Exception(ex.Message.ToString());
                    }

                }

                if (parameterConfig.IgnoreDuplicateRows)
                {
                    if (dedupColumnsList.Any())
                    {

                        // Initialize a new dictionary to store deduplicated rows with unique keys
                        var deduplicatedDict = new Dictionary<string, List<object>>();

                        // Iterate over each key-value pair in uniqueRowsWithDedup
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
                                        if (!columnIndices.ContainsKey(col) || columnIndices[col] == -1)
                                            return null; // Column not found, return null

                                        int colIndex = columnIndices[col]; // Get column index
                                        return (colIndex < row.Count) ? row[colIndex] : null; // Fetch value safely
                                    }).ToArray();
                                }, new MultiColumnComparer(false));  // False for Ascending Order
                            }
                            else
                            {

                                orderedRows = rowData.OrderByDescending(row =>
                                {
                                    return dedupColumnsList.Select(col =>
                                    {
                                        if (!columnIndices.ContainsKey(col) || columnIndices[col] == -1)
                                            return null; // Column not found, return null

                                        int colIndex = columnIndices[col]; // Get column index
                                        return (colIndex < row.Count) ? row[colIndex] : null; // Fetch value safely
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
                        //  duplicateRows = recordCount - deduplicatedDict.Values.Count;
                        foreach (var rowData in deduplicatedDict.Values)
                        {

                            for (int col = 0; col < rowData.Count; col++)
                            {

                                columns[col + firstColNum].Add(rowData[col]);

                            }
                        }

                    }
                    else
                    {
                        duplicateRows = recordCount - uniqueRows.Values.Count;
                        foreach (var rowData in uniqueRows.Values)
                        {

                            for (int col = 0; col < rowData.Count; col++)
                            {

                                columns[col + firstColNum].Add(rowData[col]);

                            }
                        }
                    }



                    using (var rowGroupWriter = parquetWriter.CreateRowGroup())
                    {
                        int rowsCount = 0;
                        for (int col = 0; col < columns.Count; col++)
                        {


                            if (columnIndices.Values.Contains(col))
                            {

                                // Determine the correct data type for the DataColumn based on the actual DataField type
                                try
                                {
                                    var value = columnIndices.Values.ToList().FindIndex(a => a == col);
                                    var dataField = schema.GetDataFields()[value];
                                    var dataArray = columns[col].ToArray();
                                    var columnsKey = columnIndices.Values.FirstOrDefault(x => x == col);
                                    string format = string.Empty;

                                    if (columnsFormat.TryGetValue(columnsKey, out DataTypeDetails dataTypeDetails))
                                    {
                                        sheetFormatList = new List<string>();
                                        format = dataTypeDetails.DataTypeFormat;

                                        if (dataTypeDetails.DataType == "date")
                                        {

                                            sheetFormatList = formats
                                               .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.DATE)
                                               .Select(x => x.apiFormat).ToList();

                                        }
                                        else if (dataTypeDetails.DataType == "datetime")
                                        {

                                            sheetFormatList = formats
                                               .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.DATETIME)
                                               .Select(x => x.apiFormat).ToList();
                                        }
                                        else if (dataTypeDetails.DataType == "time")
                                        {
                                            sheetFormatList = formats
                                                .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.TIME)
                                                .Select(x => x.apiFormat).ToList();
                                            var dateTimeFormatList = formats
                                              .Where(x => x.formatDataTypeId == (int)DatatypeNamesEnum.DATETIME)
                                              .Select(x => x.apiFormat).ToList();
                                            if (sheetFormatList.Any() && dateTimeFormatList.Any())
                                            {
                                                sheetFormatList.AddRange(dateTimeFormatList);
                                            }

                                        }
                                    }
                                    if (rowsCount == 0)
                                        insertedRows += columns[col].Count;

                                    DataColumn dataColumn;
                                    dataColumn = ConvertArrayInValidDataType(dataField, dataArray, format, dedupColumnsList.Any(), sheetFormatList);
                                    // Write the chunk of data
                                    await rowGroupWriter.WriteColumnAsync(dataColumn);
                                    rowsCount++;

                                }
                                catch (Exception ex)
                                {

                                    throw new Exception(ex.Message.ToString());
                                }
                            }

                        }

                    }



                }
            }

            success = true;
            return (success, recordCount, insertedRows, duplicateRows, columnDataTypeList, FlpActivityLogStatusEnum.ConversionToParqetFileMovedToParquetLocation);
        }



        private static bool IsRowEmpty(IRow row, int firstColNum, int lastColNum)
        {
            // Check if the row is null (doesn't exist), in which case, we treat it as empty
            if (row == null) return true;

            // Loop through the cells in the row and check for non-empty values
            for (int col = firstColNum; col <= lastColNum; col++)
            {
                // Use MissingCellPolicy.RETURN_BLANK_AS_NULL to handle missing cells
                var cell = row.GetCell(col, MissingCellPolicy.RETURN_BLANK_AS_NULL);

                // Check if the cell is not null and not an empty string or whitespace
                if (cell != null && !string.IsNullOrWhiteSpace(cell.ToString()))
                {
                    return false; // The row is not empty as we found a non-empty cell
                }
            }
            // All cells are empty or null, so the row is considered empty
            return true;
        }

        private DataColumn ConvertArrayInValidDataType(DataField dataField, object[] dataArray, string format, bool dedupCol, List<string> sheetFormatList)
        {
            DataColumn dataColumn = null;

            if (dataField is DataField<int?>)
            {
                dataColumn = new DataColumn(dataField, dataArray.Select(x =>
                {
                    int intValue;
                    return int.TryParse(x?.ToString(), out intValue) ? (int?)intValue : null;
                }).ToArray());
            }
            else if (dataField is DataField<double?>)
            {
                dataColumn = new DataColumn(dataField, dataArray.Select(x =>
                {
                    double doubleValue;
                    return double.TryParse(x?.ToString(), out doubleValue) ? (double?)doubleValue : null;
                }).ToArray());
            }
            else if (dataField is DataField<float?>)
            {
                dataColumn = new DataColumn(dataField, dataArray.Select(x =>
                {
                    float floatValue;
                    return float.TryParse(x?.ToString(), out floatValue) ? (float?)floatValue : null;
                }).ToArray());
            }
            else if (dataField is DataField<bool?>)
            {
                dataColumn = new DataColumn(dataField, dataArray.Select(x =>
                {
                    bool boolValue;
                    return bool.TryParse(x?.ToString(), out boolValue) ? (bool?)boolValue : null;
                }).ToArray());
            }
            else if (dataField is DataField<DateTime?>)
            {
                dataColumn = new DataColumn(dataField, dataArray.Select(x =>
                {
                    DateTime dateTimeValue;
                    return DateTime.TryParseExact(x?.ToString(), sheetFormatList.ToArray(),
                           CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTimeValue)
                           ? (DateTime?)dateTimeValue : null;
                }).ToArray());

            }
            else if (dataField is DataField<DateOnly?>)
            {


                dataColumn = new DataColumn(dataField, dataArray.Select(x =>
                {
                    DateOnly dateOnlyValue;
                    return DateOnly.TryParseExact(x?.ToString(), sheetFormatList.ToArray(),
                           CultureInfo.InvariantCulture, DateTimeStyles.None, out dateOnlyValue)
                           ? (DateOnly?)dateOnlyValue : null;
                }).ToArray());



            }
            else if (dataField is DataField<TimeOnly?>)
            {

                List<string> timeFormats = ParquetSchemaHelper.GetTimeFormats();
                if (!string.IsNullOrWhiteSpace(format) && !timeFormats.Any(x => x == format))
                {
                    timeFormats.Add(format);
                }
                if (sheetFormatList.Any())
                {
                    timeFormats.AddRange(sheetFormatList);
                }
                dataColumn = new DataColumn(dataField, dataArray.Select(x =>
                {

                    return TimeOnly.TryParseExact(x?.ToString(),
                                                      timeFormats.ToArray(),
                                                      System.Globalization.CultureInfo.InvariantCulture,
                                                      System.Globalization.DateTimeStyles.None,
                                                      out TimeOnly timeOnlyValue) ? (TimeOnly?)timeOnlyValue : null;
                }).ToArray());

            }
            //else if (dataField is DataField<long>)
            //{
            //    dataColumn = new DataColumn(dataField, dataArray.Select(x =>
            //    {
            //        long longValue;
            //        return long.TryParse(x?.ToString(), out longValue) ? longValue : default(long);
            //    }).ToArray());


            //}
            else if (dataField is DataField<Int64?>)
            {
                //For long
                dataColumn = new DataColumn(dataField, dataArray.Select(x =>
                {
                    if (x == null) return (long?)null;

                    double doubleValue;
                    string valueStr = x?.ToString();

                    // Try parsing as a double to support scientific notation
                    if (double.TryParse(valueStr, System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out doubleValue))
                    {
                        return (long?)doubleValue; // Convert to long safely
                    }

                    return null; // Return default long (0) if parsing fails
                }).ToArray());
            }
            else // Default to string
            {
                dataColumn = new DataColumn(dataField, dataArray.Select(x => x?.ToString() ?? string.Empty).ToArray());
            }

            return dataColumn;
        }

        

    }
}
