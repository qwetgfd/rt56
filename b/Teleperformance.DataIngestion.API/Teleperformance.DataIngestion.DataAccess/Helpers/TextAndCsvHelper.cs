using CsvHelper;
using Microsoft.IdentityModel.Tokens;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Enums.v3._0;

namespace Teleperformance.DataIngestion.DataAccess.Helpers
{
    public class TextAndCsvHelper
    {
        // Helper method to extract value by column name from dictionary in the row
        public static object GetDictionaryValue(List<object> row, string columnName)
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

        public static IComparable ConvertValue(object value, int dataTypeId)
        {
            if (value == null || value == DBNull.Value)
                return string.Empty; // Treat nulls as empty strings for sorting.

            //// Directly return if the value is already a comparable type
            //if (value is IComparable comparableValue)
            //    return comparableValue;

            if (value is string strValue)
            {
                // Check if it's a boolean
                if (dataTypeId == (int)DatatypeNamesEnum.BOOL && bool.TryParse(strValue, out bool boolValue))
                    return boolValue as IComparable;

                // Check if it's an integer
                if (dataTypeId == (int)DatatypeNamesEnum.INT && int.TryParse(strValue, out int intValue))
                    return intValue as IComparable;

                // Check if it's a long
                if (dataTypeId == (int)DatatypeNamesEnum.LONG && long.TryParse(strValue, out long longValue))
                    return longValue as IComparable;

                // Check if it's a float
                if (dataTypeId == (int)DatatypeNamesEnum.FLOAT && float.TryParse(strValue, out float floatValue))
                    return floatValue as IComparable;

                // Check if it's a double
                if (dataTypeId == (int)DatatypeNamesEnum.DOUBLE && double.TryParse(strValue, out double doubleValue))
                    return doubleValue as IComparable;

                // Check if it's a decimal
                if (dataTypeId == (int)DatatypeNamesEnum.FLOAT && decimal.TryParse(strValue, out decimal decimalValue))
                    return decimalValue as IComparable;

                // Check if it's a date
                if (dataTypeId == (int)DatatypeNamesEnum.DATETIME && DateTime.TryParse(strValue, out DateTime dateValue))
                    return dateValue as IComparable;


                if (dataTypeId == (int)DatatypeNamesEnum.DATE && DateOnly.TryParse(strValue, out DateOnly dateOnlyValue))
                    return dateOnlyValue as IComparable;


                // Check if it's a date
                if (dataTypeId == (int)DatatypeNamesEnum.TIME && TimeOnly.TryParse(strValue, out TimeOnly timeValue))
                    return timeValue as IComparable;

                return strValue; // Default to string if none of the above
            }

            // If the value is not a string, attempt to return it as IComparable
            return value as IComparable ?? value.ToString();
        }

        public static bool IsValidDelimiter(Stream csvStream, string paramCsvDelimiter)
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

