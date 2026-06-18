using Azure.Storage.Blobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Parquet.Schema;
using Parquet;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using Dapper;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingProcess.TableSchemaValidation;
using Azure;
using Microsoft.Extensions.Logging;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;

namespace Teleperformance.DataIngestion.DataAccess.Services.v1._0
{
    public class SchemaValidationService: ISchemaValidationService
    {
        //private readonly IConfiguration _configuration;
        // private string DBConnectionString;
        private readonly ISMBLibraryServices _ismbLibraryServices;
        private readonly IBlobStorageService _iBlobStorageService;
        private readonly IBronzeDbRepository _iBronzeDbRepository;
        private readonly ILogger<SchemaValidationService> _ilogger;
        public SchemaValidationService(ISMBLibraryServices ismbLibraryServices, IBlobStorageService iBlobStorageService, IBronzeDbRepository iBronzeDbRepository, ILogger<SchemaValidationService> ilogger)
        {
            _ismbLibraryServices = ismbLibraryServices;
            _iBlobStorageService = iBlobStorageService;
            _iBronzeDbRepository = iBronzeDbRepository;
            _ilogger = ilogger;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parquetFilePath"></param>
        /// <param name="connectionString"></param>
        /// <param name="processName"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        //public async Task<MappingTableSchemaResult?> CheckTableSchemaAndCreatTable(string parquetFilePath, string connectionString, string processName, string tableName)
        //{
        //    MappingTableSchemaResult mappingTableResponse = null;
        //    using (var fileStream = File.OpenRead(parquetFilePath))
        //    {
        //        using (var parquetReader = await ParquetReader.CreateAsync(fileStream))
        //        {
        //            mappingTableResponse = new MappingTableSchemaResult();
        //            DataField[] dataFields = parquetReader.Schema.GetDataFields();
        //            mappingTableResponse.MatchSchema = await CreateSqlTableIfNotExistsAsync(connectionString, processName, tableName, dataFields);
        //        }
        //    }
        //    return mappingTableResponse;

        //}


        public async Task<MappingTableSchemaResult?> CreateBronzeTableFromSharedLocation(string parquetFilePath, string connectionString, string processName, string tableName, string flpConfigurationId,string tabName, SharedLocationDestinationServerDto slDestinationServerDto, ParquetFileResponseDto resultResponse)
        {
            /*CheckConnectivitySMBLibraryModel destinationServerModel = new CheckConnectivitySMBLibraryModel
            {
                serverIP = slDestinationServerDto.ServerName,
                username = slDestinationServerDto.UserName,
                password = slDestinationServerDto.Password,
                sharedFolderName = slDestinationServerDto.FolderName,
                domain = slDestinationServerDto.Domain,
                sourceFilePath = parquetFilePath
            };*/
            MappingTableSchemaResult mappingTableResponse = null;
            try
            {
                /* using (var fileStream = _ismbLibraryServices.SMBRequest(destinationServerModel, flpConfigurationId, SMBRequestEnum.Stream).GetFileStream)
                 {
                     using (var parquetReader = await ParquetReader.CreateAsync(fileStream))
                     {
                         mappingTableResponse = new MappingTableSchemaResult();
                         DataField[] dataFields = parquetReader.Schema.GetDataFields();
                         mappingTableResponse.MatchSchema = await CreateSqlTableIfNotExistsAsync(connectionString, flpConfigurationId, tabName, processName, tableName, dataFields);
                     }
                 */
                mappingTableResponse = new MappingTableSchemaResult();
                ParquetSchema schema = ParquetSchemaHelper.CreateSchema(resultResponse.ColumnDataTypeList);
                DataField[] dataFields = schema.GetDataFields();
                mappingTableResponse.MatchSchema = await CreateSqlTableIfNotExistsAsync(connectionString, flpConfigurationId, tabName, processName, tableName, dataFields);
            }
            catch (Exception ex)
            {
                _ilogger.LogError(ex.Message.ToString());                
                mappingTableResponse = new MappingTableSchemaResult();
                mappingTableResponse.ErrorMessage = ex.Message;
                mappingTableResponse.MatchSchema = false;
            }
            return mappingTableResponse;

        }


        public async Task<MappingTableSchemaResult?> CreateBronzeTableFromBlob(BlobClient blParquetBlobClient, string connectionString, string processName, string tableName,string flpConfigurationId,string tabName, ParquetFileResponseDto resultResponse)
        {
            MappingTableSchemaResult mappingTableResponse = null;

            try
            {
                //using (Stream parquetStream = await _iBlobStorageService.ReadParquetFromBlobAsync(blParquetBlobClient))
                //{
                //    using (var parquetReader = await ParquetReader.CreateAsync(parquetStream))
                //    {
                //        mappingTableResponse = new MappingTableSchemaResult();
                //        DataField[] dataFields = parquetReader.Schema.GetDataFields();
                //        mappingTableResponse.MatchSchema = await CreateSqlTableIfNotExistsAsync(connectionString, flpConfigurationId,tabName, processName, tableName, dataFields);
                //    }
                //}
                mappingTableResponse = new MappingTableSchemaResult();
                ParquetSchema schema = ParquetSchemaHelper.CreateSchema(resultResponse.ColumnDataTypeList);
                DataField[] dataFields = schema.GetDataFields(); 
                mappingTableResponse.MatchSchema = await CreateSqlTableIfNotExistsAsync(connectionString, flpConfigurationId, tabName, processName, tableName, dataFields);
                

            }
            catch (Exception ex)
            {
                _ilogger.LogError(ex.Message.ToString());
                mappingTableResponse = new MappingTableSchemaResult();
                mappingTableResponse.MatchSchema = false;
            }
            return mappingTableResponse;

        }




        private async Task<bool> CreateSqlTableIfNotExistsAsync(string connectionString,string  flpConfigurationId,string tabName, string processName, string tableName, DataField[] dataFields)
        {
            bool response = true;
            bool tableExists = false;
            dataFields = FlpConfigurationHelper.CreateColumnInParquetFields(dataFields);
            //if(dataFields?.Count() > 0)
            //{
            //    // Add the new DataField for InsertionDateTime
            //    var insertionDateField = new DataField<DateTime?>("insertionDateTime");
            //    dataFields = dataFields.Concat(new[] { insertionDateField }).ToArray();

            //}
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                tableExists = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}'") > 0;
                //IF table doesn't exit in databae then will create
                if (!tableExists)
                {
                    var createdQuery = GetCreateTableSql(tableName, dataFields);
                     await connection.ExecuteAsync(createdQuery);
                }


            }
            response = await ValidateSchema(tableExists,flpConfigurationId,tabName, processName, tableName, connectionString, dataFields);
            return response;
        }


