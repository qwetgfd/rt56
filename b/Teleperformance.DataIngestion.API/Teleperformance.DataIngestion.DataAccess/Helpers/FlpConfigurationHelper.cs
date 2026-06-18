using Azure;
using Azure.Storage.Blobs;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2016.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using NPOI.OpenXml4Net.Exceptions;
using NPOI.SS.UserModel;
using Parquet.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Enums.v3._0;
using Teleperformance.DataIngestion.Models.Helpers;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using static Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus.StatusResponseDto;
using CellType = NPOI.SS.UserModel.CellType;
using DataField = Parquet.Schema.DataField;

namespace Teleperformance.DataIngestion.DataAccess.Helpers
{
    public class FlpConfigurationHelper
    {
        public static string GetParquetFileName(string url)
        {
            return $"{Path.GetFileNameWithoutExtension(url)}.parquet";
        }

        public static string GetBackUpFileName(string fileUrl, string processId)
        {
            string backupFileName = $"{Path.GetFileNameWithoutExtension(fileUrl)}_{processId}{Path.GetExtension(fileUrl)}";
            return backupFileName;
        }

        public static string GetFileExtension(string fileUrl)
        {
            string fileExtension = $"{Path.GetExtension(fileUrl)}";
            return fileExtension;
        }

        public static string GetFileNameWithoutExtension(string fileUrl)
        {
            string fileExtension = $"{Path.GetFileNameWithoutExtension(fileUrl)}";
            return fileExtension;
        }


        public static string EscapedTabString(string tabCharacter)
        {
            return tabCharacter.Replace("\\t", "\t");
        }
        public static string GetFileLocation(FlpConfigurationResponseDto flpConfigurationRequestDto)
        {
            if ((int)SourceLocationTypeEnum.Azure == flpConfigurationRequestDto.LocationTypeId)
                return flpConfigurationRequestDto.BlobClients.Name;

            if ((int)SourceLocationTypeEnum.OnPrem == flpConfigurationRequestDto.LocationTypeId)
                return flpConfigurationRequestDto.SourcePath;
            #region SharePoint Workspace - AY
            if ((int)SourceLocationTypeEnum.SharePoint == flpConfigurationRequestDto.LocationTypeId)
                return flpConfigurationRequestDto.SharePointFileLocation?.FileUrl ?? flpConfigurationRequestDto.SourcePath;
            #endregion
            return "";
        }

        public static List<string> GetColumnNamesList(string csvColumnNameList)
        {
            if (!string.IsNullOrWhiteSpace(csvColumnNameList))
                return SplitString(csvColumnNameList,",");

            return new List<string>();
        }
        public static List<string> GetKeyColumnNamesList(string csvColumnNameList)
        {
            if (!string.IsNullOrWhiteSpace(csvColumnNameList))
                return new List<string>(csvColumnNameList.ToLower().Split(','));

            return new List<string>();
        }

        public static List<string> SplitString(string inputValue,string seprator)
        {
            List<string> list = new List<string>();
            if (!string.IsNullOrWhiteSpace(inputValue))
            {
                foreach(string val in inputValue.Split($"{seprator}"))
                {
                    string cleanedValue = CleanColumnName(val);
                    list.Add(cleanedValue);
                }
            }             
            return list;
        }


        public static List<string> SplitColumnsWithoutCleanColumn(string inputValue, string seprator)
        {
            List<string> list = new List<string>();
            if (!string.IsNullOrWhiteSpace(inputValue))
            {
                foreach (string val in inputValue.Split($"{seprator}"))
                {
                    string cleanedValue = val;
                    list.Add(cleanedValue);
                }
            }
            return list;
        }


        public static List<string> DuplicateList(List<string> list)
        {
            if (list == null || list.Count == 0)
                return new List<string>(); // Return empty list if input is null or empty

            // Find duplicate values (ignoring case)
            var duplicateList = list
                .Where(x => !string.IsNullOrWhiteSpace(x)) // Exclude empty/whitespace values
                .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1) // Keep only duplicates
                .Select(g => g.Key) // Get duplicate values
                .ToList();

            return duplicateList;
        }


