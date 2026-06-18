using Azure.Storage.Blobs;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using NPOI.OpenXml4Net.OPC.Internal;
using NPOI.SS.Formula.Functions;
using Parquet.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v3._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0.Authentication;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingProcess.TableSchemaValidation;
using Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Enums.v3._0;

namespace Teleperformance.DataIngestion.DataAccess.Services.v3._0
{
    public class ValidateSchemaService:IValidateSchemaService
    {
        private readonly IValidateSchemaRepository _validateSchemaRepository;
        private readonly ILogger<ValidateSchemaService> _logger;
        public ValidateSchemaService(IValidateSchemaRepository validateSchemaRepository, ILogger<ValidateSchemaService> logger)
        {
            _validateSchemaRepository = validateSchemaRepository;
            _logger = logger;           
        }      


        public async Task<Dictionary<string, FlpFileColumnMappingDto>> ValidateFileSchema(
         string flpConfigurationId,bool validateFileSchema,bool isHeaderProvided,List<string> fileHeaders,string processName,string tableName, IEnumerable<FlpFileColumnMappingDto> fileMappingColumnList = null)
        {
            var customHeadersColumnsMapping = new Dictionary<string, FlpFileColumnMappingDto>();
            List<string> duplicateKeyColumns = new List<string>();
            if(fileMappingColumnList == null)
                fileMappingColumnList = await GetFileColumnList(flpConfigurationId);

            if (validateFileSchema)
            {
                
                var missingHeaders = fileMappingColumnList
                    .Where(mapping => !fileHeaders.Contains(mapping.FileColumn, StringComparer.OrdinalIgnoreCase) &&
                                      !fileHeaders.Contains(mapping.DbColumn, StringComparer.OrdinalIgnoreCase))
                    .Select(mapping => mapping.FileColumn)
                    .ToList();
                if (missingHeaders.Any())
                {
                    throw new Exception($"File is missing headers: {string.Join(", ", missingHeaders)}.");
                }
            }

            //Validate schema - true 
            foreach (var fileHeader in fileHeaders)
            {
                //var cleanFileHeader = FlpConfigurationHelper.CleanColumnName(fileHeader);               
                var fileColumnMappingDto = fileMappingColumnList.LastOrDefault(x => validateFileSchema ? (x.FileColumn == fileHeader) : (x.FileColumn == fileHeader || x.DbColumn == fileHeader));
                if (fileColumnMappingDto != null)
                {
                    customHeadersColumnsMapping.Add(fileHeader, fileColumnMappingDto);

                    var dbColumnCounts = customHeadersColumnsMapping.Values
                       .GroupBy(x => x.DbColumn, StringComparer.OrdinalIgnoreCase)
                       .Select(g => new { DbColumn = g.Key, Count = g.Count() })
                       .Where(x => x.DbColumn == fileHeader)
                       .FirstOrDefault();

                    if (dbColumnCounts?.Count > 1)
                    {
                        duplicateKeyColumns.Add(fileHeader);
                    }
                }
                else
                {
                    //Fresh New column which does not exist in the table then we will insert 
                    if (!validateFileSchema)
                    {
                        // Create a new mapping for unmapped headers
                        var newMapping = new FlpFileColumnMappingDto
                        {
                            FileColumn = fileHeader,
                            DbColumn = fileHeader
                        };
                        customHeadersColumnsMapping.Add(fileHeader, newMapping);
                        //TODO:need to update this addionally column in the database
                        await _validateSchemaRepository.AddNewColumnMapping(flpConfigurationId, processName, tableName, fileHeader.ToUpper(), fileHeader.ToUpper(),"string", (int)DatatypeNamesEnum.STRING,null);

                        _logger.LogInformation($"New header: {fileHeader} found. Updated header in database.");
                    }

                }

            }           
            //Validate schema false check How many columns are duplicate in the dbcolumn
            if (!validateFileSchema)               
            {
                List<string> fileColumns = new List<string>();
                var list = customHeadersColumnsMapping.Values.Select(x => x.DbColumn).ToList();
                var existDuplicateColumn = FlpConfigurationHelper.ContainsDuplicatesOrEmptyHeader(list);
                if (existDuplicateColumn)
                {
                    fileColumns = FlpConfigurationHelper.CreateCustomHeader(list);                    
                    
                }
                int index = 0;
                foreach(var keyValuePair in customHeadersColumnsMapping)
                {
                    var dictValues = duplicateKeyColumns.Any(x => x == keyValuePair.Key);
                    if (dictValues)
                    {
                        
                        //value.DbColumn = fileColumns[index];
                        //Update dbColumn with file column
                        var newMapping = new FlpFileColumnMappingDto
                        {
                            FileColumn = keyValuePair.Key,
                            DbColumn = fileColumns[index]// New updated column name
                        };
                        customHeadersColumnsMapping[keyValuePair.Key] = newMapping;
                        //TODO:need to update this addionally column in the database
                        await _validateSchemaRepository.AddNewColumnMapping(flpConfigurationId, processName, tableName, keyValuePair.Key.ToUpper(), fileColumns[index].ToUpper(),"string", (int)DatatypeNamesEnum.STRING,null);

                        _logger.LogInformation($"New header: {fileColumns[index]} found. Updated header in database.");
                    }
                    index++;
                }
            }

            return customHeadersColumnsMapping;
        }
    
