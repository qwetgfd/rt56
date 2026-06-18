using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using SMBLibrary.Client;
using SMBLibrary;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using FileAttributes = SMBLibrary.FileAttributes;
using CsvHelper.Configuration;
using CsvHelper;
using Parquet.Schema;
using Parquet;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Microsoft.IdentityModel.Tokens;
using System.Net.Security;
using DocumentFormat.OpenXml.Drawing;
using NPOI.OpenXmlFormats;
using System.Dynamic;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Parquet.Data;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Helpers;
using NPOI.OpenXmlFormats.Spreadsheet;
using NPOI.OpenXmlFormats.Dml.Diagram;

namespace Teleperformance.DataIngestion.DataAccess.Services.v1._0
{
    public class TxtToParquetConverterService : ITxtToParquetConverterService
    {
        private readonly ILogger<TxtToParquetConverterService> _logger;
        private readonly ISMBLibraryServices _ismbLibraryServices;
        private readonly IProcessConfigurationRepository _iIProcessConfigurationRepository;
        private readonly ICache _cache;
        public TxtToParquetConverterService(
           ILogger<TxtToParquetConverterService> logger, ISMBLibraryServices ismbLibraryServices, IProcessConfigurationRepository iIProcessConfigurationRepository, ICache cache)
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
                string fileName = FlpConfigurationHelper.GetParquetFileName(fileProcessingConfig.Name);
                // Define the Parquet file path in the Blob Storage
                string parquetFilePath = $"{fileProcessingConfig.DestinationFolder}{fileName}";
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
                        resultResponse = await ConvertTxtToParquetAsync(txtStream, parquetStream, configurationTableMappingDto);
                        resultResponse.ParquetFilePath = parquetFilePath;
                        resultResponse.ParquetBlobClient = parquetBlobClient;
                        resultResponse.ParquetBlobClientTemp = txtBlobClient;