        public static bool ContainsDuplicatesOrEmptyHeader(List<string> list)
        {
           
            // Check if the list contains empty or whitespace values
            bool hasEmptyValues = list.Any(string.IsNullOrWhiteSpace);

            // Check for duplicate values ignoring case
            bool hasDuplicates = list
                .Where(x => !string.IsNullOrWhiteSpace(x)) // Exclude empty or whitespace values for duplication check
                .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Any(g => g.Count() > 1); // True if any group has more than one item (duplicates)

            // Return true if there are either duplicates or empty values
            return hasEmptyValues || hasDuplicates;
        }

        public static List<string> CreateCustomHeader(List<string> columnList)
        {
            if(columnList !=null && columnList.Any())
            {
                // Counter for unnamed columns (empty strings)
                int unnamedCounter = 1;
                var groupedColumns = columnList
                    .Select((name, index) => new { Name = name, Index = index }) // Keep track of original index
                    .GroupBy(x => x.Name) // Group by column name
                    .ToList();
                var columnCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                List<string> processedList = new List<string>();
                for (int i = 0; i < columnList.Count; i++)
                {
                    string column = columnList[i];

                    // Handle empty string by appending "col" with the index
                    if (string.IsNullOrWhiteSpace(column))
                    {
                        column = $"col{i}";
                    }


                    // Check the grouped column for duplicates
                    var groupedColumn = groupedColumns.FirstOrDefault(g => g.Key.Equals(column, StringComparison.OrdinalIgnoreCase));


                    // Check for duplicates and append a number for each occurrence
                    if (columnCount.ContainsKey(column))
                    {
                        columnCount[column]++;
                        column = $"{column}_{columnCount[column]}"; // Append the count number to make it unique
                    }
                    else
                    {
                        if (groupedColumn != null && groupedColumn.Count() > 1)
                        {
                            if (processedList.Any(x => x == column))
                            {
                                columnCount[column] = 1;
                                column = $"{column}_{columnCount[column]}";
                            }
                        }
                        else
                        {
                            columnCount[column] = 1;
                        }

                    }

                    processedList.Add(column);
                }
                return processedList;
            }

            return new List<string>();
        }



        public static (bool,List<string>) ValidateFileSchema(bool validateFileSchema,string columnNameList,bool isHdeaderProvided)
        {
            List<string> columns = new List<string>();
            bool createFileColumn = false;
            if (validateFileSchema)
            {
                columns = GetColumnNamesList(columnNameList);
                if (columnNameList != null && columnNameList.Any())
                {
                    //Columnlist not blanck
                    createFileColumn = false;
                }
                else
                {
                    //column list blank (its from shared location)
                    createFileColumn = true;
                    //Read columns from excel and saved it

                }

            }
            else
            {
                createFileColumn = true;
                if (!isHdeaderProvided)
                {
                    columns = GetColumnNamesList(columnNameList);
                    if (columnNameList != null && columnNameList.Any())
                    {
                        //Columnlist not blanck
                        createFileColumn = false;
                    }

                }

                //Read columns from excel and saved it
            }
            return (createFileColumn, columns);

        }


      

        public static Dictionary<string, int> GetCustomHeaersIndex(string columnNameList)
        {


            try
            {
                // Split the string by commas to get individual column definitions
                string[] columns = columnNameList.Split(',');

                // Create a dictionary to store the column and its last value
                Dictionary<string, int> columnDictionary = new Dictionary<string, int>();
                foreach (string column in columns)
                {
                    string[] parts = column.Split('=');
                    string columnName = parts[0];
                    int lastValue = int.Parse(parts[^1]); // ^1 means "last element" (C# 8.0+ syntax)

                    // Add to the dictionary
                    columnDictionary[columnName] = lastValue;
                }

                // Now columnDictionary contains the column names as keys and the last values as values
                // For example: {"col0" => 0, "col1" => 1, ...}
                return columnDictionary;
            }
            catch (Exception ex)
            {
             throw new InvalidFormatException(ex.Message.ToString());
            }

        }