        public async Task<MappingTableSchemaResult?> CreateBronzeTableFromSharedLocation(string parquetFilePath, string connectionString, string processName, string tableName, string flpConfigurationId, string tabName, SharedLocationDestinationServerDto slDestinationServerDto, ParquetFileResponseDto resultResponse)
        {
            
            MappingTableSchemaResult mappingTableResponse = null;
            try
            {
                
                mappingTableResponse = new MappingTableSchemaResult();
                ParquetSchema schema = ParquetSchemaHelper.CreateSchema(resultResponse.ColumnDataTypeList);
                DataField[] dataFields = schema.GetDataFields();
                //mappingTableResponse.MatchSchema = await CreateSqlTableIfNotExistsAsync(connectionString,tableName, dataFields);
                (mappingTableResponse.MatchSchema, mappingTableResponse.FirstTimeTableCreated) = await CreateSqlTableIfNotExistsAsyncV2(connectionString, tableName, dataFields);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString());
                mappingTableResponse = new MappingTableSchemaResult();
                mappingTableResponse.ErrorMessage = ex.Message;
                mappingTableResponse.MatchSchema = false;
            }
            return mappingTableResponse;

        }

        public async Task<MappingTableSchemaResult?> CreateBronzeTableFromBlob(BlobClient blParquetBlobClient, string connectionString, string processName, string tableName, string flpConfigurationId, string tabName, ParquetFileResponseDto resultResponse)
        {
            MappingTableSchemaResult mappingTableResponse = null;

            try
            {
                
                mappingTableResponse = new MappingTableSchemaResult();
                ParquetSchema schema = ParquetSchemaHelper.CreateSchema(resultResponse.ColumnDataTypeList);
                DataField[] dataFields = schema.GetDataFields();
                //mappingTableResponse.MatchSchema = await CreateSqlTableIfNotExistsAsync(connectionString,tableName, dataFields);
                (mappingTableResponse.MatchSchema, mappingTableResponse.FirstTimeTableCreated) = await CreateSqlTableIfNotExistsAsyncV2(connectionString,tableName, dataFields);
                



            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString());
                mappingTableResponse = new MappingTableSchemaResult();
                mappingTableResponse.MatchSchema = false;
            }
            return mappingTableResponse;

        }


        public async Task<MappingTableSchemaResult?> CreateTempTableFromBlob(BlobClient blParquetBlobClient, string connectionString, string processName, string tableName, string flpConfigurationId, string tabName, ParquetFileResponseDto resultResponse)
        {
            MappingTableSchemaResult mappingTableResponse = null;

            try
            {

                mappingTableResponse = new MappingTableSchemaResult();
                ParquetSchema schema = ParquetSchemaHelper.CreateSchema(resultResponse.ColumnDataTypeList);
                DataField[] dataFields = schema.GetDataFields();
                //mappingTableResponse.MatchSchema = await CreateSqlTableIfNotExistsAsync(connectionString,tableName, dataFields);
                (mappingTableResponse.MatchSchema, mappingTableResponse.FirstTimeTableCreated) = await CreateSqlTempTableIfNotExistsAsync(connectionString, tableName, dataFields);




            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString());
                mappingTableResponse = new MappingTableSchemaResult();
                mappingTableResponse.MatchSchema = false;
            }
            return mappingTableResponse;

        }



        public async Task<MappingTableSchemaResult?> CreateSilverTableFromBlob(BlobClient blParquetBlobClient, string connectionString, string processName, string tableName, string flpConfigurationId, string tabName, ParquetFileResponseDto resultResponse)
        {
            MappingTableSchemaResult mappingTableResponse = null;

            try
            {
               // tableName = tableName + "_SILVER";
                mappingTableResponse = new MappingTableSchemaResult();
                ParquetSchema schema = ParquetSchemaHelper.CreateSchema(resultResponse.ColumnDataTypeList);
                DataField[] dataFields = schema.GetDataFields();                
                (mappingTableResponse.MatchSchema, mappingTableResponse.FirstTimeTableCreated) = await CreateSqlTableIfNotExistsAsyncV2(connectionString, tableName, dataFields);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString());
                mappingTableResponse = new MappingTableSchemaResult();
                mappingTableResponse.MatchSchema = false;
            }
            return mappingTableResponse;

        }



        public async Task<IEnumerable<FlpFileColumnMappingDto>> GetFileColumnList(string flpConfigurationId)
        {

            IEnumerable<FlpFileColumnMappingDto> columnList =new List<FlpFileColumnMappingDto>();
            var dbResult = await _validateSchemaRepository.GetFileColumnList(flpConfigurationId);
            if(dbResult != null && dbResult.Any())
            {
                columnList = dbResult.Select(cl => new FlpFileColumnMappingDto
                {
                    DataTypeId = cl.dataTypeId,
                    FileColumn = FlpConfigurationHelper.CleanColumnName(cl.fileColumn),
                    DbColumn = FlpConfigurationHelper.CleanColumnName(cl.dbColumn),
                    FlpConfigurationId = cl.flpConfigurationId,
                    FormatId = cl.formatId,
                    TabName = cl.tabName,
                    
                });
            }          
            return columnList;
        }

        public async Task<IEnumerable<FlpFileColumnMappingDto>> GetFileColumnListV2(string flpConfigurationId)
        {

            IEnumerable<FlpFileColumnMappingDto> columnList = new List<FlpFileColumnMappingDto>();
            var dbResult = await _validateSchemaRepository.GetFileColumnList(flpConfigurationId);
            if (dbResult != null && dbResult.Any())
            {
                columnList = dbResult.Select(cl => new FlpFileColumnMappingDto
                {
                    DataTypeId = cl.dataTypeId,
                    FileColumn = FlpConfigurationHelper.CleanColumnName(cl.fileColumn),
                    DbColumn = FlpConfigurationHelper.CleanColumnName(cl.dbColumn),
                    FlpConfigurationId = cl.flpConfigurationId,
                    FormatId = cl.formatId,
                    TabName = cl.tabName,
                    dataType = cl.dataType

                });
            }
            return columnList;
        }


        public async Task<IEnumerable<FlpFileColumnMappingDto>> GetFileColumnListV2ByTabName(string flpConfigurationId,string tabName)
        {

            IEnumerable<FlpFileColumnMappingDto> columnList = new List<FlpFileColumnMappingDto>();
            var dbResult = await _validateSchemaRepository.GetFileColumnListByTabName(flpConfigurationId, tabName);
            if (dbResult != null && dbResult.Any())
            {
                columnList = dbResult.Select(cl => new FlpFileColumnMappingDto
                {
                    DataTypeId = cl.dataTypeId,
                    FileColumn = FlpConfigurationHelper.CleanColumnName(cl.fileColumn),
                    DbColumn = FlpConfigurationHelper.CleanColumnName(cl.dbColumn),
                    FlpConfigurationId = cl.flpConfigurationId,
                    FormatId = cl.formatId,
                    TabName = cl.tabName,
                    dataType = cl.dataType

                });
            }
            return columnList;
        }


        private async Task<bool> CreateSqlTableIfNotExistsAsync(string connectionString, string tableName, DataField[] dataFields)
        {
            try
            {
                dataFields = FlpConfigurationHelper.CreateColumnInParquetFields(dataFields);
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    // Check if the table exists using a parameterized query to avoid SQL injection
                    var tableExistsQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
                    bool tableExists = await connection.ExecuteScalarAsync<int>(tableExistsQuery, new { TableName = tableName }) > 0;
                    if (tableExists)
                    {
                        // If the table exists, get the current columns and add new ones if necessary
                        var existingColumns = await GetAllColumnNamesAsync(connectionString, tableName);
                        var existingColumnSet = new HashSet<string>(existingColumns, StringComparer.OrdinalIgnoreCase); // Case-insensitive comparison
                                                                                                                        // Find new columns that are not in the existing table
                        var newColumns = dataFields
                            .Where(df => !existingColumnSet.Contains(df.Name))
                            .Select(df => $"ALTER TABLE {tableName} ADD [{df.Name}] {GetSqlDataType(df.ClrType)}")
                            .ToList();
                        if (newColumns.Any())
                        {
                            await ExecuteSchemaChanges(newColumns, connectionString);
                        }

                    }
                    else
                    {
                        // Create the table if it does not exist
                        var createTableQuery = GetCreateTableSql(tableName, dataFields);
                        var result = await connection.ExecuteAsync(createTableQuery);
                       
                    }


                }

                return true;
            }
            catch (Exception ex)
            {

                throw new Exception(ex.Message);
            }
        }


        private async Task<(bool,bool)> CreateSqlTableIfNotExistsAsyncV2(string connectionString, string tableName, DataField[] dataFields)
        {
            bool firstTimeTableCreated = false;
            
            try
            {
                //dataFields = FlpConfigurationHelper.CreateColumnInParquetFields(dataFields);
                dataFields = FlpConfigurationHelper.CreateColumnInParquetFieldsV2(dataFields);
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    // Check if the table exists using a parameterized query to avoid SQL injection
                    var tableExistsQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
                    bool tableExists = await connection.ExecuteScalarAsync<int>(tableExistsQuery, new { TableName = tableName }) > 0;
                    if (tableExists)
                    {
                        // If the table exists, get the current columns and add new ones if necessary
                        var existingColumns = await GetAllColumnNamesAsync(connectionString, tableName);
                        var existingColumnSet = new HashSet<string>(existingColumns, StringComparer.OrdinalIgnoreCase); // Case-insensitive comparison
                                                                                                                        // Find new columns that are not in the existing table
                        var newColumns = dataFields
                            .Where(df => !existingColumnSet.Contains(df.Name))
                            .Select(df => $"ALTER TABLE {tableName} ADD [{df.Name}] {GetSqlDataType(df.ClrType)}")
                            .ToList();
                        if (newColumns.Any())
                        {
                            await ExecuteSchemaChanges(newColumns, connectionString);
                        }

                    }
                    else
                    {
                        firstTimeTableCreated = true;
                        // Create the table if it does not exist
                        var createTableQuery = GetCreateTableSql(tableName, dataFields);
                        var result = await connection.ExecuteAsync(createTableQuery);

                    }


                }

                return (true,firstTimeTableCreated);
            }
            catch (Exception ex)
            {
                
                throw new Exception(ex.Message);
            }
        }

        private async Task<(bool, bool)> CreateSqlTempTableIfNotExistsAsync(string connectionString, string tableName, DataField[] dataFields)
        {
            bool firstTimeTableCreated = false;

            try
            {
                //dataFields = FlpConfigurationHelper.CreateColumnInParquetFields(dataFields);
                //dataFields = FlpConfigurationHelper.CreateColumnInParquetFieldsV2(dataFields);
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    // Check if the table exists using a parameterized query to avoid SQL injection
                    var tableExistsQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
                    bool tableExists = await connection.ExecuteScalarAsync<int>(tableExistsQuery, new { TableName = tableName }) > 0;
                    if (tableExists)
                    {
                        // If the table exists, get the current columns and add new ones if necessary
                        var existingColumns = await GetAllColumnNamesAsync(connectionString, tableName);
                        var existingColumnSet = new HashSet<string>(existingColumns, StringComparer.OrdinalIgnoreCase); // Case-insensitive comparison
                                                                                                                        // Find new columns that are not in the existing table
                        var newColumns = dataFields
                            .Where(df => !existingColumnSet.Contains(df.Name))
                            .Select(df => $"ALTER TABLE {tableName} ADD [{df.Name}] {GetSqlDataType(df.ClrType)}")
                            .ToList();
                        if (newColumns.Any())
                        {
                            await ExecuteSchemaChanges(newColumns, connectionString);
                        }

                    }
                    else
                    {
                        firstTimeTableCreated = true;
                        // Create the table if it does not exist
                        var createTableQuery = GetCreateTableSql(tableName, dataFields);
                        var result = await connection.ExecuteAsync(createTableQuery);

                    }


                }

                return (true, firstTimeTableCreated);
            }
            catch (Exception ex)
            {

                throw new Exception(ex.Message);
            }
        }

        //private async Task<bool> CreateSqlTableIfNotExistsAsync1(string connectionString,string tableName, DataField[] dataFields)
        //{
        //    bool response = true;
        //    bool tableExists = false;
        //    dataFields = FlpConfigurationHelper.CreateColumnInParquetFields(dataFields);
        //    using (var connection = new SqlConnection(connectionString))
        //    {
        //        await connection.OpenAsync();
        //        tableExists = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}'") > 0;
        //        //IF table doesn't exist in databae then will create
        //        if (!tableExists)
        //        {
        //            var createdQuery = GetCreateTableSql(tableName, dataFields);
        //            var ret =  await connection.ExecuteAsync(createdQuery);
        //            response = ret > 0;
        //        }
        //        else
        //        {
        //            List<string> alterCommands = new List<string>();
        //            //If table is exist then will find the new column
        //            //Get the old column list 
        //            var exitColumnsInTables = await GetAllColumnNamesAsync(connectionString, tableName);
        //            //New column List 
        //            //find new column which doesn't exist in old column list 
        //            var newColumnList = dataFields.Where(x => !exitColumnsInTables.Any(ec => ec.ToLower() == x.Name.ToLower())).ToList();
        //            //create alter commands
        //            foreach (var df in newColumnList)
        //            {
        //                alterCommands.Add($"ALTER TABLE {tableName} ADD {df.Name} {GetSqlDataType(df.ClrType)}");
        //            }
        //            if (alterCommands.Any())
        //            {
        //                // Execute the commands to update the table schema
        //                await ExecuteSchemaChanges(alterCommands, connectionString);
        //            }
        //        }


        //    }
        //    return response;
        //}

        private string GetCreateTableSql(string tableName, DataField[] dataFields)
        {
            var columnsSql = string.Join(", ", dataFields.Select(df => $"[{df.Name}] {GetSqlDataType(df.ClrType)}"));
            return $"CREATE TABLE [{tableName}] ({columnsSql})";
        }

        private string GetSqlDataType(Type type)
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

       

        private async Task ExecuteSchemaChanges(List<string> alterTableCommands, string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                foreach (var commandText in alterTableCommands)
                {
                    var command = new SqlCommand(commandText, connection);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }       

        public async Task<List<string>> GetAllColumnNamesAsync(string connectionString, string tableName)
        {
            // SQL query to get all column names for a given table
            var query = @"SELECT COLUMN_NAME 
                  FROM INFORMATION_SCHEMA.COLUMNS 
                  WHERE TABLE_NAME = @TableName 
                  ORDER BY ORDINAL_POSITION";

            var columnNames = new List<string>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    // Add parameters to prevent SQL injection
                    command.Parameters.AddWithValue("@TableName", tableName);

                    // Open the connection
                    await connection.OpenAsync();

                    // Execute the query and read all column names
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Get the column name and add it to the list
                            columnNames.Add(reader["COLUMN_NAME"].ToString());
                        }
                    }
                }
            }

            return columnNames;
        }
    }
}