                        return resultResponse;
                    }



                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to convert data to parquet Error: {ex.Message}", $"FlpConfigId: {parameterConfig.FlpConfigurationId}");
                resultResponse = new ParquetFileResponseDto();
                resultResponse.ErrorMessage = ex.Message.ToString();
                resultResponse.ParquetFileCreated = false;
                return resultResponse;
            }
        }


        public async Task<ParquetFileResponseDto> ConvertDataToParquetOnPremSharedLocation(string txtTempPath, ConfigurationTableMappingDto configurationTableMappingDto, FlpConfigurationResponseDto parameterConfig, CheckConnectivitySMBLibraryModel destinationServerModel)
        {
            ParquetFileResponseDto resultResponse = new ParquetFileResponseDto();
            resultResponse.ParquetFileCreated = false;
            SMB2Client smbClient = new SMB2Client();
            ISMBFileStore fileStore = null;

            try
            {

                // Step 1: Generate parquet file name and path
                string fileName = FlpConfigurationHelper.GetParquetFileName(txtTempPath);
                string parquetFilePath = $"{parameterConfig.DestinationPath}{fileName}";

                // Step 2: Connect to the destination SMB server
                (smbClient, fileStore) = _ismbLibraryServices.SMBRequest(destinationServerModel, parameterConfig.FlpConfigurationId);

                // Step 3: Get the CSV stream from the SMB server
                using (var txtStream = _ismbLibraryServices.GetFileStream(fileStore, txtTempPath, parameterConfig.FlpConfigurationId))
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
                            resultResponse = await ConvertTxtToParquetAsync(txtStream, parquetStream, configurationTableMappingDto);
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


        private async Task<ParquetFileResponseDto> ConvertTxtToParquetAsync(Stream txtStream, Stream parquetStream, ConfigurationTableMappingDto parameterConfig)
        {


            var resultResponse = new ParquetFileResponseDto();
            if (string.Compare(parameterConfig.Delimiter, "\\t", true) == 0)
            {
                parameterConfig.Delimiter = FlpConfigurationHelper.EscapedTabString(parameterConfig.Delimiter.Trim());
            }
            var isValidFile = IsValidDelimiter(txtStream, parameterConfig.Delimiter);
            if (!isValidFile)
            {
                throw new InvalidOperationException("Unable to determine the CSV delimiter.");
            }


            List<string> columnNameList = new List<string>();
            bool createFileColumn = false;
            (createFileColumn, columnNameList) = FlpConfigurationHelper.ValidateFileSchema(parameterConfig.ValidateFileSchema, parameterConfig.ColumnNameList, parameterConfig.IsHeaderProvided);
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = parameterConfig.Delimiter,
                Quote = Convert.ToChar(parameterConfig.QuoteCharacter),
                IgnoreBlankLines = true,
                HasHeaderRecord = parameterConfig.IsHeaderProvided
            };


            using (var reader = new StreamReader(txtStream))
            using (var csv = new CsvReader(reader, csvConfig))
            {

                // Skip initial rows
                for (int i = 0; i < parameterConfig.SkipRows; i++)
                {
                    csv.Read();
                }
                // Read the remaining rows into a list
                bool hasDuplicateOrEmptyHeader = false;
                List<string> headerRow = new List<string>();
                List<string> filesHeaders = new List<string>();
                var formats = await _cache.GetFormatListAsync();
                resultResponse.ColumnDataTypeList = new Dictionary<string, DataTypeDetails>();
                //parameterConfig.ConvertDataTypeColumnNameList = $"ID=int,uniquId=int,NAME=string,DATE=date|2,FORMAT=time|6";
                Dictionary<string, string> originalToTransformedColumnNameMap = new Dictionary<string, string>();
                var dedupColumnsList = FlpConfigurationHelper.SplitString(parameterConfig.OrderByColumnListForDedup, ",");
                if (!string.IsNullOrWhiteSpace(parameterConfig.KeyColumnList))
                {
                    parameterConfig.KeyColumnList = parameterConfig.KeyColumnList.ToLower();
                }
                var (convertDatatypeString, columnDataTypeMappings) = FlpConfigurationHelper.GetColumnDataTypeList(parameterConfig.ConvertDataTypeColumnNameList);
                var customHeadersColumnsMapping = new Dictionary<string, string>();

                //custom header not  created 
                if (parameterConfig.IsHeaderProvided && !columnNameList.Any())
                {
                    //Validate schema false
                    csv.Read();
                    csv.ReadHeader();
                    filesHeaders = csv.Context.Reader.HeaderRecord.ToList();
                    hasDuplicateOrEmptyHeader = FlpConfigurationHelper.ContainsDuplicatesOrEmptyHeader(filesHeaders);
                    if (hasDuplicateOrEmptyHeader)
                    {
                        filesHeaders = FlpConfigurationHelper.CreateCustomHeader(filesHeaders);
                        for (int i = 0; i < filesHeaders.Count(); i++)
                        {

                            var item = filesHeaders[i];
                            headerRow.Add(item);
                            string transformedColumnName = FlpConfigurationHelper.CleanColumnName(item);
                            // var findValue = headerRow.FirstOrDefault(x => x == transformedColumnName);
                            customHeadersColumnsMapping.Add(item, transformedColumnName);
                        }

                    }
                    else
                    {
                        headerRow = filesHeaders.ToList();
                    }

                }
                else if (columnNameList != null && columnNameList.Any())
                {
                    csv.Read();
                    if (parameterConfig.IsHeaderProvided)
                    {
                        foreach (var columnName in columnNameList)
                        {
                            //string transformedColumnName = FlpConfigurationHelper.CleanColumnName(columnName);
                            headerRow.Add(columnName);
                        }
                        csv.ReadHeader();
                        filesHeaders = csv.Context.Reader.HeaderRecord.ToList();
                        hasDuplicateOrEmptyHeader = FlpConfigurationHelper.ContainsDuplicatesOrEmptyHeader(filesHeaders);
                        if (hasDuplicateOrEmptyHeader)
                        {
                            filesHeaders = FlpConfigurationHelper.CreateCustomHeader(filesHeaders);
                            foreach (var item in filesHeaders)
                            {
                                string transformedColumnName = FlpConfigurationHelper.CleanColumnName(item);
                                var findValue = headerRow.FirstOrDefault(x => x == transformedColumnName);
                                if (findValue != null)
                                {
                                    customHeadersColumnsMapping.Add(item, transformedColumnName);
                                }
                            }
                        }
                        else
                        {
                            foreach (var item in filesHeaders)
                            {
                                string transformedColumnName = FlpConfigurationHelper.CleanColumnName(item);
                                var findValue = headerRow.FirstOrDefault(x => x == transformedColumnName);
                                if (findValue != null)
                                {
                                    customHeadersColumnsMapping.Add(transformedColumnName, item);
                                }
                            }
                        }


                    }
                    else
                    {

                        if (!parameterConfig.ColumnNameList.Contains("="))
                        {
                            throw new InvalidDataException("Not found index format with column name list for custom header");
                        }
                        var customHeadesWithIndexes = FlpConfigurationHelper.GetCustomHeaersIndex(parameterConfig.ColumnNameList);
                        var firstRow = csv.Parser.RawRecord.Split(parameterConfig.Delimiter);
                        var filesCustomHeaders = Enumerable.Range(1, firstRow.Length).Select(i => $"Field{i}").ToList();

                        foreach (var (key, value) in customHeadesWithIndexes)
                        {
                            string transformedColumnName = FlpConfigurationHelper.CleanColumnName(key);
                            string mappedColumnName = filesCustomHeaders[value];
                            headerRow.Add(transformedColumnName);
                            customHeadersColumnsMapping.Add(transformedColumnName, mappedColumnName);

                        }

                    }

                }
                else
                {
                    // Auto-generate header names
                    csv.Read();
                    var firstRow = csv.Parser.RawRecord.Split(parameterConfig.Delimiter);
                    headerRow = Enumerable.Range(0, firstRow.Length).Select(i => $"Field{i}").ToList();
                }


                // Transform column names and map them
                for (int i = 0; i < headerRow.Count; i++)
                {
                    string originalColumnName = headerRow[i];
                    string transformedColumnName = FlpConfigurationHelper.CleanColumnName(originalColumnName);
                    headerRow[i] = transformedColumnName;
                    originalToTransformedColumnNameMap[originalColumnName] = transformedColumnName;
                }
                //TODO: may be used below code in future
                //if (createFileColumn)
                //{
                //    var columns = originalToTransformedColumnNameMap.Select(x => x.Value).ToList();
                //    var columnList = string.Join(",", columns);
                //    var dbResponse = await _iIProcessConfigurationRepository.UpdateColumnNameList(parameterConfig.FlpConfigurationId, parameterConfig.TabName, columnList);
                //    if (string.Compare(dbResponse.Result, "Success", true) != 0)
                //    {
                //        _logger.LogError($"Not updated column list for flpConfigId: {parameterConfig.FlpConfigurationId} and tab: {parameterConfig.TabName ?? "Tab name is empty"}");
                //    }
                //}
                var fields = new DataField[headerRow.Count];
                for (int i = 0; i < headerRow.Count; i++)
                {
                    DataTypeDetails dataTypeDetails = new DataTypeDetails();
                    var header = headerRow[i];
                    string format = "";
                    if (!string.IsNullOrWhiteSpace(convertDatatypeString))
                    {
                        fields[i] = new DataField<string>(header);
                        dataTypeDetails.DataType = "string";
                        dataTypeDetails.DataTypeFormat = format;
                    }
                    else
                    {
                        if (columnDataTypeMappings != null && columnDataTypeMappings.ContainsKey(header))
                        {
                            var dataType = columnDataTypeMappings[header];
                            if (dataType.Contains("|"))
                            {
                                var arr = FlpConfigurationHelper.SplitString(dataType, "|");
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

                            fields[i] = dataType switch
                            {
                                "string" => new DataField<string?>(header),
                                "int" => new DataField<int?>(header),
                                "long" => new DataField<long?>(header),
                                "float" => new DataField<float?>(header),
                                "double" => new DataField<double?>(header),
                                "bool" => new DataField<bool?>(header),
                                "datetime" => new DataField<DateTime?>(header),
                                "date" => new DataField<DateOnly?>(header),
                                "time" => new DataField<TimeOnly?>(header),
                                _ => new DataField<string?>(header), // default to string if the data type is unrecognized
                            };
                        }
                        else
                        {
                            fields[i] = new DataField<string?>(header); // default to string if no override is specified
                        }
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
                            (rows, uniqueKeys, tempRows) = GetFirstRows(csv, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueKeys, tempRows, rows, keyColumnList);
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
                                for (int i = 0; i < filesHeaders.Count; i++)
                                {
                                    string header = filesHeaders[i];
                                    // Check if the header exists in the custom mapping dictionary
                                    if (customHeadersColumnsMapping.TryGetValue(header, out var mappedValue) && !string.IsNullOrWhiteSpace(mappedValue))
                                    {
                                        recordDict[mappedValue] = csv.GetField(i); //Map the custom header to the value
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

                        if (dedupColumnsList.Any())
                        {
                            rows = GetRowsFromCsvReaderWithDedupColumn(csvRecords, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueRowsWithDedup, rows, actualRecordsCount, keyColumnList);
                        }
                        else
                        {
                            rows = GetRowsFromCsvReader(csvRecords, customHeadersColumnsMapping, originalToTransformedColumnNameMap, parameterConfig, uniqueKeys, tempRows, rows, actualRecordsCount, keyColumnList);
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
                            // Iterate over each key-value pair in uniqueRowsWithDedup
                            try
                            {
                                foreach (var kvp in uniqueRowsWithDedup)
                                {
                                    var key = kvp.Key;  // Get the unique key for the current entry
                                    var rowData = kvp.Value; // Original List<List<object>>
                                    //List<List<object>> listOfLists = kvp.Value;
                                    //List<List<object>> rowData = listOfLists.Select(innerList => innerList.ToList()).ToList();


                                    // Initialize the sorting
                                    IOrderedEnumerable<List<object>> orderedRows = null;
                                    for (int i = 0; i < dedupColumnsList.Count; i++)
                                    {
                                        string col = dedupColumnsList[i].ToString();
                                        // var colIndex = Array.IndexOf(headerRow.ToArray(), col);
                                        if (customHeadersColumnsMapping?.Count > 0)
                                            col = customHeadersColumnsMapping[col];
                                        // Order rowData by the specified column in descending order                                       
                                        if (i == 0)
                                        {
                                            orderedRows = parameterConfig.KeepFirstRow
                                                ? rowData.OrderBy(row => GetDictionaryValue(row, col))
                                                : rowData.OrderByDescending(row => GetDictionaryValue(row, col));
                                        }
                                        else
                                        {
                                            orderedRows = parameterConfig.KeepFirstRow
                                                ? orderedRows.ThenBy(row => GetDictionaryValue(row, col))
                                                : orderedRows.ThenByDescending(row => GetDictionaryValue(row, col));
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



                            }
                            catch (Exception ex)
                            {


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
                                if (customHeadersColumnsMapping?.Count > 0)
                                {
                                    if (hasDuplicateOrEmptyHeader)
                                    {
                                        //string key = customHeadersColumnsMapping.FirstOrDefault(x => x.Value == header).Key;
                                        //value = rowDict.ContainsKey(header) ? rowDict[header] : null;
                                        value = rowDict.ContainsKey(header) ? rowDict[header] : null;
                                    }
                                    else
                                    {
                                        var val = customHeadersColumnsMapping[header];
                                        value = rowDict[val];
                                    }
                                }
                                else
                                {
                                    value = rowDict.ContainsKey(header) ? rowDict[header] : null;
                                }
                                columns[i].Add(value?.ToString() ?? string.Empty);
                                //object value1 = value2;// rowDict.ContainsKey(header) ? customHeadersColumnsMapping[header] : null;
                                /* if (!string.IsNullOrWhiteSpace(convertDatatypeString))
                                 {
                                     columns[i].Add(value?.ToString()??string.Empty);
                                 }
                                 else if (value != null && columnDataTypeMappings != null && columnDataTypeMappings.ContainsKey(header))
                                 {
                                     var dataType = columnDataTypeMappings[header];
                                     string[] dateFormats = { "dd/M/yyyy", "M/dd/yyyy", "yyyy-MM-dd" };
                                     value = dataType switch
                                     {
                                         "int" => int.TryParse(value.ToString(), out int intValue) ? intValue : 0,
                                         "long" => long.TryParse(value.ToString(), out long longValue) ? longValue : 0L,
                                         "float" => float.TryParse(value.ToString(), out float floatValue) ? floatValue : 0f,
                                         "double" => double.TryParse(value.ToString(), out double doubleValue) ? doubleValue : 0d,
                                         "bool" => bool.TryParse(value.ToString(), out bool boolValue) ? boolValue : false,
                                         "datetime" => DateTime.TryParse(value.ToString(), out DateTime datetimeValue) ? datetimeValue : DateTime.TryParseExact(value.ToString(), dateFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime dtValue) ? dtValue : null,
                                         _ => value.ToString()
                                     };


                                     columns[i].Add(value);
                                 }
                                 else
                                 {
                                     columns[i].Add(value);
                                 }*/

                            }

                        }





                        for (int i = 0; i < columns.Count; i++)
                        {
                            var dataField = fields[i];

                            if (!string.IsNullOrWhiteSpace(convertDatatypeString))
                            {
                                var dataColumn = new Parquet.Data.DataColumn(dataField, columns[i].Cast<string?>().ToArray());
                                await groupWriter.WriteColumnAsync(dataColumn);
                            }
                            else if (columnDataTypeMappings != null)
                            {
                                string format = "";
                                var dataType = columnDataTypeMappings.ContainsKey(dataField.Name) ? columnDataTypeMappings[dataField.Name] : "string";
                                var columnDataTypeDetails = resultResponse.ColumnDataTypeList.ContainsKey(dataField.Name) ? resultResponse.ColumnDataTypeList[dataField.Name] : null;
                                if (columnDataTypeDetails != null)
                                {
                                    dataType = columnDataTypeDetails.DataType;
                                    format = columnDataTypeDetails.DataTypeFormat;
                                }
                                List<string> dateFormats = new List<string>();
                                if (!string.IsNullOrWhiteSpace(format))
                                {
                                    dateFormats.Add(format);
                                }
                                else
                                {
                                    dateFormats = formats.Where(x => x.formatDataTypeId == 24).Select(x => x.format).ToList();
                                    if (dataType == DataTypeOptionsEnum.DATETIME.GetDescription())
                                    {
                                        dateFormats = formats.Where(x => x.formatDataTypeId == 18).Select(x => x.format).ToList();
                                    }
                                    else if (dataType == DataTypeOptionsEnum.DATE.GetDescription())
                                    {
                                        dateFormats = formats.Where(x => x.formatDataTypeId == 24).Select(x => x.format).ToList();
                                    }
                                    else if (dataType == DataTypeOptionsEnum.TIME.GetDescription())
                                    {
                                        dateFormats = ParquetSchemaHelper.GetTimeFormats();
                                        if (!string.IsNullOrWhiteSpace(format) && !dateFormats.Any(x => x == format))
                                        {
                                            dateFormats.Add(format);
                                        }
                                    }
                                }

                                var dataColumn = dataType switch
                                {
                                    //"int" => new Parquet.Data.DataColumn(dataField, columns[i].Cast<int>().ToArray()),
                                    //"long" => new Parquet.Data.DataColumn(dataField, columns[i].Cast<long>().ToArray()),
                                    //"float" => new Parquet.Data.DataColumn(dataField, columns[i].Cast<float>().ToArray()),
                                    //"double" => new Parquet.Data.DataColumn(dataField, columns[i].Cast<double>().ToArray()),
                                    //"bool" => new Parquet.Data.DataColumn(dataField, columns[i].Cast<bool>().ToArray()),
                                    //"datetime" => new Parquet.Data.DataColumn(dataField, columns[i].Cast<DateTime?>().ToArray()),
                                    //_ => new Parquet.Data.DataColumn(dataField, columns[i].Cast<string>().ToArray())
                                    "int" => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        int intValue;
                                        return int.TryParse(x?.ToString(), out intValue) ? (int?)intValue : null;
                                    }).ToArray()),
                                    "long" => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        long longValue;
                                        return long.TryParse(x?.ToString(), out longValue) ? (long?)longValue : null;
                                    }).ToArray()),
                                    "float" => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        float floatValue;
                                        return float.TryParse(x?.ToString(), out floatValue) ? (float?)floatValue : null;
                                    }).ToArray()),
                                    "double" => new DataColumn(dataField, columns[i].ToArray().Select(x =>
                                    {
                                        double doubleValue;
                                        return double.TryParse(x?.ToString(), out doubleValue) ? (double?)doubleValue : null;
                                    }).ToArray()),
                                    "bool" => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        bool boolValue;
                                        return bool.TryParse(x?.ToString(), out boolValue) ? (bool?)boolValue : null;
                                    }).ToArray()),
                                    "datetime" => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        DateTime dateTimeValue;
                                        return DateTime.TryParseExact(x?.ToString(), dateFormats.ToArray(),
                                               CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTimeValue)
                                               ? (DateTime?)dateTimeValue : null;
                                    }).ToArray()),
                                    "date" => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        if (x == null || string.IsNullOrWhiteSpace(x.ToString()))
                                            return null;

                                        DateOnly dateOnlyValue;
                                        return DateOnly.TryParseExact(x?.ToString(), dateFormats.ToArray(),
                                               CultureInfo.InvariantCulture, DateTimeStyles.None, out dateOnlyValue)
                                               ? (DateOnly?)dateOnlyValue : null;
                                    }).ToArray()),
                                    "time" => new DataColumn(dataField, columns[i].Select(x =>
                                    {
                                        TimeOnly timeOnlyValue;
                                        return TimeOnly.TryParseExact(x?.ToString(), dateFormats.ToArray(),
                                               CultureInfo.InvariantCulture, DateTimeStyles.None, out timeOnlyValue)
                                               ? (TimeOnly?)timeOnlyValue : null;
                                    }).ToArray()),
                                    _ => new DataColumn(dataField, columns[i].Select(x => x?.ToString() ?? null).ToArray())
                                };

                                await groupWriter.WriteColumnAsync(dataColumn);
                            }
                            else
                            {
                                var dataColumn = new Parquet.Data.DataColumn(dataField, columns[i].Cast<string?>().ToArray());
                                await groupWriter.WriteColumnAsync(dataColumn);
                            }
                        }

                        resultResponse.ParquetFileCreated = true;
                    }
                }
            }
            return resultResponse;
        }


        // Helper method to extract value by column name from dictionary in the row
        private static object GetDictionaryValue(List<object> row, string columnName)
        {
            foreach (var item in row)
            {
                if (item is Dictionary<string, object> dictionary && dictionary.TryGetValue(columnName, out var value))
                {
                    return value;
                }
            }
            return null; // Return null if column name not found in any dictionary
        }
        private static bool IsValidDelimiter(Stream csvStream, string paramCsvDelimiter)
        {
            // Check if the delimiter is valid (either a single character or '\t' for tab)

            if (paramCsvDelimiter.Length != 1 && paramCsvDelimiter != "\t")
            {
                throw new ArgumentException("Delimiter should be a single character or tab", nameof(paramCsvDelimiter));
            }

            char delimiterChar = paramCsvDelimiter == "\t" ? '\t' : paramCsvDelimiter[0];

            using (var reader = new StreamReader(csvStream, leaveOpen: true))
            {
                // Read the first line
                string firstLine = reader.ReadLine();

                // Reset the stream position after reading the first line
                csvStream.Position = 0;

                // If the first line is null or empty, delimiter can't be validated
                if (string.IsNullOrEmpty(firstLine))
                {
                    throw new InvalidDataException("CSV stream is empty or has an invalid first line.");
                }

                // Count occurrences of delimiters
                int delimiterCount = firstLine.Count(c => c == delimiterChar);

                // Determine if the delimiter is valid by checking if the count is greater than zero
                return delimiterCount > 0;
            }
        }



        private static List<dynamic> GetRowsFromCsvReader(List<dynamic> csvRecords, Dictionary<string, string> customHeadersColumnsMapping, Dictionary<string, string> originalToTransformedColumnNameMap, ConfigurationTableMappingDto parameterConfig, HashSet<string> uniqueKeys, Dictionary<string, dynamic> tempRows, List<dynamic> rows, int actualRecordsCount, List<string> keyColumnList)
        {


            //while (csv.Read())
            for (int i = 0; i < actualRecordsCount; i++)
            {
                var fileRow = csvRecords[i];//csv.GetRecord<dynamic>();
                var fileRowDict = (IDictionary<string, object>)fileRow;

                // Use transformed column names
                var transformedRowDict = fileRowDict.ToDictionary(
                    kvp => originalToTransformedColumnNameMap.ContainsKey(kvp.Key) ? originalToTransformedColumnNameMap[kvp.Key] : kvp.Key,
                    kvp => kvp.Value);

                if (parameterConfig.IgnoreDuplicateRows)
                {
                    List<string> keyColumns = new List<string>();

                    // Generate key based on specified DataKey columns
                    if (customHeadersColumnsMapping?.Count > 0)
                    {
                        keyColumns = keyColumnList
                      .Select(k => customHeadersColumnsMapping.ContainsKey(k.Trim())
                          ? customHeadersColumnsMapping[k.Trim()]
                          : k.Trim())
                      .ToList();


                    }
                    else
                    {
                        keyColumns = keyColumnList
                                .Select(k => originalToTransformedColumnNameMap.ContainsKey(k.Trim())
                                    ? originalToTransformedColumnNameMap[k.Trim()]
                                    : k.Trim())
                                .ToList();

                    }

                    var keyValues = keyColumns.Select(k => transformedRowDict.ContainsKey(k) ? transformedRowDict[k]?.ToString() : string.Empty).ToArray();
                    var key = string.Join(parameterConfig.Delimiter, keyValues);

                    if (parameterConfig.KeepFirstRow)
                    {
                        if (!uniqueKeys.Contains(key))
                        {
                            uniqueKeys.Add(key);
                            rows.Add(transformedRowDict);
                        }
                    }
                    else if (!parameterConfig.KeepFirstRow)
                    {
                        tempRows[key] = transformedRowDict;
                    }
                    else
                    {
                        rows.Add(transformedRowDict);
                    }
                }
                else
                {
                    rows.Add(transformedRowDict);
                }


            }

            return rows;
        }



        private static List<dynamic> GetRowsFromCsvReaderWithDedupColumn(List<dynamic> csvRecords, Dictionary<string, string> customHeadersColumnsMapping, Dictionary<string, string> originalToTransformedColumnNameMap, ConfigurationTableMappingDto parameterConfig, Dictionary<string, List<List<object>>> uniqueRowsWithDedup, List<dynamic> rows, int actualRecordsCount, List<string> keyColumnList)
        {


            //while (csv.Read())
            for (int i = 0; i < actualRecordsCount; i++)
            {
                var fileRow = csvRecords[i];//csv.GetRecord<dynamic>();
                var fileRowDict = (IDictionary<string, object>)fileRow;
                List<dynamic> listOfRows = new List<dynamic>();

                // Use transformed column names
                var transformedRowDict = fileRowDict.ToDictionary(
                    kvp => originalToTransformedColumnNameMap.ContainsKey(kvp.Key) ? originalToTransformedColumnNameMap[kvp.Key] : kvp.Key,
                    kvp => kvp.Value);
                //rows.Add(transformedRowDict);
                listOfRows.Add(transformedRowDict);
                List<string> keyColumns = new List<string>();
                // Generate key based on specified DataKey columns
                if (customHeadersColumnsMapping?.Count > 0)
                {
                    keyColumns = keyColumnList
                  .Select(k => customHeadersColumnsMapping.ContainsKey(k.Trim())
                      ? customHeadersColumnsMapping[k.Trim()]
                      : k.Trim())
                  .ToList();


                }
                else
                {
                    keyColumns = keyColumnList
                            .Select(k => originalToTransformedColumnNameMap.ContainsKey(k.Trim())
                                ? originalToTransformedColumnNameMap[k.Trim()]
                                : k.Trim())
                            .ToList();

                }

                var keyValues = keyColumns.Select(k => transformedRowDict.ContainsKey(k) ? transformedRowDict[k]?.ToString() : string.Empty).ToArray();
                var key = string.Join(parameterConfig.Delimiter, keyValues);

                if (uniqueRowsWithDedup.ContainsKey(key))
                {
                    uniqueRowsWithDedup[key].Add(listOfRows);
                }
                else
                {
                    // If the key does not exist, create a new entry
                    uniqueRowsWithDedup[key] = new List<List<object>> { listOfRows };
                }


            }

            return rows;
        }


        private static (List<dynamic>, HashSet<string>, Dictionary<string, dynamic>) GetFirstRows(CsvReader csv, Dictionary<string, string> customHeadersColumnsMapping, Dictionary<string, string> originalToTransformedColumnNameMap, ConfigurationTableMappingDto parameterConfig, HashSet<string> uniqueKeys, Dictionary<string, dynamic> tempRows, List<dynamic> rows, List<string> keyColumnList)
        {

            var fileRow = csv.GetRecord<dynamic>();
            var fileRowDict = (IDictionary<string, object>)fileRow;

            // Use transformed column names
            var transformedRowDict = fileRowDict.ToDictionary(
                kvp => originalToTransformedColumnNameMap.ContainsKey(kvp.Key) ? originalToTransformedColumnNameMap[kvp.Key] : kvp.Key,
                kvp => kvp.Value);


            if (parameterConfig.IgnoreDuplicateRows)
            {
                List<string> keyColumns = new List<string>();

                // Generate key based on specified DataKey columns
                if (customHeadersColumnsMapping?.Count > 0)
                {
                    keyColumns = keyColumnList
                  .Select(k => customHeadersColumnsMapping.ContainsKey(k.Trim())
                      ? customHeadersColumnsMapping[k.Trim()]
                      : k.Trim())
                  .ToList();


                }
                else
                {
                    keyColumns = keyColumnList
                            .Select(k => originalToTransformedColumnNameMap.ContainsKey(k.Trim())
                                ? originalToTransformedColumnNameMap[k.Trim()]
                                : k.Trim())
                            .ToList();

                }

                var keyValues = keyColumns.Select(k => transformedRowDict.ContainsKey(k) ? transformedRowDict[k]?.ToString() : string.Empty).ToArray();
                var key = string.Join(parameterConfig.Delimiter, keyValues);

                if (parameterConfig.KeepFirstRow)
                {
                    if (!uniqueKeys.Contains(key))
                    {
                        uniqueKeys.Add(key);
                        rows.Add(transformedRowDict);
                    }
                }
                else if (!parameterConfig.KeepFirstRow)
                {
                    tempRows[key] = transformedRowDict;
                }
                else
                {
                    rows.Add(transformedRowDict);
                }
            }
            else
            {
                rows.Add(transformedRowDict);
            }


            return (rows, uniqueKeys, tempRows);
        }

    }
}