        private async Task<bool> ValidateSchema(bool tableExists,string flpConfigurationId,string tabName, string processName, string tableName,string connectionString, DataField[] dataFields)
        {
            bool response = true;
            if (!tableExists)
            {
              
                //Inserted all columns 
                List<MappingTableDbRequest> list = new List<MappingTableDbRequest>();
                foreach (var field in dataFields)
                {
                    MappingTableDbRequest mappingTableDbRequest = new MappingTableDbRequest();
                    mappingTableDbRequest.ProcessName = processName;
                    mappingTableDbRequest.TableName = tableName;
                    mappingTableDbRequest.ColumnName = field.Name;
                    mappingTableDbRequest.DataType = GetSqlDataType(field.ClrType);
                    mappingTableDbRequest.ActionType = "Insert";
                    list.Add(mappingTableDbRequest);
                }
                foreach (var data in list)
                {
                    var dbResponse = await _iBronzeDbRepository.CommitMappingColumnList(data.ProcessName, data.TableName, data.ColumnName, data.DataType, data.ActionType,flpConfigurationId,tabName);
                    if ((string.Compare(dbResponse.Result, "Failure", true) == 0) || (string.Compare(dbResponse.Result, "Error", true) == 0))
                    {
                        response = false;
                        //Add log
                        break;
                        //Add log
                    }

                }

            }
            else
            {
                //  Retrieve the mapping column list from the database
                var mappingColumnList = await _iBronzeDbRepository.GetMappingColumnList(flpConfigurationId, tableName);
                // Create a list to hold columns from the file
                List<MappingTableDbRequest> filelist = new List<MappingTableDbRequest>();
                // Populate filelist with columns from the file
                foreach (var field in dataFields)
                {
                    MappingTableDbRequest mappingTableDbRequest = new MappingTableDbRequest
                    {
                        ProcessName = processName,
                        FlpConfigurationId = processName,
                        TableName = tableName,
                        ColumnName = field.Name,
                        DataType = GetSqlDataType(field.ClrType),
                        ActionType = "Insert"
                    };
                    filelist.Add(mappingTableDbRequest);
                }
                /////////////////////////////////                  
                var res = false;
                List<string> alterCommands = new List<string>();
                (res, alterCommands) = await CompareSchemasAsync(connectionString,flpConfigurationId,tabName,tableName,filelist, mappingColumnList.Where(x => x.Active).ToList(), mappingColumnList.Where(x => !x.Active).ToList());
                if (res & alterCommands.Any())
                {
                    // Execute the commands to update the table schema
                    await ExecuteSchemaChanges(alterCommands, connectionString);
                }
               

            }
            return response;
        }