        public static (string, Dictionary<string, string>) GetColumnDataTypeList(string csvConvertDatatypesColumnList)
        {
          
            try
            {
                Dictionary<string, string> columnDataTypeMappings = new Dictionary<string, string>();

                if (string.IsNullOrWhiteSpace(csvConvertDatatypesColumnList))
                    return (null, null);

                if (csvConvertDatatypesColumnList.ToLower() == "all=string")
                {
                    return ("string", null);
                }
                else
                {

                    columnDataTypeMappings = csvConvertDatatypesColumnList.ToLower()
                    .Split(',')
                    .Select(x =>
                    {
                        // Split by '=' and handle cases where '=' is missing
                        var parts = x.Split('=');
                        return parts.Length == 2
                            ? new KeyValuePair<string, string>(CleanColumnName(parts[0]), parts[1].Trim())
                            : new KeyValuePair<string, string>(CleanColumnName(parts[0]), "string"); // Default to "string" if '=' is missing
                    })
                    .ToDictionary(x => x.Key, x => x.Value);
                }
                return (null, columnDataTypeMappings);
            }
            catch (Exception ex)
            {
                throw new InvalidFormatException(ex.Message.ToString());
            }
        }

        public static string CleanColumnName(string columnName)
        {
            //Trim all columns 
            columnName = columnName.Trim().ToLower();
            columnName = columnName.Replace('\t', ' ');
            columnName = Regex.Replace(columnName, @"[-,.,@,(,{]", "_");
            columnName = Regex.Replace(columnName, @"[),',},$,#,!,&,%,*,/,~,^,?,<,>,;,:,¿,¡]", "");
            columnName = columnName.Replace(' ', '_').Replace("\"", "");
            columnName = columnName.Replace("[", "").Replace("]", "").Replace(",", "");
            columnName = columnName.Replace("___", "_").Replace("__", "_");
            return columnName;
        }