        public static List<dynamic> GetRowsFromCsvReader(List<dynamic> csvRecords, Dictionary<string, string> customHeadersColumnsMapping, Dictionary<string, string> originalToTransformedColumnNameMap, ConfigurationTableMappingDto parameterConfig, HashSet<string> uniqueKeys, Dictionary<string, dynamic> tempRows, List<dynamic> rows, int actualRecordsCount, List<string> keyColumnList)
        {


            //while (csv.Read())
            for (int i = 0; i < actualRecordsCount; i++)
            {
                var fileRow = csvRecords[i];//csv.GetRecord<dynamic>();
                if (parameterConfig.SkipEmptyLines)
                {
                    if (IsEmptyLine(fileRow))
                    {
                        continue;
                    }
                }
                var fileRowDict = (IDictionary<string, object>)fileRow;

                // Use transformed column names

                // Use transformed column names
                try
                {
                    var transformedRowDict = fileRowDict.ToDictionary(
                   kvp => customHeadersColumnsMapping.ContainsKey(kvp.Key) ? customHeadersColumnsMapping[kvp.Key] : kvp.Key,
                   kvp => kvp.Value);
                    if (parameterConfig.IgnoreDuplicateRows)
                    {
                        List<string> keyColumns = new List<string>();
                        keyColumns = keyColumnList
                                   .Select(k => originalToTransformedColumnNameMap.ContainsKey(k.Trim())
                                       ? originalToTransformedColumnNameMap[k.Trim()]
                                       : k.Trim())
                                   .ToList();
                        // Generate key based on specified DataKey columns
                        //if (!customHeadersColumnsMapping.IsNullOrEmpty())
                        //{
                            
                        //    keyColumns = keyColumnList
                        //  .Select(k =>  customHeadersColumnsMapping.ContainsKey(k.Trim())
                        //      ? customHeadersColumnsMapping[k.Trim()]
                        //      : k.Trim())
                        //  .ToList();




                        //}
                        //else
                        //{
                        //    keyColumns = keyColumnList
                        //            .Select(k => originalToTransformedColumnNameMap.ContainsKey(k.Trim())
                        //                ? originalToTransformedColumnNameMap[k.Trim()]
                        //                : k.Trim())
                        //            .ToList();

                        //}

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
                catch (Exception ex)
                {

                    throw;
                }
               

                

            }

            return rows;
        }

        public static List<dynamic> GetRowsFromCsvReaderV2(List<dynamic> csvRecords, Dictionary<string, string> customHeadersColumnsMapping, Dictionary<string, string> originalToTransformedColumnNameMap, Models.DTOs.v4._1.FileLoadingConfigurationProcess.ConfigurationTableMappingDto parameterConfig, HashSet<string> uniqueKeys, Dictionary<string, dynamic> tempRows, List<dynamic> rows, int actualRecordsCount, List<string> keyColumnList)
        {


            //while (csv.Read())
            for (int i = 0; i < actualRecordsCount; i++)
            {
                var fileRow = csvRecords[i];//csv.GetRecord<dynamic>();
                if (parameterConfig.SkipEmptyLines)
                {
                    if (IsEmptyLine(fileRow))
                    {
                        continue;
                    }
                }
                var fileRowDict = (IDictionary<string, object>)fileRow;

                // Use transformed column names

                // Use transformed column names
                try
                {
                    var transformedRowDict = fileRowDict.ToDictionary(
                   kvp => customHeadersColumnsMapping.ContainsKey(kvp.Key) ? customHeadersColumnsMapping[kvp.Key] : kvp.Key,
                   kvp => kvp.Value);
                    if (parameterConfig.IgnoreDuplicateRows)
                    {
                        List<string> keyColumns = new List<string>();
                        keyColumns = keyColumnList
                                   .Select(k => originalToTransformedColumnNameMap.ContainsKey(k.Trim())
                                       ? originalToTransformedColumnNameMap[k.Trim()]
                                       : k.Trim())
                                   .ToList();
                        // Generate key based on specified DataKey columns
                        //if (!customHeadersColumnsMapping.IsNullOrEmpty())
                        //{

                        //    keyColumns = keyColumnList
                        //  .Select(k =>  customHeadersColumnsMapping.ContainsKey(k.Trim())
                        //      ? customHeadersColumnsMapping[k.Trim()]
                        //      : k.Trim())
                        //  .ToList();




                        //}
                        //else
                        //{
                        //    keyColumns = keyColumnList
                        //            .Select(k => originalToTransformedColumnNameMap.ContainsKey(k.Trim())
                        //                ? originalToTransformedColumnNameMap[k.Trim()]
                        //                : k.Trim())
                        //            .ToList();

                        //}

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
                catch (Exception ex)
                {

                    throw;
                }




            }

            return rows;
        }




        public static List<dynamic> GetRowsFromCsvReaderWithDedupColumn(List<dynamic> csvRecords, Dictionary<string, string> customHeadersColumnsMapping, Dictionary<string, string> originalToTransformedColumnNameMap, ConfigurationTableMappingDto parameterConfig, Dictionary<string, List<List<object>>> uniqueRowsWithDedup, List<dynamic> rows, int actualRecordsCount, List<string> keyColumnList,Dictionary<string,DataTypeDetails> dataTypeList)
        {


            //while (csv.Read())
            for (int i = 0; i < actualRecordsCount; i++)
            {
                var fileRow = csvRecords[i];//csv.GetRecord<dynamic>();
                if (parameterConfig.SkipEmptyLines)
                {
                    if (IsEmptyLine(fileRow))
                    {
                        continue;
                    }
                }
                var fileRowDict = (IDictionary<string, object>)fileRow;
                List<dynamic> listOfRows = new List<dynamic>();

                // Use transformed column names
                var transformedRowDict = fileRowDict.ToDictionary(
                    kvp => customHeadersColumnsMapping.ContainsKey(kvp.Key) ? customHeadersColumnsMapping[kvp.Key] : kvp.Key,
                    kvp => kvp.Value);

              
                
                foreach (var item in transformedRowDict.Keys.ToList()) 
                {
                    if (dataTypeList.TryGetValue(item, out DataTypeDetails dataTypeDetails))
                    {
                        transformedRowDict[item] = ConvertValueV2(transformedRowDict[item], dataTypeDetails); 
                    }
                }
                listOfRows.Add(transformedRowDict);
                List<string> keyColumns = new List<string>();
                // Generate key based on specified DataKey columns
                keyColumns = keyColumnList
                           .Select(k => originalToTransformedColumnNameMap.ContainsKey(k.Trim())
                               ? originalToTransformedColumnNameMap[k.Trim()]
                               : k.Trim())
                           .ToList();


             


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

        public static List<dynamic> GetRowsFromCsvReaderWithDedupColumnV2(List<dynamic> csvRecords, Dictionary<string, string> customHeadersColumnsMapping, Dictionary<string, string> originalToTransformedColumnNameMap, Models.DTOs.v4._1.FileLoadingConfigurationProcess.ConfigurationTableMappingDto  parameterConfig, Dictionary<string, List<List<object>>> uniqueRowsWithDedup, List<dynamic> rows, int actualRecordsCount, List<string> keyColumnList, Dictionary<string, DataTypeDetails> dataTypeList)
        {


            //while (csv.Read())
            for (int i = 0; i < actualRecordsCount; i++)
            {
                var fileRow = csvRecords[i];//csv.GetRecord<dynamic>();
                if (parameterConfig.SkipEmptyLines)
                {
                    if (IsEmptyLine(fileRow))
                    {
                        continue;
                    }
                }
                var fileRowDict = (IDictionary<string, object>)fileRow;
                List<dynamic> listOfRows = new List<dynamic>();

                // Use transformed column names
                var transformedRowDict = fileRowDict.ToDictionary(
                    kvp => customHeadersColumnsMapping.ContainsKey(kvp.Key) ? customHeadersColumnsMapping[kvp.Key] : kvp.Key,
                    kvp => kvp.Value);



                foreach (var item in transformedRowDict.Keys.ToList())
                {
                    if (dataTypeList.TryGetValue(item, out DataTypeDetails dataTypeDetails))
                    {
                        transformedRowDict[item] = ConvertValueV2(transformedRowDict[item], dataTypeDetails);
                    }
                }
                listOfRows.Add(transformedRowDict);
                List<string> keyColumns = new List<string>();
                // Generate key based on specified DataKey columns
                keyColumns = keyColumnList
                           .Select(k => originalToTransformedColumnNameMap.ContainsKey(k.Trim())
                               ? originalToTransformedColumnNameMap[k.Trim()]
                               : k.Trim())
                           .ToList();





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


        public static (List<dynamic>, HashSet<string>, Dictionary<string, dynamic>) GetFirstRows(CsvReader csv, Dictionary<string, string> customHeadersColumnsMapping, Dictionary<string, string> originalToTransformedColumnNameMap, ConfigurationTableMappingDto parameterConfig, HashSet<string> uniqueKeys, Dictionary<string, dynamic> tempRows, List<dynamic> rows, List<string> keyColumnList)
        {

            var fileRow = csv.GetRecord<dynamic>();
            
            var fileRowDict = (IDictionary<string, object>)fileRow;

            // Use transformed column names

            // Use transformed column names
            var transformedRowDict = fileRowDict.ToDictionary(
                kvp => customHeadersColumnsMapping.ContainsKey(kvp.Key) ? customHeadersColumnsMapping[kvp.Key] : kvp.Key,
                kvp => kvp.Value);


            if (parameterConfig.IgnoreDuplicateRows)
            {
                List<string> keyColumns = new List<string>();

                // Generate key based on specified DataKey columns
                if (!customHeadersColumnsMapping.Any())
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

        public static (List<dynamic>, HashSet<string>, Dictionary<string, dynamic>) GetFirstRowsV2(CsvReader csv, Dictionary<string, string> customHeadersColumnsMapping, Dictionary<string, string> originalToTransformedColumnNameMap, Models.DTOs.v4._1.FileLoadingConfigurationProcess.ConfigurationTableMappingDto parameterConfig, HashSet<string> uniqueKeys, Dictionary<string, dynamic> tempRows, List<dynamic> rows, List<string> keyColumnList)
        {

            var fileRow = csv.GetRecord<dynamic>();

            var fileRowDict = (IDictionary<string, object>)fileRow;

            // Use transformed column names

            // Use transformed column names
            var transformedRowDict = fileRowDict.ToDictionary(
                kvp => customHeadersColumnsMapping.ContainsKey(kvp.Key) ? customHeadersColumnsMapping[kvp.Key] : kvp.Key,
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


        public static object ConvertValueV2(object value, DataTypeDetails dataTypeDetails)
        {
            if (value == null || value == DBNull.Value)
                return string.Empty;
            string strValue = value.ToString().Trim();



            return dataTypeDetails.DataType switch
            {
                "int" => int.TryParse(strValue, out int intValue) ? intValue : null,
                "long" => long.TryParse(strValue, out long longValue) ? longValue : null,
                "float" => float.TryParse(strValue, out float floatValue) ? floatValue : null,
                "double" => double.TryParse(strValue, out double doubleValue) ? doubleValue : null,
                "bool" => bool.TryParse(strValue, out bool boolValue) ? boolValue : null,
                "datetime" => DateTime.TryParseExact(strValue,
                  dataTypeDetails.DataTypeFormat,
                  System.Globalization.CultureInfo.InvariantCulture,
                  System.Globalization.DateTimeStyles.None,
                  out DateTime datetimeValue) ? datetimeValue : (DateTime?)null,
                "date" => DateOnly.TryParseExact(strValue,
                  dataTypeDetails.DataTypeFormat,
                  System.Globalization.CultureInfo.InvariantCulture,
                  System.Globalization.DateTimeStyles.None,
                  out DateOnly dateOnlyValue) ? (DateOnly?)dateOnlyValue : null,
                 "time" => TimeOnly.TryParseExact(strValue,
                  dataTypeDetails.DataTypeFormat,
                  System.Globalization.CultureInfo.InvariantCulture,
                  System.Globalization.DateTimeStyles.None,
                  out TimeOnly timeOnlyValue) ? (TimeOnly?)timeOnlyValue : null,



                _ => strValue // Default case: return as string
            };
        }

        public static async Task<List<string>> ConvertSpanishToEnglish(List<string> filesHeaders, IProcessConfigServiceV3 _iProcessConfigurationService)
        {
            var tasks = filesHeaders.Select(async item =>
            {
                if (string.IsNullOrWhiteSpace(item)) return item; // Preserve empty values

                var result = await _iProcessConfigurationService.ConvertEnglishCharactersOnly(item, "spanish");

                if (result?.ResponseCode == 200)
                    return result.Result.ToLower();

                throw new Exception("Unable to convert Spanish to English file header");
            });

            return (await Task.WhenAll(tasks)).ToList();  //  Convert array to List<string>
        }




        public static bool IsEmptyLine(dynamic row)
        {
            if (row == null) return true;

            var dict = (IDictionary<string, object>)row;
            // Check if all values in the row are null, empty, or whitespace
            return dict.Values.All(value => value == null || string.IsNullOrWhiteSpace(value.ToString()));
        }




        public static (List<dynamic>, HashSet<string>, Dictionary<string, dynamic>) GetRowsFromCsvReaderV2(List<dynamic> csvRecords, Dictionary<string, string> customHeadersColumnsMapping, Dictionary<string, string> originalToTransformedColumnNameMap, ConfigurationTableMappingDto parameterConfig, HashSet<string> uniqueKeys, Dictionary<string, dynamic> tempRows, List<string> keyColumnList)
        {

            List<dynamic> rows = new List<dynamic>();
            //while (csv.Read())
            // for (int i = 0; i < actualRecordsCount; i++)
            foreach (var csvRecord in csvRecords)
            {

                var fileRow = csvRecord;//[i];//csv.GetRecord<dynamic>();
                if (parameterConfig.SkipEmptyLines)
                {
                    if (IsEmptyLine(fileRow))
                    {
                        continue;
                    }
                }
                var fileRowDict = (IDictionary<string, object>)fileRow;

                // Use transformed column names
                try
                {
                    var transformedRowDict = fileRowDict.ToDictionary(
                   kvp => customHeadersColumnsMapping.ContainsKey(kvp.Key) ? customHeadersColumnsMapping[kvp.Key] : kvp.Key,
                   kvp => kvp.Value);
                    if (parameterConfig.IgnoreDuplicateRows)
                    {
                        List<string> keyColumns = new List<string>();
                        keyColumns = keyColumnList
                                   .Select(k => originalToTransformedColumnNameMap.ContainsKey(k.Trim())
                                       ? originalToTransformedColumnNameMap[k.Trim()]
                                       : k.Trim())
                                   .ToList();
                        
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
                catch (Exception ex)
                {

                    throw;
                }
            }
            return (rows,uniqueKeys,tempRows);
        }


        public static (List<dynamic>, Dictionary<string, List<List<object>>>) GetRowsFromCsvReaderWithDedupColumnV2(List<dynamic> csvRecords, Dictionary<string, string> customHeadersColumnsMapping, 
            Dictionary<string, string> originalToTransformedColumnNameMap, ConfigurationTableMappingDto parameterConfig,
            Dictionary<string, List<List<object>>> uniqueRowsWithDedup, List<string> keyColumnList, Dictionary<string, DataTypeDetails> dataTypeList)
        {


            List<dynamic> rows = new List<dynamic>();
            // for (int i = 0; i < actualRecordsCount; i++)
            foreach (var csvRecord in csvRecords)
            {
                var fileRow = csvRecord;//csv.GetRecord<dynamic>();
                if (parameterConfig.SkipEmptyLines)
                {
                    if (IsEmptyLine(fileRow))
                    {
                        continue;
                    }
                }
                var fileRowDict = (IDictionary<string, object>)fileRow;
                List<dynamic> listOfRows = new List<dynamic>();

                // Use transformed column names
                var transformedRowDict = fileRowDict.ToDictionary(
                    kvp => customHeadersColumnsMapping.ContainsKey(kvp.Key) ? customHeadersColumnsMapping[kvp.Key] : kvp.Key,
                    kvp => kvp.Value);



                foreach (var item in transformedRowDict.Keys.ToList())
                {
                    if (dataTypeList.TryGetValue(item, out DataTypeDetails dataTypeDetails))
                    {
                        transformedRowDict[item] = ConvertValueV2(transformedRowDict[item], dataTypeDetails);
                    }
                }
                listOfRows.Add(transformedRowDict);
                List<string> keyColumns = new List<string>();
                // Generate key based on specified DataKey columns
                keyColumns = keyColumnList
                           .Select(k => originalToTransformedColumnNameMap.ContainsKey(k.Trim())
                               ? originalToTransformedColumnNameMap[k.Trim()]
                               : k.Trim())
                           .ToList();





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

            return (rows, uniqueRowsWithDedup);
        }




    }
}