        private async Task<(bool, List<string>)> CompareSchemasAsync(string connectionString,string flpConfigurationId,string tabName,string tableName,List<MappingTableDbRequest> filelist, List<ParquetColumnMapping> mappingColumnList, List<ParquetColumnMapping> deletedColMappingList)
        {
            bool response = false;
            List<string> alterCommands = new List<string>();
            var exitColumnsInTables = await GetAllColumnNamesAsync(connectionString, tableName);
            // Loop through filelist and compare with mappingColumnList
            foreach (var fileColumn in filelist)
            {
                var matchedColumn = mappingColumnList.FirstOrDefault(m => m.ColumnName == fileColumn.ColumnName);
                if (matchedColumn != null)
                {
                    if (fileColumn.DataType != matchedColumn.DataType)
                    {
                        response = false;
                        break;
                        // DataType is different
                        // sameSchema.Add($"Column {fileColumn.ColumnName} has different DataType: FileList {fileColumn.DataType}, MappingList {matchedColumn.DataType}");                           
                    }
                    if (fileColumn.DataType == matchedColumn.DataType)
                    {
                        response = true;
                        //Check the table column name  exit  or not. If column does not exit due to any reason then will alter the column again
                        if (!exitColumnsInTables.Contains(fileColumn.ColumnName))
                            alterCommands.Add($"ALTER TABLE {fileColumn.TableName} ADD {fileColumn.ColumnName} {fileColumn.DataType}");
                    }
                }
                else
                {
                    // Column exists in filelist but not in mappingColumnList
                    //extraInFileList.Add($"Column {fileColumn.ColumnName} exists in FileList but not in MappingList");
                    //extraInFileList.Add(fileColumn);
                    var dbResponse = await _iBronzeDbRepository.CommitMappingColumnList(fileColumn.ProcessName,
                       fileColumn.TableName, fileColumn.ColumnName, fileColumn.DataType, "Insert", flpConfigurationId,tabName);
                    if ((string.Compare(dbResponse.Result, "Failure", true) == 0) || (string.Compare(dbResponse.Result, "Error", true) == 0))
                    {
                        response = false;
                        //Add log
                        break;
                        //Add log
                    }
                    else
                    {
                        response = true;
                        if (!deletedColMappingList.Any(x => x.TableName == fileColumn.TableName && x.ColumnName == fileColumn.ColumnName) && (!exitColumnsInTables.Contains(fileColumn.ColumnName)))
                            alterCommands.Add($"ALTER TABLE {fileColumn.TableName} ADD {fileColumn.ColumnName} {fileColumn.DataType}");
                    }
                }
            }

            // Loop through mappingColumnList to find columns that do not exist in filelist
            foreach (var mappingColumn in mappingColumnList)
            {
                var matchedFileColumn = filelist.FirstOrDefault(f => f.ColumnName == mappingColumn.ColumnName);

                if (matchedFileColumn == null)
                {
                    // Column exists in mappingColumnList but not in filelist
                    // extraInMappingList.Add($"Column {mappingColumn.ColumnName} exists in MappingList but not in FileList");

                    var dbResponse = await _iBronzeDbRepository.CommitMappingColumnList(mappingColumn.ProcessName,
                        mappingColumn.TableName, mappingColumn.ColumnName, mappingColumn.DataType, "Delete",flpConfigurationId,tabName);
                    if ((string.Compare(dbResponse.Result, "Failure", true) == 0) || (string.Compare(dbResponse.Result, "Error", true) == 0))
                    {
                        response = false;
                        //Add log
                        break;
                        //Add log
                    }
                    else
                    {
                        response = true;
                    }
                }
            }
            if (!response)
            {
                //If we got any issue then alterncommands will not run for generated table 
                alterCommands = new List<string>();
            }
            return (response, alterCommands);
        }


        private string GetCreateTableSql(string tableName, DataField[] dataFields)
        {
            var columnsSql = string.Join(", ", dataFields.Select(df => $"[{df.Name}] {GetSqlDataType(df.ClrType)}"));
            return $"CREATE TABLE [{tableName}] ({columnsSql})";
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

    }
}