        public static string GetBlobConnectionString(string storageAccountName, string storageAccountKey)
        {
            string blobConnectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageAccountKey};EndpointSuffix=core.windows.net"; ;
            return blobConnectionString;
        }

        public static string GetBlobConnectionStringBySasKey(string storageAccountName,string sasToken)
        {
            string blobConnectionString = $"https://{storageAccountName}.blob.core.windows.net/{sasToken}";
            return blobConnectionString;
        }
        public static bool ValidString(params string[] values)
        {
            return values != null && values.All(value => !string.IsNullOrWhiteSpace(value));
        }


        public static (bool, FlpProcessTempFile) CopyFileBlobToOnPremAsync(string tempFilePath, string flpConfigurationId, BlobClient blobClient, SharedLocationDestinationServerDto slDestinationServerDto, ISMBLibraryServices ismbLibraryServices)
        {
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();

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
      
        public async static Task<(bool ret, FlpProcessTempFile)> CopyFileOnPremToDestinationBlobAsync(string onPremFilePath, string flpConfigurationId, BlobClient destinationBlobClient, CheckConnectivitySMBLibraryModel checkConnectivitySMB, ISMBLibraryServices ismbLibraryServices)
        {
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();
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

        public static async Task<(bool ret, FlpProcessTempFile)> CreateCsvFromExcelUsingSmbLibraryAsync(string flpConfigurationId,BlobClient destinationBlobClient,CheckConnectivitySMBLibraryModel checkConnectivitySMB,
       BlobClient csvBlobClient,ISMBLibraryServices ismbLibraryServices)
        {
            FlpProcessTempFile flpProcessTempFile = new FlpProcessTempFile();

            try
            {
                // Register encoding provider for ExcelDataReader
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                // Step 1: Get Excel file stream from SMB
                using var excelStream = ismbLibraryServices
                    .SMBRequest(checkConnectivitySMB, flpConfigurationId, SMBRequestEnum.Stream)
                    .GetFileStream;

                // Step 2: Convert Excel to CSV
                string csvContent = ExcelHelper.ConvertExcelStreamToCsv(excelStream);

                //Step 3: Upload CSV to Blob
               //using var csvStream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
               //var res = await destinationBlobClient.UploadAsync(csvStream, overwrite: true);              

                using var csvStream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
                var response = await csvBlobClient.UploadAsync(csvStream, overwrite: true);
                var ret = response != null && (response.GetRawResponse().Status == 201|| response.GetRawResponse().Status == 200);              
               // var ret = statusCode == 201 || statusCode == 200;
                if (ret)
                {
                    flpProcessTempFile.CsvFile = new BlobLocationCsvFile
                    {
                        CsvName = csvBlobClient.Name,
                        CsvUri = csvBlobClient.Uri.ToString(),
                        CsvBlobContainerName = csvBlobClient.BlobContainerName,
                        CsvCanGenerateSasUri = csvBlobClient.CanGenerateSasUri
                    };
                }

                return (ret, flpProcessTempFile);
            }
            catch (Exception ex)
            {
                flpProcessTempFile.ErrorMessage = $"Error: {ex.Message}";
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

      

        public static async Task<(bool, string)> DeleteFile(string filePath)
        {

            try
            {
                // Delete original parquet file if needed
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                return (true, "Deleted file");
            }
            catch (Exception ex)
            {
                return (false, ex.Message.ToString());
            }

        }

        public static DataField[]? CreateColumnInParquetFields(DataField[] dataFields)
        {
            if (dataFields?.Count() > 0)
            {
                // Add the new DataField for InsertionDateTime
                var insertionDateField = new DataField<DateTime?>("insertionDateTime");
                dataFields = dataFields.Concat(new[] { insertionDateField }).ToArray();


                var hasByteDateField = new DataField<byte[]>("hashByte");
                dataFields = dataFields.Concat(new[] { hasByteDateField }).ToArray();
            }
            return dataFields;
        }


        public static DataField[]? CreateColumnInParquetFieldsV2(DataField[] dataFields)
        {
            if (dataFields?.Count() > 0)
            {

                // Add the new DataField for InsertionDateTime
                var uploadFileId = new DataField<string?>("fileUploadId");
                dataFields = dataFields.Concat(new[] { uploadFileId }).ToArray();


                // Add the new DataField for InsertionDateTime
                var fileName = new DataField<string?>("fileName");
                dataFields = dataFields.Concat(new[] { fileName }).ToArray();

                // Add the new DataField for InsertionDateTime
                var insertionDateField = new DataField<DateTime?>("insertionDateTime");
                dataFields = dataFields.Concat(new[] { insertionDateField }).ToArray();


                var hasByteDateField = new DataField<byte[]>("hashByte");
                dataFields = dataFields.Concat(new[] { hasByteDateField }).ToArray();
            }
            return dataFields;
        }


        public static string ValidDateFormat(string format)
        {
          
            if (format.ToLower().Contains("d") && format.ToLower().Contains("y"))
            {
                // Assume date format
                format = format.Replace("D", "d").Replace("Y", "y").Replace("m", "M");
                return format;
            }
            return format;
        }

        public static string GetDateTypeValue(int dataTypeId)
        {
            if(dataTypeId == (int)DatatypeNamesEnum.INT)
            {
                return "int";

            }else if (dataTypeId == (int)DatatypeNamesEnum.FLOAT)
            {
                return "float";
            }
            else if (dataTypeId == (int)DatatypeNamesEnum.LONG)
            {
                return "long";
            }
            else if (dataTypeId == (int)DatatypeNamesEnum.DOUBLE)
            {
                return "double";
            }
            else if (dataTypeId == (int)DatatypeNamesEnum.DATE)
            {
                return "date";
            }
            else if (dataTypeId == (int)DatatypeNamesEnum.DATETIME)
            {
                return "datetime";
            }
            else if (dataTypeId == (int)DatatypeNamesEnum.TIME)
            {
                return "time";
            }
            else if (dataTypeId == (int)DatatypeNamesEnum.BOOL)
            {
                return "bool";
            }
            else
            {
                return "string";
            }            
        }

        public static Encoding GetEncoding()
        {
           return Encoding.GetEncoding(1252);
        }

        public static DataField CreateDataField(int dataTypeId, string columnName)
        {
            return dataTypeId switch
            {
                (int)DatatypeNamesEnum.STRING => new DataField<string?>(columnName),
                (int)DatatypeNamesEnum.INT => new DataField<int?>(columnName),
                (int)DatatypeNamesEnum.LONG => new DataField<long?>(columnName),
                (int)DatatypeNamesEnum.FLOAT => new DataField<float?>(columnName),
                (int)DatatypeNamesEnum.DOUBLE => new DataField<double?>(columnName),
                (int)DatatypeNamesEnum.BOOL => new DataField<bool?>(columnName),
                (int)DatatypeNamesEnum.DATETIME => new DataField<DateTime?>(columnName),   // For DateTime
                (int)DatatypeNamesEnum.DATE => new DataField<DateOnly?>(columnName),       // Nullable DateOnly
                (int)DatatypeNamesEnum.TIME => new DataField<TimeOnly?>(columnName),       // Nullable TimeOnly
                _ => new DataField<string>(columnName)                // Default to string if no match
            };
        }

        public static Dictionary<string, string> MapHeadersToColumns<T>(
                                                   bool hasDuplicateOrEmptyHeader,
                                                   List<string> headers,
                                                   List<string> fileHeaders,
                                                   Dictionary<string, T> mappingDictionary,
                                                   Func<T, string> getDbColumnFunc)
        {
            var result = new Dictionary<string, string>();
            int indexNo = 0;
            foreach (var header in headers)
            {
                if (mappingDictionary.TryGetValue(header, out var mappingDto) && mappingDto != null)
                {
                    var dbColumn = getDbColumnFunc(mappingDto);
                    if (hasDuplicateOrEmptyHeader)
                    {
                        result.Add(header, dbColumn);
                    }
                    else
                    {
                        string fileHeader = fileHeaders[indexNo];
                        result.Add(fileHeader, dbColumn);
                    }
                    
                }
                indexNo++;
            }

            return result;
        }

        /// <summary>
        /// Now hasDuplicateColumn is false for all condtion 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="hasDuplicateColumn"></param>
        /// <returns></returns>

        public static string ConvertToRoman(string str,bool hasDuplicateColumn =false)
        {
            // string pattern = str.Contains("_") && hasDuplicateColumn ? @"(\d+)(?=_\d+$)" : @"\d+$";
            string pattern = str.Contains("_") && hasDuplicateColumn ? @"(\d+)(?=_\d+$)" : @"\d+";

            string output = Regex.Replace(str, pattern, match => {
                int num = int.Parse(match.Value);
                return ToRoman(num).Trim().ToLower();
            }); // Convert each number to Roman numeral });
            /*Match match = Regex.Match(str, pattern);

            if (match.Success)
            {
                int number = int.Parse(match.Value);
                string romanNumeral = ToRoman(number).Trim().ToLower();
                // Replace only the last matched number
                var value = str.Substring(0, match.Index) + romanNumeral + str.Substring(match.Index + match.Length);
                return  value;
            }*/

            return output; // Return original string if no number is found
        }

        public static string ToRoman(int number)
        {
            var romanNumerals = new (int value, string symbol)[]
            {
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"),
            (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"),
            (5, "V"), (4, "IV"), (1, "I")
            };

            var result = string.Empty;

            foreach (var (value, symbol) in romanNumerals)
            {
                while (number >= value)
                {
                    result += symbol;
                    number -= value;
                }
            }

            return result;
        }



        public static (bool,string) checkSelectedValidOptions(ConfigurationTableMappingDto parameterConfig,string keyColumns,string dedupColumnList)
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


        public static string GetSqlDataType(Type type)
        {
            if (type == typeof(byte[]))
            {
                return "VARBINARY(MAX)";
            }

            // Check specific types for Date, DateTime, and Time
            if (type == typeof(DateTime))
            {
                return "DATETIME";
            }
            else if (type == typeof(DateOnly)) // DateOnly for SQL DATE type
            {
                return "DATE";
            }
            else if (type == typeof(TimeOnly) || type == typeof(TimeSpan)) // TimeOnly or TimeSpan for SQL TIME type
            {
                return "TIME";
            }

            // Handle other data types
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean: return "BIT";
                case TypeCode.Byte: return "TINYINT";
                case TypeCode.Int16: return "SMALLINT";
                case TypeCode.Int32: return "INT";
                case TypeCode.Int64: return "BIGINT";
                case TypeCode.Single: return "REAL";
                case TypeCode.Double: return "FLOAT";
                case TypeCode.Decimal: return "DECIMAL(18, 2)";
                case TypeCode.String: return "NVARCHAR(MAX)";
                default: return "NVARCHAR(MAX)";
            }
        }


        public static string RemoveSuffixFromColumnDefinitions(string input)
        {
            // Use a regular expression to match and remove "|<dynamic_value>"
            return Regex.Replace(input, @"\|\d+", string.Empty);
        }

       


        public static bool IsCellEmpty(ICell cell)
        {
            if (cell == null)
                return true;
            if (cell.CellType == CellType.Blank)
                return true;
            if (cell.CellType == CellType.String && string.IsNullOrWhiteSpace(cell.StringCellValue))
                return true;
            // Optionally, treat cells with only whitespace as empty
            if (string.IsNullOrWhiteSpace(cell.ToString()))
                return true;
            return false;
        }

        public static string GetCellValue(ISheet sheet, ICell cell, string colDataType, string dateFormat, string fileExtention)
        {
            string cellValue = string.Empty;

            short formatIndex = cell.CellStyle != null ? cell.CellStyle.DataFormat : (short)0;
            string formatString = sheet.Workbook.GetCreationHelper().CreateDataFormat().GetFormat(formatIndex);

            if (!string.IsNullOrWhiteSpace(formatString))
            {
                formatString = ValidDateFormat(formatString);
            }

            DateTime? date = null;

            // Handle .xlsb as string values, try to parse to DateTime
            if (string.Equals(fileExtention, ".xlsb", StringComparison.OrdinalIgnoreCase))
            {
                var rawValue = cell?.ToString();
                if (DateTime.TryParse(rawValue, out DateTime parsed))
                {
                    date = parsed;

                    //string pattern = @"\b\d{1,2}/\d{1,2}/\d{4} \d{1,2}:\d{2}:\d{2} (AM|PM)(?: \+\d{2}:\d{2})?\b";
                    string pattern = @"[+-]\d{2}:\d{2}"; 

                    var match = Regex.Match(rawValue, pattern);



                    if (match.Success)
                    {
                        cellValue = date.Value.ToString("HH:mm:ss");
                        return cellValue;
                    }


                    string timePattern = @"^(?:[01]?\d|2[0-3]):[0-5]\d(?::[0-5]\d)?(?:\s?[APap][Mm])?$";

                    if (Regex.IsMatch(rawValue, timePattern))
                    {
                        if (DateTime.TryParse(rawValue, out DateTime parsedTime))
                        {
                            cellValue = parsedTime.ToString("HH:mm:ss");
                            return cellValue;
                        }
                    }


                }
            }
            // Handle standard Excel date cells (.xls, .xlsx)
            else if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
            {
                date = cell.DateCellValue;
            }

            // If a valid date is found, process it
            if (date.HasValue)
            {
                // Check if the date part is 1899-12-31 (Excel's default "empty" date)
                if (date.Value.Date == new DateTime(1899, 12, 31))
                {
                    // Only return time part if it's the "empty" date
                    cellValue = date.Value.ToString("HH:mm:ss");

                }
                else
                {
                    // Otherwise, return both date and time (full datetime)
                    if (!string.IsNullOrEmpty(dateFormat))
                    {
                        // Use the provided dateFormat (e.g., "yyyy-MM-dd HH:mm:ss")
                        cellValue = date.Value.ToString(dateFormat);
                    }
                    else
                    {
                        // Default datetime format
                        cellValue = date.Value.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                }
            }
            else
            {
                // Fallback: return raw cell content if not a date
                cellValue = cell?.ToString() ?? string.Empty;
            }

            return cellValue;
        }
        public static string GetUploadedFileId(FlpRequestDto flpRequestDto)
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

        public static DateTime? GetDateTimeValue(ICell cell, IFormulaEvaluator evaluator)
        {
            if (cell == null)
                return null;

            try
            {
                // Case 1: Agar formula hai → evaluate karo
                if (cell.CellType == CellType.Formula)
                {
                    var evaluated = evaluator.Evaluate(cell);
                    if (evaluated != null)
                    {
                        if (evaluated.CellType == CellType.Numeric && DateUtil.IsValidExcelDate(evaluated.NumberValue))
                        {
                            return DateTime.FromOADate(evaluated.NumberValue);
                        }
                        else if (evaluated.CellType == CellType.String &&
                                 DateTime.TryParse(evaluated.StringValue, out DateTime dt))
                        {
                            return dt;
                        }
                    }
                }
               

                // Case 3: String (UI me formatted)
                if (cell.CellType == CellType.String &&
                    DateTime.TryParse(cell.StringCellValue, out DateTime parsed))
                {
                    return parsed;
                }
            }
            catch
            {
                // ignore parsing issues, return null
            }

            return null;
        }

       
        public static string GetDateTimeCellStringValue(ISheet sheet, ICell cell, string colDataType, string dateFormat, string fileExtention)
        {
            string cellValue = string.Empty;
            //var formatIndex = cell.CellStyle.DataFormat;
            var formatIndex = cell.CellStyle.DataFormat;
            var formatString = sheet.Workbook.GetCreationHelper().CreateDataFormat().GetFormat(formatIndex);
            if (!string.IsNullOrWhiteSpace(formatString))
                formatString = ValidDateFormat(formatString);          

            if (formatString.ToLower().Contains("d") && formatString.ToLower().Contains("m") && (formatString.ToLower().Contains("y")))
            {
                //var timeOfDay = date.Value.TimeOfDay.ToString();
                if (formatString.Contains("h") || formatString.Contains("H"))
                {
                    cellValue = cell?.DateCellValue?.ToString() ?? string.Empty; //date.Value.ToString(formatString) ?? string.Empty;
                }
                else
                {
                    cellValue = cell?.DateOnlyCellValue?.ToString() ?? string.Empty; //date.Value.ToString(formatString) ?? string.Empty;
                }
            }
            else if (formatString.Contains("h") || formatString.Contains("H"))
            {
                // Likely a time format
                cellValue = cell?.TimeOnlyCellValue?.ToString() ?? string.Empty; //date.Value.ToString(formatString) ?? string.Empty;

            }
            
            return cellValue;
        }
        //previous working code 1
        //public static string GetCellValue(ISheet sheet, ICell cell, string colDataType, string dateFormat, string fileExtention)
        //{
        //    string cellValue = string.Empty;
        //    //var formatIndex = cell.CellStyle.DataFormat;
        //    var formatIndex = cell.CellStyle.DataFormat;
        //    var formatString = sheet.Workbook.GetCreationHelper().CreateDataFormat().GetFormat(formatIndex);
        //    if (!string.IsNullOrWhiteSpace(formatString))
        //        formatString = ValidDateFormat(formatString);

        //    DateTime? date = null;

        //    if (string.Equals(fileExtention, ".xlsb", StringComparison.OrdinalIgnoreCase))
        //    {
        //        var cellText = cell.ToString();
        //        if (DateTime.TryParse(cellText, out DateTime parsedDate))
        //        {
        //            date = parsedDate.Date; // Get only date
        //        }
        //    }
        //    else if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
        //    {
        //        date = cell.DateCellValue;
        //    }

        //    //DateTime? date = string.Compare(fileExtention, ".xlsb", true) == 0 ? Convert.ToDateTime(cell.ToString()) : cell.DateCellValue;
        //    if (date != null)
        //    {
        //        // Check for specific data type and format date accordingly
        //        if ((colDataType == DataTypeOptionsEnum.DATETIME.GetDescription() ||
        //             colDataType == DataTypeOptionsEnum.DATE.GetDescription() ||
        //             colDataType == DataTypeOptionsEnum.TIME.GetDescription()) && !string.IsNullOrEmpty(dateFormat))
        //        {
        //            cellValue = date.Value.ToString(dateFormat);
        //            //if (DateTime.TryParse(cell.StringCellValue, out DateTime dt))
        //            //    cellValue = dt.ToString("yyyy-MM-dd HH:mm:ss");
        //            //else
        //            //    cellValue = cell.StringCellValue; // fallback
        //        }
        //        else if (formatString.ToLower().Contains("d") && formatString.ToLower().Contains("m") && (formatString.ToLower().Contains("y")))
        //        {                                        
        //            //var timeOfDay = date.Value.TimeOfDay.ToString();
        //            if (formatString.Contains("h") || formatString.Contains("H"))
        //            {
        //                cellValue = cell?.DateCellValue?.ToString() ?? string.Empty; //date.Value.ToString(formatString) ?? string.Empty;
        //            }
        //            else
        //            {
        //                cellValue = cell?.DateOnlyCellValue?.ToString() ?? string.Empty; //date.Value.ToString(formatString) ?? string.Empty;
        //            }
        //        }
        //        else if (formatString.Contains("h") || formatString.Contains("H"))
        //        {
        //            // Likely a time format
        //             cellValue = cell?.TimeOnlyCellValue?.ToString() ?? string.Empty; //date.Value.ToString(formatString) ?? string.Empty;

        //        }
        //        else
        //        {

        //            if (!string.IsNullOrWhiteSpace(formatString))
        //                cellValue = date.Value.ToString(formatString);
        //            else
        //                cellValue = date.Value.ToString();


        //        }
        //    }
        //    return cellValue;
        //}
    }
}
