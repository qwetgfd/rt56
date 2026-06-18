using Parquet.Schema;
using Parquet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Parquet.Data;
using Azure.Storage.Blobs;
using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;
using Teleperformance.DataIngestion.Models.DTOs.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Microsoft.Extensions.Logging;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using SMBLibrary.Client;
using SMBLibrary;
using FileAttributes = SMBLibrary.FileAttributes;
using ClosedXML.Excel;
using static Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus.StatusResponseDto;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using DataColumn = Parquet.Data.DataColumn;
using CsvHelper;
using NPOI.SS.Formula.Functions;
using System.Collections;
using System.Globalization;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using AngleSharp.Dom;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingProcess.TableSchemaValidation;
using DocumentFormat.OpenXml;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Helpers;
using Microsoft.IdentityModel.Tokens;

namespace Teleperformance.DataIngestion.DataAccess.Services.v1._0
{
    public class ExcelToParquetConverterService : IExcelToParquetConverterService
    {

        private readonly ILogger<ExcelToParquetConverterService> _logger;
        private readonly ISMBLibraryServices _ismbLibraryServices;
        private readonly IProcessConfigurationRepository _iIProcessConfigurationRepository;
        private readonly ICache _cache;
        public ExcelToParquetConverterService(
           ILogger<ExcelToParquetConverterService> logger, ISMBLibraryServices ismbLibraryServices, IProcessConfigurationRepository iIProcessConfigurationRepository, ICache cache)
        {
            _logger = logger;
            _ismbLibraryServices = ismbLibraryServices;
            _iIProcessConfigurationRepository = iIProcessConfigurationRepository;
            _cache = cache;
        }
        public async Task<ParquetFileResponseDto> ConvertDataToParquet(ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto parameterConfig, FlpProcessTempFile fileProcessingConfig)
        {
            ParquetFileResponseDto resultResponse = null;
            try
            {
                //var sourceFileDetails = parameterConfig.BlobClients;

                // Get the file name from the CSV path
                string fileName = FlpConfigurationHelper.GetParquetFileName(fileProcessingConfig.Name);
                string parquetFilePath = $"{fileProcessingConfig.DestinationFolder}{fileName}";

                BlobServiceClient blobServiceClient = new BlobServiceClient(fileProcessingConfig.ParquetBlobConnectionString);
                BlobContainerClient csvContainerClient = blobServiceClient.GetBlobContainerClient(fileProcessingConfig.BlobContainerName);
                BlobClient csvBlobClient = csvContainerClient.GetBlobClient(fileProcessingConfig.Name);
                string fileExtension = FlpConfigurationHelper.GetFileExtension(fileProcessingConfig.Name);

                // Download the CSV file as a stream
                using (var csvStream = await csvBlobClient.OpenReadAsync())
                {
                    // Get the blob client for the Parquet file
                    BlobClient parquetBlobClient = csvContainerClient.GetBlobClient(parquetFilePath);
                    // Create a stream to write the Parquet file to Blob Storage
                    using (var parquetStream = await parquetBlobClient.OpenWriteAsync(overwrite: true))
                    {
                        // Convert CSV to Parquet
                        (bool ret, int totalRows, int duplicateRows, Dictionary<string, DataTypeDetails> dict) = await ConvertExcelToParquetAsyncV2(csvStream, parquetStream, configurationTableMappingDto, fileExtension);
                        // Reset the memory stream position to the beginning
                        resultResponse = new ParquetFileResponseDto();
                        resultResponse.ParquetFilePath = parquetFilePath;
                        resultResponse.ParquetBlobClient = parquetBlobClient;
                        resultResponse.ParquetBlobClientTemp = csvBlobClient;
                        resultResponse.TotalRows = totalRows;
                        resultResponse.DuplicateRows = duplicateRows;
                        resultResponse.InsertedRows = 0;
                        resultResponse.ParquetFileCreated = ret;
                        resultResponse.ColumnDataTypeList = dict;

                        return resultResponse;
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to convert data to parquet Error: {ex.Message}", $"FlpConfigId: {parameterConfig.FlpConfigurationId}");
                resultResponse = new ParquetFileResponseDto();
                resultResponse.ParquetFileCreated = false;
                resultResponse.ErrorMessage = ex.Message;
                return resultResponse;
            }
        }

        public async Task<ParquetFileResponseDto> ConvertDataToParquetOnPremSharedLocation(string csvTempPath, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto parameterConfig, CheckConnectivitySMBLibraryModel destinationServerModel)
        {
            ParquetFileResponseDto resultResponse = new ParquetFileResponseDto();
            resultResponse.ParquetFileCreated = false;
            SMB2Client smbClient = new SMB2Client();
            ISMBFileStore fileStore = null;
            try
            {
                // Step 1: Generate parquet file name and path
                string fileName = FlpConfigurationHelper.GetParquetFileName(csvTempPath);
                string parquetFilePath = $"{parameterConfig.DestinationPath}{fileName}";
                string fileExtension = FlpConfigurationHelper.GetFileExtension(csvTempPath);

                // Step 2: Connect to the destination SMB server
                (smbClient, fileStore) = _ismbLibraryServices.SMBRequest(destinationServerModel, parameterConfig.FlpConfigurationId);

                // Step 3: Get the CSV stream from the SMB server
                using (var csvStream = _ismbLibraryServices.GetFileStream(fileStore, csvTempPath, parameterConfig.FlpConfigurationId))
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
                            (bool ret, int totalRows, int duplicateRows, Dictionary<string, DataTypeDetails> dict) = await ConvertExcelToParquetAsyncV2(csvStream, parquetStream, configurationTableMappingDto, fileExtension);
                            resultResponse.ParquetFileCreated = ret;
                            resultResponse.TotalRows = totalRows;
                            resultResponse.DuplicateRows = duplicateRows;
                            resultResponse.InsertedRows = 0;
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
                _logger.LogError($"Unable to convert data to parquet Error: {ex.Message}", $"FlpConfigId: {parameterConfig.FlpConfigurationId}");
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




        private async Task<(bool success, int recordCount, int duplicateRows, Dictionary<string, DataTypeDetails>)> ConvertExcelToParquetAsyncV2(Stream excelStream, Stream parquetStream, ConfigurationTableMappingDto parameterConfig, string fileExtention)
        {
            int recordCount = 0;
            int duplicateRows = 0;
            bool success = false;
            // create file schema            
            IWorkbook workbook;
            //IWorkbook workbook = WorkbookFactory.Create(excelStream);
            //// Load workbook based on file extension (.xls or .xlsx)
            if (string.Compare(fileExtention, ".xls", true) == 0)
            {
                workbook = new HSSFWorkbook(excelStream); // Use HSSF for .xls files
            }
            else
            {
                workbook = new XSSFWorkbook(excelStream); // Use XSSF for .xlsx files
            }
            var sheet = workbook.GetSheetAt(0); // Get the first sheet
            int firstRowNum = sheet.FirstRowNum;
            int lastRowNum = sheet.LastRowNum;
            var headerRowsStarted = parameterConfig.SkipRows;
            IRow firstRow = sheet.GetRow(headerRowsStarted);
            int firstColNum = firstRow.FirstCellNum;
            int lastColNum = firstRow.LastCellNum - 1;
            bool hasDuplicateOrEmptyHeader = false;
            List<string> filesHeaders = new List<string>();

            var fields = new List<DataField>();
            var formats = await _cache.GetFormatListAsync();
            // parameterConfig.ConvertDataTypeColumnNameList = $"ID=string,NAME=string,DATE=date|2,Format=time|6";
            var (convertDatatypeString, columnDataTypeMappings) = FlpConfigurationHelper.GetColumnDataTypeList(parameterConfig.ConvertDataTypeColumnNameList);
            var columnIndices = new Dictionary<string, int>();
            var columnsFormat = new Dictionary<int, DataTypeDetails>();
            var columnDataTypeList = new Dictionary<string, DataTypeDetails>();
            // var uniqueColumns = FlpConfigurationHelper.GetKeyColumnNamesList(parameterConfig.KeyColumnList);
            var uniqueColumns = FlpConfigurationHelper.SplitString(parameterConfig.KeyColumnList, ",");
            var dedupColumnsList = FlpConfigurationHelper.SplitString(parameterConfig.OrderByColumnListForDedup, ",");

            List<string> columnNameList = new List<string>();
            bool createFileColumn = false;
            (createFileColumn, columnNameList) = FlpConfigurationHelper.ValidateFileSchema(parameterConfig.ValidateFileSchema, parameterConfig.ColumnNameList, parameterConfig.IsHeaderProvided);

            // Handling headers
            if (parameterConfig.IsHeaderProvided)
            {
                IRow headerRow = sheet.GetRow(headerRowsStarted);

                //Provided:- Header  and custom column name list 
                if (columnNameList != null && columnNameList.Any())
                {
                    filesHeaders = headerRow.Cells.Select(cell => FlpConfigurationHelper.CleanColumnName(cell.ToString())).ToList();
                    hasDuplicateOrEmptyHeader = FlpConfigurationHelper.ContainsDuplicatesOrEmptyHeader(filesHeaders);
                    if (hasDuplicateOrEmptyHeader)
                    {
                        filesHeaders = FlpConfigurationHelper.CreateCustomHeader(filesHeaders);
                    }
                    foreach (var colName in columnNameList)
                    {
                        DataTypeDetails dataTypeDetails = new DataTypeDetails();
                        string cleanedName = colName;// FlpConfigurationHelper.CleanColumnName(colName.Trim());
                        string format = string.Empty;

                        if (columnDataTypeMappings != null && columnDataTypeMappings.TryGetValue(cleanedName, out string overrideType))
                        {
                            string dataType = overrideType;

                            if (overrideType.Contains("|"))
                            {
                                var arr = FlpConfigurationHelper.SplitString(overrideType, "|");
                                if (arr != null && arr.Count == 2)
                                {
                                    dataType = arr?.FirstOrDefault() ?? "";
                                    int formatId = Convert.ToInt32(arr.LastOrDefault());
                                    if (formatId > 0)
                                    {
                                        format = formats?.FirstOrDefault(fv => fv.formatId == formatId).apiFormat ?? "";
                                    }
                                }
                            }

                            dataTypeDetails.DataType = dataType;
                            dataTypeDetails.DataTypeFormat = format;
                            fields.Add(CreateDataField(cleanedName, dataType));

                        }
                        else
                        {
                            dataTypeDetails.DataType = "string";
                            fields.Add(new DataField<string>(cleanedName));
                        }

                        int colIndex = -1;

                        for (int col = firstColNum; col <= lastColNum; col++)
                        {
                            //string header1 = headerRow.GetCell(col).ToString();
                            string header = filesHeaders[col];
                            //header = FlpConfigurationHelper.CleanColumnName(header);
                            if (string.Compare(header.Trim(), cleanedName, false) == 0)
                            {
                                colIndex = col;
                                break;
                            }
                        }
                        columnIndices[cleanedName] = colIndex;
                        columnDataTypeList[cleanedName] = dataTypeDetails;
                        columnsFormat[colIndex] = dataTypeDetails;
                    }
                }
                else
                {

                    filesHeaders = headerRow.Cells.Select(cell => FlpConfigurationHelper.CleanColumnName(cell.ToString())).ToList();
                    hasDuplicateOrEmptyHeader = FlpConfigurationHelper.ContainsDuplicatesOrEmptyHeader(filesHeaders);
                    if (hasDuplicateOrEmptyHeader)
                    {
                        filesHeaders = FlpConfigurationHelper.CreateCustomHeader(filesHeaders);
                    }
                    int colIndex = 0;
                    foreach (var colName in filesHeaders)
                    {
                        DataTypeDetails dataTypeDetails = new DataTypeDetails();
                        string cleanedName = colName;// FlpConfigurationHelper.CleanColumnName(colName.Trim());
                        string format = string.Empty;

                        if (columnDataTypeMappings != null && columnDataTypeMappings.TryGetValue(cleanedName, out string overrideType))
                        {
                            string dataType = overrideType;

                            if (overrideType.Contains("|"))
                            {
                                var arr = FlpConfigurationHelper.SplitString(overrideType, "|");
                                if (arr != null && arr.Count == 2)
                                {
                                    dataType = arr?.FirstOrDefault() ?? "";
                                    int formatId = Convert.ToInt32(arr.LastOrDefault());
                                    if (formatId > 0)
                                    {
                                        format = formats?.FirstOrDefault(fv => fv.formatId == formatId).apiFormat ?? "";
                                    }
                                }
                            }

                            dataTypeDetails.DataType = dataType;
                            dataTypeDetails.DataTypeFormat = format;
                            fields.Add(CreateDataField(cleanedName, dataType));

                        }
                        else
                        {
                            dataTypeDetails.DataType = "string";
                            fields.Add(new DataField<string>(cleanedName));
                        }

                        columnIndices[cleanedName] = colIndex;
                        columnDataTypeList[cleanedName] = dataTypeDetails;
                        columnsFormat[colIndex] = dataTypeDetails;
                        colIndex++;
                    }
                    //We can skip rows 
                    //for (int col = firstColNum; col <= lastColNum; col++)
                    //{
                    //    string header = headerRow.GetCell(col).ToString();
                    //    header = FlpConfigurationHelper.CleanColumnName(header);
                    //    fields.Add(new DataField<string>(header));
                    //    DataTypeDetails dataTypeDetails = new DataTypeDetails();
                    //    dataTypeDetails.DataType = "string";
                    //    dataTypeDetails.DataTypeFormat = "";

                    //    //Fill dictionary
                    //    columnIndices[header] = col;
                    //    columnDataTypeList[header] = dataTypeDetails;
                    //    columnsFormat[col] = dataTypeDetails;

                    //}
                }

            }
            else
            {
                firstRowNum = 0;

                //Not Provided Header but Provided columnnameList
                if (!string.IsNullOrWhiteSpace(parameterConfig.ColumnNameList))
                {
                    if (!parameterConfig.ColumnNameList.Contains("="))
                    {
                        throw new InvalidDataException("Not found index format with column name list for custom header");
                    }
                    var customHeadesWithIndexes = FlpConfigurationHelper.GetCustomHeaersIndex(parameterConfig.ColumnNameList);
                    List<string> flpCustomHeaderList = new List<string>();
                    //Create Custom Header 
                    for (int col = firstColNum; col <= lastColNum; col++)
                    {
                        string cleanedColName = FlpConfigurationHelper.CleanColumnName($"col{col}");
                        flpCustomHeaderList.Add(cleanedColName);
                    }
                    foreach (var (key, value) in customHeadesWithIndexes)
                    {
                        string transformedColumnName = FlpConfigurationHelper.CleanColumnName(key);
                        DataTypeDetails dataTypeDetails = new DataTypeDetails();
                        string format = "";
                        //if (columnDataTypeMappings != null && columnDataTypeMappings.TryGetValue(transformedColumnName, out string overrideType))
                        //{
                        //    fields.Add(CreateDataField(transformedColumnName, overrideType));
                        //}
                        //else
                        //{
                        //    fields.Add(new DataField<string>(transformedColumnName));
                        //}

                        if (columnDataTypeMappings != null && columnDataTypeMappings.TryGetValue(transformedColumnName, out string overrideType))
                        {
                            string dataType = overrideType;

                            if (overrideType.Contains("|"))
                            {
                                var arr = FlpConfigurationHelper.SplitString(overrideType, "|");
                                if (arr != null && arr.Count == 2)
                                {
                                    dataType = arr?.FirstOrDefault() ?? "";
                                    int formatId = Convert.ToInt32(arr.LastOrDefault());
                                    if (formatId > 0)
                                    {
                                        format = formats?.FirstOrDefault(fv => fv.formatId == formatId).apiFormat ?? "";

                                    }
                                }
                            }

                            dataTypeDetails.DataType = dataType;
                            fields.Add(CreateDataField(transformedColumnName, dataType));

                        }
                        else
                        {
                            dataTypeDetails.DataType = "string";
                            fields.Add(new DataField<string>(transformedColumnName));
                        }

                        int colIndex = -1;
                        for (int col = firstColNum; col <= flpCustomHeaderList.Count; col++)
                        {

                            if (string.Compare(flpCustomHeaderList[col], transformedColumnName, false) == 0)
                            {
                                colIndex = col;
                                break;
                            }
                        }
                        columnIndices[transformedColumnName] = colIndex;
                        columnDataTypeList[transformedColumnName] = dataTypeDetails;
                        columnsFormat[colIndex] = dataTypeDetails;
                    }

                }
                else
                {
                    for (int col = firstColNum; col <= lastColNum; col++)
                    {
                        string cleanedName = FlpConfigurationHelper.CleanColumnName($"col{col - firstColNum}");
                        fields.Add(new DataField<string>(cleanedName));
                        DataTypeDetails dataTypeDetails = new DataTypeDetails();
                        columnIndices[cleanedName] = col;
                        columnDataTypeList[cleanedName] = dataTypeDetails;
                        columnsFormat[col] = dataTypeDetails;
                    }
                }
            }

            // Create Parquet schema
            var schema = new ParquetSchema(fields.ToArray());

            using (var parquetWriter = await ParquetWriter.CreateAsync(schema, parquetStream))
            {
                int chunkSize = 10000; // Chunk size
                int headerRows = headerRowsStarted + (parameterConfig.IsHeaderProvided ? 1 : 0);
                // int headerRows = headerRowsStarted + (parameterConfig.IsHeaderProvided ? 1 : 0);
                int startingRows = firstRowNum + headerRows;// headerRowsStarted + parameterConfig.SkipRows;
                int lastRows = lastRowNum - parameterConfig.SkipFooterRows;
                var columns = new List<List<object>>();
                var duplicateRowList = new List<List<object>>();
                for (int col = firstColNum; col <= lastColNum; col++)
                {
                    columns.Add(new List<object>());
                }
                var uniqueRows = new Dictionary<string, List<string>>();
                var uniqueRowsWithDedup = new Dictionary<string, List<List<object>>>();
                for (int startRow = startingRows; startRow <= lastRows; startRow += chunkSize)
                {
                    int endRow = Math.Min(startRow + chunkSize - 1, lastRows);
                    recordCount += endRow - startRow + 1;


                    if (parameterConfig.IgnoreDuplicateRows)
                    {

                        for (int row = startRow; row <= endRow; row++)
                        {
                            if (IsRowEmpty(sheet.GetRow(row), firstColNum, lastColNum))
                            {
                                recordCount--;
                                continue;
                            }

                            var rowKeyParts = new List<string>();
                            if (dedupColumnsList.Any())
                            {
                                foreach (var uniqueColumn in uniqueColumns)
                                {
                                    int colIndex = columnIndices[uniqueColumn];
                                    //string cellValue = sheet.GetRow(row).GetCell(colIndex).ToString();
                                    string cellValue = sheet.GetRow(row)?.GetCell(colIndex)?.ToString() ?? string.Empty;
                                    rowKeyParts.Add(cellValue);
                                }

                                string rowKey = string.Join("|", rowKeyParts);
                                var rowData = new List<object>();
                                for (int col = firstColNum; col <= lastColNum; col++)
                                {

                                    object value = sheet.GetRow(row)?.GetCell(col); // Handle potential null valuesvalues
                                    //string value1 = sheet.GetRow(row)?.GetCell(col)?.ToString() ?? string.Empty; // Handle potential null valuesvalues

                                    if (value != null && columnIndices.Values.Contains(col))
                                    {

                                        try
                                        {
                                            var colInxValue = columnIndices.Values.ToList().FindIndex(a => a == col);
                                            var dataField = schema.GetDataFields()[colInxValue];

                                            if (columnsFormat.TryGetValue(colInxValue, out DataTypeDetails DataTypeDetails))
                                            {
                                                string dataType = DataTypeDetails.DataType;
                                                var cell = sheet.GetRow(row)?.GetCell(col);
                                                var formatIndex = cell.CellStyle.DataFormat;
                                                var formatString = sheet.Workbook.GetCreationHelper().CreateDataFormat().GetFormat(formatIndex);

                                                if (dataType == DataTypeOptionsEnum.DATETIME.GetDescription() ||
                                                    dataType == DataTypeOptionsEnum.DATE.GetDescription() ||
                                                    dataType == DataTypeOptionsEnum.TIME.GetDescription())
                                                {

                                                    if (cell?.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
                                                    {
                                                        //var formatIndex = cell.CellStyle.DataFormat;
                                                        //var formatString = sheet.Workbook.GetCreationHelper().CreateDataFormat().GetFormat(formatIndex);
                                                        //if (!string.IsNullOrWhiteSpace(formatString))
                                                        //    formatString = FlpConfigurationHelper.ValidDateFormat(formatString);

                                                        DateTime? date = cell.DateCellValue;
                                                        value = date?.ToString(DataTypeDetails.DataTypeFormat);
                                                    }
                                                }
                                                else
                                                {
                                                    //Set string value for datetime format
                                                    if (cell?.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
                                                    {
                                                        value = cell.DateOnlyCellValue?.ToString();
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
                                                                    DataTypeDetails.DataTypeFormat,
                                                                   System.Globalization.CultureInfo.InvariantCulture,
                                                                   System.Globalization.DateTimeStyles.None,
                                                                   out DateTime datetimeValue) ? datetimeValue : (DateTime?)null,

                                                    // For handling DateOnly with custom formats
                                                    "date" => DateOnly.TryParseExact(value.ToString(),
                                                                DataTypeDetails.DataTypeFormat,
                                                                System.Globalization.CultureInfo.InvariantCulture,
                                                                System.Globalization.DateTimeStyles.None,
                                                                out DateOnly dateOnlyValue) ? (DateOnly?)dateOnlyValue : null,

                                                    // For handling TimeOnly with custom formats
                                                    "time" => TimeOnly.TryParseExact(value.ToString(),
                                                                DataTypeDetails.DataTypeFormat,
                                                                System.Globalization.CultureInfo.InvariantCulture,
                                                                System.Globalization.DateTimeStyles.None,
                                                                out TimeOnly timeOnlyValue) ? (TimeOnly?)timeOnlyValue : null,

                                                    _ => value.ToString()
                                                };
                                            }

                                        }
                                        catch (Exception ex)
                                        {

                                            throw new Exception(ex.Message.ToString());
                                        }
                                    }

                                    rowData.Add(value);

                                }
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
                                foreach (var uniqueColumn in uniqueColumns)
                                {
                                    int colIndex = columnIndices[uniqueColumn];
                                    string cellValue = sheet.GetRow(row)?.GetCell(colIndex)?.ToString() ?? string.Empty;
                                    rowKeyParts.Add(cellValue);
                                }

                                string rowKey = string.Join("|", rowKeyParts);

                                var rowData = new List<string>();
                                for (int col = firstColNum; col <= lastColNum; col++)
                                {
                                    //var cell = sheet.GetRow(row)?.GetCell(col);
                                    string cellValue = string.Empty;
                                    string colDataType = DataTypeOptionsEnum.STRING.GetDescription();
                                    string dateFormat = string.Empty;
                                    if (columnIndices.Values.Contains(col))
                                    {
                                        var colInxValue = columnIndices.Values.ToList().FindIndex(a => a == col);
                                        var dataField = schema.GetDataFields()[colInxValue];
                                        string format = string.Empty;
                                        if (columnsFormat.TryGetValue(colInxValue, out DataTypeDetails DataTypeDetails))
                                        {
                                            colDataType = DataTypeDetails.DataType;
                                            dateFormat = DataTypeDetails.DataTypeFormat;
                                        }
                                    }
                                    //var cell = sheet.GetRow(row)?.GetCell(col);
                                    //cellValue = cell?.ToString() ?? string.Empty;//IN case null value
                                    //if (cell != null)
                                    //{
                                    //    if (((string.Compare(colDataType, DataTypeOptionsEnum.DATETIME.GetDescription(), false) == 0) || (string.Compare(colDataType, DataTypeOptionsEnum.DATE.GetDescription(), false) == 0) || (string.Compare(colDataType, DataTypeOptionsEnum.TIME.GetDescription(), false) == 0)) && (cell?.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell)))
                                    //    {
                                    //        DateTime? date = cell.DateCellValue;
                                    //        var value = date?.ToString(dateFormat);
                                    //        cellValue = value ?? string.Empty;
                                    //    }
                                    //    else if (cell?.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
                                    //    {
                                    //        DateTime? date = cell.DateCellValue;
                                    //        cellValue = date?.ToString();
                                    //    }

                                    //}                                   

                                    var cell = sheet.GetRow(row)?.GetCell(col);
                                    cellValue = cell?.ToString() ?? string.Empty; // In case of null value

                                    if (cell != null && cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
                                    {

                                        var formatIndex = cell.CellStyle.DataFormat;
                                        var formatString = sheet.Workbook.GetCreationHelper().CreateDataFormat().GetFormat(formatIndex);
                                        if (!string.IsNullOrWhiteSpace(formatString))
                                            formatString = FlpConfigurationHelper.ValidDateFormat(formatString);
                                        DateTime? date = cell.DateCellValue;
                                        if (date != null)
                                        {
                                            // Check for specific data type and format date accordingly
                                            if ((colDataType == DataTypeOptionsEnum.DATETIME.GetDescription() ||
                                                 colDataType == DataTypeOptionsEnum.DATE.GetDescription() ||
                                                 colDataType == DataTypeOptionsEnum.TIME.GetDescription()) && !string.IsNullOrEmpty(dateFormat))
                                            {
                                                cellValue = date.Value.ToString(dateFormat);
                                            }
                                            else if (formatString.Contains("h") || formatString.Contains("H"))
                                            {

                                                // Likely a time format                                         
                                                var timeOfDay = date.Value.TimeOfDay.ToString();
                                                cellValue = timeOfDay?.ToString() ?? string.Empty;
                                            }
                                            else
                                            {

                                                if (!string.IsNullOrWhiteSpace(formatString))
                                                    cellValue = date.Value.ToString(formatString);
                                                else
                                                    cellValue = date.Value.ToString();


                                            }
                                        }
                                    }

                                    rowData.Add(cellValue);

                                }
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
                        for (int col = firstColNum; col <= lastColNum; col++)
                        {
                            columns.Add(new List<object>());
                        }
                        for (int row = startRow; row <= endRow; row++)
                        {

                            if (IsRowEmpty(sheet.GetRow(row), firstColNum, lastColNum))
                            {
                                recordCount--;
                                continue;
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
                                    if (columnsFormat.TryGetValue(colInxValue, out DataTypeDetails DataTypeDetails))
                                    {
                                        colDataType = DataTypeDetails.DataType;
                                        dateFormat = DataTypeDetails.DataTypeFormat;
                                    }
                                }
                                var cell = sheet.GetRow(row)?.GetCell(col);
                                cellValue = cell?.ToString() ?? string.Empty; // In case of null value

                                if (cell != null && cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
                                {
                                    DateTime? date = cell.DateCellValue;
                                    if (date != null)
                                    {
                                        var formatIndex = cell.CellStyle.DataFormat;
                                        var formatString = sheet.Workbook.GetCreationHelper().CreateDataFormat().GetFormat(formatIndex);
                                        if (!string.IsNullOrWhiteSpace(formatString))
                                            formatString = FlpConfigurationHelper.ValidDateFormat(formatString);

                                        // Check for specific data type and format date accordingly
                                        if ((colDataType == DataTypeOptionsEnum.DATETIME.GetDescription() ||
                                             colDataType == DataTypeOptionsEnum.DATE.GetDescription() ||
                                             colDataType == DataTypeOptionsEnum.TIME.GetDescription()))
                                        {

                                            cellValue = date.Value.ToString(dateFormat);
                                        }
                                        else if (formatString.Contains("h") || formatString.Contains("H"))
                                        {

                                            // Likely a time format                                         
                                            var timeOfDay = date.Value.TimeOfDay.ToString();
                                            cellValue = timeOfDay?.ToString() ?? string.Empty;
                                        }
                                        else
                                        {

                                            //if (!string.IsNullOrWhiteSpace(formatString))
                                            //    cellValue = date.Value.ToString(formatString);
                                            //else
                                            //    cellValue = date.Value.ToString();
                                            cellValue = cell.DateOnlyCellValue?.ToString();


                                        }

                                    }
                                }
                                columns[col - firstColNum].Add(cellValue);

                            }
                        }

                        using (var rowGroupWriter = parquetWriter.CreateRowGroup())
                        {
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
                                        if (columnsFormat.TryGetValue(value, out DataTypeDetails dataTypeDetails))
                                        {
                                            format = dataTypeDetails.DataTypeFormat;
                                        }

                                        //DataColumn dataColumn;

                                        dataColumn = ConvertArrayInValidDataType(dataField, dataArray, format, dedupColumnsList.Any());
                                        // Write the chunk of data
                                        await rowGroupWriter.WriteColumnAsync(dataColumn);
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

                            // Initialize the sorting
                            IOrderedEnumerable<List<object>> orderedRows = null;
                            for (int i = 0; i < dedupColumnsList.Count; i++)
                            {
                                string col = dedupColumnsList[i].ToString();

                                // Try to get the column index for each column name in dedupColumnsList
                                if (columnIndices.TryGetValue(col, out int colIndex))
                                {
                                    // Order rowData by the specified column in descending order
                                    if (i == 0)
                                    {
                                        orderedRows = parameterConfig.KeepFirstRow ? rowData.OrderBy(row => row[colIndex]) : rowData.OrderByDescending(row => row[colIndex]);
                                    }
                                    else
                                    {
                                        orderedRows = parameterConfig.KeepFirstRow ? orderedRows.ThenBy(row => row[colIndex]) : orderedRows.ThenByDescending(row => row[colIndex]);
                                    }
                                }
                            }

                            // Take only the first row after sorting (deduplication)
                            var deduplicatedRow = orderedRows?.FirstOrDefault();
                            if (deduplicatedRow != null)
                            {
                                // Add the deduplicated row to the new dictionary with the same unique key
                                deduplicatedDict[key] = deduplicatedRow;
                            }
                        }

                        // Now, deduplicatedDict contains each key with only one deduplicated row
                        // You can proceed to use deduplicatedDict as needed
                        duplicateRows = recordCount - deduplicatedDict.Values.Count;
                        foreach (var rowData in deduplicatedDict.Values)
                        {

                            for (int col = 0; col < rowData.Count; col++)
                            {

                                columns[col].Add(rowData[col]);

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

                                columns[col].Add(rowData[col]);

                            }
                        }
                    }



                    using (var rowGroupWriter = parquetWriter.CreateRowGroup())
                    {
                        int totalRows = columns[0].Count; // Assuming all columns have the same number of rows
                        int totalChunks = (totalRows + chunkSize - 1) / chunkSize; // Calculate number of chunks

                        for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
                        {
                            int startRow = chunkIndex * chunkSize;
                            int endRow = Math.Min(startRow + chunkSize, totalRows); // Ensure last chunk is smaller if needed

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

                                        string format = string.Empty;
                                        if (columnsFormat.TryGetValue(value, out DataTypeDetails dataTypeDetails))
                                        {
                                            format = dataTypeDetails.DataTypeFormat;
                                        }

                                        DataColumn dataColumn;
                                        dataColumn = ConvertArrayInValidDataType(dataField, dataArray, format, dedupColumnsList.Any());
                                        // Write the chunk of data
                                        await rowGroupWriter.WriteColumnAsync(dataColumn);

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
            }

            success = true;
            return (success, recordCount, duplicateRows, columnDataTypeList);
        }


        private static DataField CreateDataField(string columnName, string dataType)
        {
            return dataType.ToLower() switch
            {
                "string" => new DataField<string?>(columnName),
                "int" => new DataField<int?>(columnName),
                "long" => new DataField<long?>(columnName),
                "float" => new DataField<float?>(columnName),
                "double" => new DataField<double?>(columnName),
                "bool" => new DataField<bool?>(columnName),
                "datetime" => new DataField<DateTime?>(columnName),   // For DateTime
                "date" => new DataField<DateOnly?>(columnName),       // Nullable DateOnly
                "time" => new DataField<TimeOnly?>(columnName),       // Nullable TimeOnly
                _ => new DataField<string>(columnName)                // Default to string if no match
            };
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




        private DataColumn ConvertArrayInValidDataType(DataField dataField, object[] dataArray, string format, bool dedupCol)
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
                    return DateTime.TryParseExact(x?.ToString(), format,
                           CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTimeValue)
                           ? (DateTime?)dateTimeValue : null;
                }).ToArray());

            }
            else if (dataField is DataField<DateOnly?>)
            {


                dataColumn = new DataColumn(dataField, dataArray.Select(x =>
                {
                    DateOnly dateOnlyValue;
                    return DateOnly.TryParseExact(x?.ToString(), format,
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
                dataColumn = new DataColumn(dataField, dataArray.Select(x =>
                {

                    return TimeOnly.TryParseExact(x?.ToString(),
                                                      timeFormats.ToArray(),
                                                      System.Globalization.CultureInfo.InvariantCulture,
                                                      System.Globalization.DateTimeStyles.None,
                                                      out TimeOnly timeOnlyValue) ? (TimeOnly?)timeOnlyValue : null;
                }).ToArray());

            }
            else if (dataField is DataField<long>)
            {
                dataColumn = new DataColumn(dataField, dataArray.Select(x =>
                {
                    long longValue;
                    return long.TryParse(x?.ToString(), out longValue) ? longValue : default(long);
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
