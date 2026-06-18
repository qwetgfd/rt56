using Azure.Storage.Blobs;
using Microsoft.Data.SqlClient;
using Parquet.Schema;
using Parquet;
using Dapper;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Models.v1._0;
using DataColumn = Parquet.Data.DataColumn;
using System.Data;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingProcess.TableSchemaValidation;
using Microsoft.Extensions.Logging;
using static Dapper.SqlMapper;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using ZstdSharp.Unsafe;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using System.Text;
using Teleperformance.DataIngestion.Common.Crypto;
using NPOI.SS.Formula.Functions;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v1._0
{
    public class FLPBronzeDbRepository: IBronzeDbRepository
    {
        private readonly ISMBLibraryServices _ismbLibraryServices;
        private readonly IBlobStorageService _iBlobStorageService;
        private readonly ILogger<FLPBronzeDbRepository> _logger;
        private readonly IDapperService _dapperService;
        public FLPBronzeDbRepository(ISMBLibraryServices ismbLibraryServices, IBlobStorageService iBlobStorageService, ILogger<FLPBronzeDbRepository> logger, IDapperService dapperService)
        {
            _ismbLibraryServices = ismbLibraryServices;
            _iBlobStorageService = iBlobStorageService;
            _logger = logger;
            _dapperService = dapperService;
        }
        public async Task<IEnumerable<ParquetColumnMapping>> GetMappingColumnList(string flpConfigurationId, string tableName)
        {
            try
            {
               
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@tableName", tableName);

                var csvConfiguration = await _dapperService.GetMultipleRowsAsync<ParquetColumnMapping>("[sel_mappingColumnList]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return csvConfiguration;

            }
            catch (Exception ex)
            {
               _logger.LogError(ex.Message.ToString());
               throw;
            }



        }
        public async Task<DatabaseResponse> CommitMappingColumnList(string processName, string tableName, string columnName, string dataType, string actionType,string flpConfigurationId,string tabName)
        {
            try
            {
                
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@processName", processName);
                dynamicParameters.Add("@tableName", tableName);
                dynamicParameters.Add("@columnName", columnName);
                dynamicParameters.Add("@dataType", dataType);
                dynamicParameters.Add("@actionType", actionType);
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@tabName", tabName);

                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_parquetTableColumnMapping]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString());
                throw;
            }



        }

        public async Task<bool> ConvertParquetToSqlAsync(string parquetFilePath, string connectionString, string tableName)
        {
            using (var fileStream = File.OpenRead(parquetFilePath))
            {
                using (var parquetReader = await ParquetReader.CreateAsync(fileStream))
                {
                    DataField[] dataFields = parquetReader.Schema.GetDataFields();
                    await CreateSqlTableIfNotExistsAsync(connectionString, tableName, dataFields);

                    using (var sqlConnection = new SqlConnection(connectionString))
                    {
                        await sqlConnection.OpenAsync();

                        for (int i = 0; i < parquetReader.RowGroupCount; i++)
                        {
                            using (ParquetRowGroupReader groupReader = parquetReader.OpenRowGroupReader(i))
                            {
                                List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
                                foreach (DataField dataField in dataFields)
                                {
                                    DataColumn column = await groupReader.ReadColumnAsync(dataField);
                                    Array values = column.Data;
                                    for (int j = 0; j < values.Length; j++)
                                    {
                                        if (rows.Count <= j)
                                        {
                                            rows.Add(new Dictionary<string, object>());
                                        }
                                        // Replace spaces with underscores in parameter names
                                        string parameterName = dataField.Name;
                                        rows[j][parameterName] = values.GetValue(j);
                                    }
                                }

                                foreach (var row in rows)
                                {
                                    var insertQuery = GetInsertSql(dataFields, tableName);
                                    await sqlConnection.ExecuteAsync(insertQuery, row);
                                }
                            }
                        }
                    }
                }
            }
            return true;

        }
      
        public async Task<(bool Success, int TotalInsertedRecords)> InsertDataFromParquetSharedLocationAsync(string parquetFilePath, string connectionString, string tableName, string flpConfigurationId, SharedLocationDestinationServerDto slDestinationServerDto,string uploadFileId,string fileName)
        {
            CheckConnectivitySMBLibraryModel destinationServerModel = new CheckConnectivitySMBLibraryModel
            {
                serverIP = slDestinationServerDto.ServerName,
                username = slDestinationServerDto.UserName,
                password = slDestinationServerDto.Password,
                sharedFolderName = slDestinationServerDto.FolderName,
                domain = slDestinationServerDto.Domain,
                sourceFilePath = parquetFilePath
            };

            int totalInsertedRecords = 0;

            try
            {
                using (var fileStream = _ismbLibraryServices.SMBRequest(destinationServerModel, flpConfigurationId, SMBRequestEnum.Stream).GetFileStream)
                {
                    using (var parquetReader = await ParquetReader.CreateAsync(fileStream))
                    {
                        DataField[] dataFields = parquetReader.Schema.GetDataFields();
                       // FlpConfigurationHelper.CreateColumnInParquetFields(dataFields);
                        DataTable dataTable = CreateDataTableV2(dataFields);
                        
                        using (var sqlConnection = new SqlConnection(connectionString))
                        {
                            await sqlConnection.OpenAsync();

                            using (var sqlTransaction = sqlConnection.BeginTransaction())
                            {
                                try
                                {

                                    for (int i = 0; i < parquetReader.RowGroupCount; i++)
                                    {
                                        using (ParquetRowGroupReader groupReader = parquetReader.OpenRowGroupReader(i))
                                        {
                                            await FillDataTableV2(dataTable, groupReader, dataFields,fileName,uploadFileId);

                                            // Bulk insert into SQL
                                            using (var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.Default, sqlTransaction))
                                            {
                                                bulkCopy.BulkCopyTimeout = 0;
                                                bulkCopy.BatchSize = dataTable.Rows.Count > 300000 ? 100000 : 50000;
                                                bulkCopy.DestinationTableName = tableName;

                                                // Explicitly map the columns from DataTable to the SQL table
                                                foreach (System.Data.DataColumn column in dataTable.Columns)
                                                {
                                                    bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                                                }

                                                await bulkCopy.WriteToServerAsync(dataTable);
                                            }

                                            // Update total records inserted
                                            totalInsertedRecords += dataTable.Rows.Count;

                                            // Clear DataTable for the next row group
                                            dataTable.Clear();
                                        }
                                    }

                                    // Commit the transaction if all inserts succeed
                                    await sqlTransaction.CommitAsync();
                                }
                                catch (Exception commitEx)
                                {
                                    totalInsertedRecords = 0;
                                    // Rollback the transaction if any insert fails
                                    await sqlTransaction.RollbackAsync();
                                    _logger.LogError($"Error: records not inserted due to error: {commitEx.Message} for {flpConfigurationId}");
                                    return (false, totalInsertedRecords);
                                }
                            }
                        }
                    }
                }

                return (true, totalInsertedRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message} for {flpConfigurationId}");
                return (false, totalInsertedRecords);
            }
        }


        public async Task<bool> InsertDataFromParquetStreamAsync(string parquetFilePath, string connectionString, string tableName)
        {
            using (var fileStream = File.OpenRead(parquetFilePath))
            {
                using (var parquetReader = await ParquetReader.CreateAsync(fileStream))
                {
                    DataField[] dataFields = parquetReader.Schema.GetDataFields();
                    // await CreateSqlTableIfNotExistsAsync(connectionString, tableName, dataFields);

                    using (var sqlConnection = new SqlConnection(connectionString))
                    {
                        await sqlConnection.OpenAsync();

                        for (int i = 0; i < parquetReader.RowGroupCount; i++)
                        {
                            using (ParquetRowGroupReader groupReader = parquetReader.OpenRowGroupReader(i))
                            {
                                List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
                                foreach (DataField dataField in dataFields)
                                {
                                    DataColumn column = await groupReader.ReadColumnAsync(dataField);
                                    Array values = column.Data;
                                    for (int j = 0; j < values.Length; j++)
                                    {
                                        if (rows.Count <= j)
                                        {
                                            rows.Add(new Dictionary<string, object>());
                                        }
                                        // Replace spaces with underscores in parameter names
                                        string parameterName = dataField.Name;
                                        rows[j][parameterName] = values.GetValue(j);
                                    }
                                }

                                foreach (var row in rows)
                                {
                                    var insertQuery = GetInsertSql(dataFields, tableName);
                                    await sqlConnection.ExecuteAsync(insertQuery, row);
                                }
                            }
                        }
                    }
                }
            }
            return true;

        }
        public  async Task<bool> InsertDataFromParquetStream(BlobClient parquetBlobClient, string connectionString, string tableName)
        {
            try
            {
                using (Stream parquetStream = await _iBlobStorageService.ReadParquetFromBlobAsync(parquetBlobClient))
                {
                    using (var parquetReader = await ParquetReader.CreateAsync(parquetStream))
                    {
                        DataField[] dataFields = parquetReader.Schema.GetDataFields();
                        //await CreateSqlTableIfNotExistsAsync(connectionString, tableName, dataFields);

                        using (var sqlConnection = new SqlConnection(connectionString))
                        {
                            await sqlConnection.OpenAsync();

                            for (int i = 0; i < parquetReader.RowGroupCount; i++)
                            {
                                using (ParquetRowGroupReader groupReader = parquetReader.OpenRowGroupReader(i))
                                {
                                    List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
                                    foreach (DataField dataField in dataFields)
                                    {
                                        DataColumn column = await groupReader.ReadColumnAsync(dataField);
                                        Array values = column.Data;
                                        for (int j = 0; j < values.Length; j++)
                                        {
                                            if (rows.Count <= j)
                                            {
                                                rows.Add(new Dictionary<string, object>());
                                            }
                                            // Replace spaces with underscores in parameter names
                                            string parameterName = dataField.Name;
                                            rows[j][parameterName] = values.GetValue(j);

                                        }
                                    }
                                    foreach (var row in rows)
                                    {
                                        var insertQuery = GetInsertSql(dataFields, tableName);
                                        await sqlConnection.ExecuteAsync(insertQuery, row);
                                    }
                                }
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString());
                return false;
            }

        }

        public async Task<(bool Success, int TotalInsertedRecords)> InsertDataFromParquetSharedLocationAsync(string parquetFilePath, string connectionString, string tableName, string flpConfigurationId, SharedLocationDestinationServerDto slDestinationServerDto,bool mergeData,bool createHistory,string historyTableName, string uploadFileId, string fileName)
        {
            CheckConnectivitySMBLibraryModel destinationServerModel = new CheckConnectivitySMBLibraryModel
            {
                serverIP = slDestinationServerDto.ServerName,
                username = slDestinationServerDto.UserName,
                password = slDestinationServerDto.Password,
                sharedFolderName = slDestinationServerDto.FolderName,
                domain = slDestinationServerDto.Domain,
                sourceFilePath = parquetFilePath
            };

            int totalInsertedRecords = 0;

            try
            {
                using (var fileStream = _ismbLibraryServices.SMBRequest(destinationServerModel, flpConfigurationId, SMBRequestEnum.Stream).GetFileStream)
                {
                    using (var parquetReader = await ParquetReader.CreateAsync(fileStream))
                    {
                        DataField[] dataFields = parquetReader.Schema.GetDataFields();
                       // FlpConfigurationHelper.CreateColumnInParquetFieldsV2(dataFields);
                        DataTable dataTable = CreateDataTableV2(dataFields);

                        using (var sqlConnection = new SqlConnection(connectionString))
                        {
                            await sqlConnection.OpenAsync();

                            using (var sqlTransaction = sqlConnection.BeginTransaction())
                            {
                                try
                                {
                                    if (mergeData & createHistory)
                                    {
                                        //Create History Table
                                        //
                                        var toBeCreatedHistoryTable = await CheckTableExistOrNot(sqlConnection, sqlTransaction, historyTableName);
                                        if (!toBeCreatedHistoryTable)
                                        {
                                            string historyTableGenerateQuery = GenerateTempTableQuery(historyTableName, dataFields);

                                            using (var createTableCommand = new SqlCommand(historyTableGenerateQuery, sqlConnection, sqlTransaction))
                                            {
                                                await createTableCommand.ExecuteNonQueryAsync();
                                            }
                                        }
                                                                               
                                    }

                                    for (int i = 0; i < parquetReader.RowGroupCount; i++)
                                    {
                                        using (ParquetRowGroupReader groupReader = parquetReader.OpenRowGroupReader(i))
                                        {
                                            await FillDataTableV2(dataTable, groupReader, dataFields, fileName, uploadFileId);

                                            // Bulk insert into SQL
                                            using (var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.Default, sqlTransaction))
                                            {
                                                bulkCopy.BulkCopyTimeout = 0;
                                                bulkCopy.BatchSize = dataTable.Rows.Count > 300000 ? 100000 : 50000;
                                                bulkCopy.DestinationTableName = tableName;

                                                // Explicitly map the columns from DataTable to the SQL table
                                                foreach (System.Data.DataColumn column in dataTable.Columns)
                                                {
                                                    bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                                                }

                                                await bulkCopy.WriteToServerAsync(dataTable);
                                            }

                                            // Update total records inserted
                                            totalInsertedRecords += dataTable.Rows.Count;

                                            // Clear DataTable for the next row group
                                            dataTable.Clear();
                                        }
                                    }

                                    // Commit the transaction if all inserts succeed
                                    await sqlTransaction.CommitAsync();
                                }
                                catch (Exception commitEx)
                                {
                                    totalInsertedRecords = 0;
                                    // Rollback the transaction if any insert fails
                                    await sqlTransaction.RollbackAsync();
                                    _logger.LogError($"Error: records not inserted due to error: {commitEx.Message} for {flpConfigurationId}");
                                    return (false, totalInsertedRecords);
                                }
                            }
                        }
                    }
                }

                return (true, totalInsertedRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message} for {flpConfigurationId}");
                return (false, totalInsertedRecords);
            }
        }

        public async Task<(bool Success, int TotalInsertedRecords)> InsertDataInTempTableFromParquetStream(BlobClient parquetBlobClient, string flpConfigurationId, string connectionString, string tableName)
        {
            int totalInsertedRecords = 0; // Track the number of inserted records

            try
            {
                using (Stream parquetStream = await _iBlobStorageService.ReadParquetFromBlobAsync(parquetBlobClient))
                {
                    using (var parquetReader = await ParquetReader.CreateAsync(parquetStream))
                    {
                        DataField[] dataFields = parquetReader.Schema.GetDataFields();
                        //dataFields = FlpConfigurationHelper.CreateColumnInParquetFields(dataFields);
                        DataTable dataTable = CreateDataTableV3(dataFields); // Create a DataTable for bulk copy

                        using (var sqlConnection = new SqlConnection(connectionString))
                        {
                            await sqlConnection.OpenAsync();

                            using (var sqlTransaction = sqlConnection.BeginTransaction()) // Start a transaction
                            {
                                try
                                {
                                    for (int i = 0; i < parquetReader.RowGroupCount; i++)
                                    {
                                        using (ParquetRowGroupReader groupReader = parquetReader.OpenRowGroupReader(i))
                                        {
                                            await FillDataTableV3(dataTable, groupReader, dataFields);

                                            // Bulk insert into SQL
                                            using (var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.TableLock, sqlTransaction))
                                            {
                                                bulkCopy.BulkCopyTimeout = 0;
                                                bulkCopy.BatchSize = dataTable.Rows.Count > 300000 ? 100000 : 50000;
                                                bulkCopy.DestinationTableName = tableName;

                                                // Explicitly map the columns from DataTable to the SQL table
                                                foreach (System.Data.DataColumn column in dataTable.Columns)
                                                {
                                                    bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                                                }

                                                await bulkCopy.WriteToServerAsync(dataTable);
                                            }

                                            // Update the total inserted records
                                            totalInsertedRecords += dataTable.Rows.Count;
                                            // Clear DataTable for the next row group
                                            dataTable.Clear();
                                        }


                                    }

                                    // Commit the transaction if everything succeeds
                                    await sqlTransaction.CommitAsync();
                                }
                                catch (Exception commitEx)
                                {
                                    totalInsertedRecords = 0;
                                    // Rollback the transaction in case of an error
                                    await sqlTransaction.RollbackAsync();
                                    _logger.LogError($"Error: records not inserted due to error: {commitEx.Message} for {flpConfigurationId}");
                                    return (false, totalInsertedRecords);
                                }
                            }
                        }
                    }
                }

                return (true, totalInsertedRecords); // Return success and the total inserted records
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return (false, totalInsertedRecords); // Return failure and the total inserted records (will be 0 if failed)
            }
        }


        public async Task<(bool Success, int TotalInsertedRecords)> InsertDataFromParquetStreamV2(BlobClient parquetBlobClient,string flpConfigurationId, string connectionString, string tableName,string uploadedFileId,string fileName)
        {
            int totalInsertedRecords = 0; // Track the number of inserted records

            try
            {
                using (Stream parquetStream = await _iBlobStorageService.ReadParquetFromBlobAsync(parquetBlobClient))
                {
                    using (var parquetReader = await ParquetReader.CreateAsync(parquetStream))
                    {
                        DataField[] dataFields = parquetReader.Schema.GetDataFields();
                        //dataFields = FlpConfigurationHelper.CreateColumnInParquetFields(dataFields);
                        DataTable dataTable = CreateDataTableV2(dataFields); // Create a DataTable for bulk copy

                        using (var sqlConnection = new SqlConnection(connectionString))
                        {
                            await sqlConnection.OpenAsync();

                            using (var sqlTransaction = sqlConnection.BeginTransaction()) // Start a transaction
                            {
                                try
                                {
                                    for (int i = 0; i < parquetReader.RowGroupCount; i++)
                                    {
                                        using (ParquetRowGroupReader groupReader = parquetReader.OpenRowGroupReader(i))
                                        {
                                            await FillDataTableV2(dataTable, groupReader, dataFields,fileName,uploadedFileId);

                                            // Bulk insert into SQL
                                            using (var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.TableLock, sqlTransaction))
                                            {
                                                bulkCopy.BulkCopyTimeout = 0;
                                                bulkCopy.BatchSize = dataTable.Rows.Count > 300000?100000:50000;
                                                bulkCopy.DestinationTableName = tableName;

                                                // Explicitly map the columns from DataTable to the SQL table
                                                foreach (System.Data.DataColumn column in dataTable.Columns)
                                                {
                                                    bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                                                }

                                                await bulkCopy.WriteToServerAsync(dataTable);
                                            }

                                            // Update the total inserted records
                                            totalInsertedRecords += dataTable.Rows.Count;
                                            // Clear DataTable for the next row group
                                            dataTable.Clear();
                                        }

                                       
                                    }

                                    // Commit the transaction if everything succeeds
                                    await sqlTransaction.CommitAsync();
                                }
                                catch (Exception commitEx)
                                {
                                    totalInsertedRecords = 0;
                                    // Rollback the transaction in case of an error
                                    await sqlTransaction.RollbackAsync();
                                    _logger.LogError($"Error: records not inserted due to error: {commitEx.Message} for {flpConfigurationId}");
                                    return (false, totalInsertedRecords);
                                }
                            }
                        }
                    }
                }

                return (true, totalInsertedRecords); // Return success and the total inserted records
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return (false, totalInsertedRecords); // Return failure and the total inserted records (will be 0 if failed)
            }
        }


        public async Task<(bool Success, int TotalInsertedRecords)> InsertDataFromParquetStreamV2(BlobClient parquetBlobClient, string flpConfigurationId, string connectionString, string tableName, bool mergeData, bool createHistory, string historyTableName,string uploadFileId,string fileName)
        {
            int totalInsertedRecords = 0; // Track the number of inserted records

            try
            {
                using (Stream parquetStream = await _iBlobStorageService.ReadParquetFromBlobAsync(parquetBlobClient))
                {
                    using (var parquetReader = await ParquetReader.CreateAsync(parquetStream))
                    {
                        DataField[] dataFields = parquetReader.Schema.GetDataFields();
                        //dataFields = FlpConfigurationHelper.CreateColumnInParquetFields(dataFields);
                        DataTable dataTable = CreateDataTableV2(dataFields); // Create a DataTable for bulk copy

                        using (var sqlConnection = new SqlConnection(connectionString))
                        {
                            await sqlConnection.OpenAsync();

                            using (var sqlTransaction = sqlConnection.BeginTransaction()) // Start a transaction
                            {
                                try
                                {

                                    if (mergeData & createHistory)
                                    {
                                        //Create History Table
                                        //
                                        var toBeCreatedHistoryTable = await CheckTableExistOrNot(sqlConnection, sqlTransaction, historyTableName);
                                        if (!toBeCreatedHistoryTable)
                                        {
                                            string historyTableGenerateQuery = GenerateTempTableQuery(historyTableName, dataFields);

                                            using (var createTableCommand = new SqlCommand(historyTableGenerateQuery, sqlConnection, sqlTransaction))
                                            {
                                                await createTableCommand.ExecuteNonQueryAsync();
                                            }
                                        }
                                    }

                                    for (int i = 0; i < parquetReader.RowGroupCount; i++)
                                    {
                                        using (ParquetRowGroupReader groupReader = parquetReader.OpenRowGroupReader(i))
                                        {
                                            await FillDataTableV2(dataTable, groupReader, dataFields,fileName,uploadFileId);

                                            // Bulk insert into SQL
                                            using (var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.TableLock, sqlTransaction))
                                            {
                                                bulkCopy.BulkCopyTimeout = 0;
                                                bulkCopy.BatchSize = dataTable.Rows.Count > 300000 ? 100000 : 50000;
                                                bulkCopy.DestinationTableName = tableName;

                                                // Explicitly map the columns from DataTable to the SQL table
                                                foreach (System.Data.DataColumn column in dataTable.Columns)
                                                {
                                                    bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                                                }

                                                await bulkCopy.WriteToServerAsync(dataTable);
                                            }

                                            // Update the total inserted records
                                            totalInsertedRecords += dataTable.Rows.Count;
                                            // Clear DataTable for the next row group
                                            dataTable.Clear();
                                        }


                                    }

                                    // Commit the transaction if everything succeeds
                                    await sqlTransaction.CommitAsync();
                                }
                                catch (Exception commitEx)
                                {
                                    totalInsertedRecords = 0;
                                    // Rollback the transaction in case of an error
                                    await sqlTransaction.RollbackAsync();
                                    _logger.LogError($"Error: records not inserted due to error: {commitEx.Message} for {flpConfigurationId}");
                                    return (false, totalInsertedRecords);
                                }
                            }
                        }
                    }
                }

                return (true, totalInsertedRecords); // Return success and the total inserted records
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return (false, totalInsertedRecords); // Return failure and the total inserted records (will be 0 if failed)
            }
        }

        public async Task<(bool Success, int TotalInsertedRecords)> InsertDataFromParquetSharedLocationAsyncV3(string parquetFilePath, string connectionString, string tableName, string flpConfigurationId, SharedLocationDestinationServerDto slDestinationServerDto, List<string> keyColumns,bool createHistory,string historyTableName, string uploadFileId, string fileName)
        {
            CheckConnectivitySMBLibraryModel destinationServerModel = new CheckConnectivitySMBLibraryModel
            {
                serverIP = slDestinationServerDto.ServerName,
                username = slDestinationServerDto.UserName,
                password = slDestinationServerDto.Password,
                sharedFolderName = slDestinationServerDto.FolderName,
                domain = slDestinationServerDto.Domain,
                sourceFilePath = parquetFilePath
            };

            int totalInsertedRecords = 0;

            try
            {
                using (var fileStream = _ismbLibraryServices.SMBRequest(destinationServerModel, flpConfigurationId, SMBRequestEnum.Stream).GetFileStream)
                {
                    using (var parquetReader = await ParquetReader.CreateAsync(fileStream))
                    {
                        DataField[] dataFields = parquetReader.Schema.GetDataFields();
                       // FlpConfigurationHelper.CreateColumnInParquetFields(dataFields);
                        DataTable dataTable = CreateDataTableV2(dataFields);

                        totalInsertedRecords = await mergeDataInTable(connectionString, parquetReader, dataTable, flpConfigurationId, tableName, dataFields, keyColumns, createHistory,historyTableName,uploadFileId,fileName);
                        if (totalInsertedRecords == 0)
                            return (false, totalInsertedRecords);
                    }
                }

                return (true, totalInsertedRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message} for {flpConfigurationId}");
                return (false, totalInsertedRecords);
            }
        }


        public async Task<(bool Success, int TotalInsertedRecords)> InsertDataFromParquetStreamV3(BlobClient parquetBlobClient, string flpConfigurationId, string connectionString, string tableName, List<string> keyColumns,bool createHistory,string historyTableName, string uploadFileId, string fileName)
        {
            int totalInsertedRecords = 0;
            try
            {
                using (Stream parquetStream = await _iBlobStorageService.ReadParquetFromBlobAsync(parquetBlobClient))
                using (var parquetReader = await ParquetReader.CreateAsync(parquetStream))
                {
                    DataField[] dataFields = parquetReader.Schema.GetDataFields();
                    DataTable dataTable = CreateDataTableV2(dataFields); // Create dynamic DataTable


                    totalInsertedRecords = await mergeDataInTable(connectionString, parquetReader, dataTable, flpConfigurationId, tableName, dataFields, keyColumns,createHistory,historyTableName,uploadFileId,fileName);
                    if(totalInsertedRecords ==0)
                        return (false, totalInsertedRecords); 

                }

                return (true, totalInsertedRecords); // Success
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return (false, totalInsertedRecords); // Failure
            }
        }


      

        private async Task<int> mergeDataInTable(string connectionString, ParquetReader parquetReader, DataTable dataTable, string flpConfigurationId, string tableName, DataField[] dataFields, List<string> keyColumns,bool createHistoryTable,string historyTableName,string fileUploadedId,string fileName)
        {
            int totalInsertedRecords = 0;
            using (var sqlConnection = new SqlConnection(connectionString))
            {
                await sqlConnection.OpenAsync();
                using (var sqlTransaction = sqlConnection.BeginTransaction()) // Start transaction
                {
                    try
                    {
                        //Create Temporary Table Dynamically
                        string tempTableName = $"#temp_{tableName}";
                        string createTempTableQuery = GenerateTempTableQuery(tempTableName, dataFields);


                        using (var createTableCommand = new SqlCommand(createTempTableQuery, sqlConnection, sqlTransaction))
                        {
                            await createTableCommand.ExecuteNonQueryAsync();
                        }
                        bool updatedNewColumn = false;
                        //Dynamically Generate MERGE Query with Multi-Column Primary Key
                        //List<string> columnNames = dataFields.Select(f => f.Name).ToList();
                        List<string> columnNames = dataTable.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName).ToList();

                        //Generate dynamic ON condition (for multiple primary keys)
                        string onCondition = string.Join(" AND ", keyColumns.Select(pk => $"target.[{pk}] = source.[{pk}]"));
                        //Generate dynamic UPDATE statement
                        string updateColumns = string.Join(", ", columnNames
                            .Where(col => !keyColumns.Contains(col)) //Ignore primary keys in update
                            .Select(col => $"target.[{col}] = source.[{col}]"));

                        //Generate dynamic INSERT statement
                        string insertColumns = string.Join(", ", columnNames.Select(col => $"[{col}]"));
                        string insertValues = string.Join(", ", columnNames.Select(col => $"source.[{col}]"));

                        if (createHistoryTable)
                        {
                            //Create History Table
                            //
                            
                            var historyTableExist = await CheckTableExistOrNot(sqlConnection,sqlTransaction,historyTableName);
                            if (historyTableExist)
                            {

                                // If the table exists, get the current columns and add new ones if necessary
                                var existingColumns = await GetAllColumnNamesAsync(sqlConnection, sqlTransaction, connectionString, historyTableName);
                                var existingColumnSet = new HashSet<string>(existingColumns, StringComparer.OrdinalIgnoreCase); // Case-insensitive comparison
                                                                                                                                // Find new columns that are not in the existing table
                                var newColumns = dataFields
                                    .Where(df => !existingColumnSet.Contains(df.Name))
                                    .Select(df => $"ALTER TABLE {historyTableName} ADD [{df.Name}] {FlpConfigurationHelper.GetSqlDataType(df.ClrType)}")
                                    .ToList();
                                if (newColumns.Any())
                                {
                                    updatedNewColumn = true;
                                    foreach (var commandText in newColumns)
                                    {
                                        var command = new SqlCommand(commandText, sqlConnection, sqlTransaction);
                                        await command.ExecuteNonQueryAsync();
                                    }
                                }
                                
                            }
                            else
                            {
                                string historyTableGenerateQuery = GenerateTempTableQuery(historyTableName, dataFields);

                                using (var createTableCommand = new SqlCommand(historyTableGenerateQuery, sqlConnection, sqlTransaction))
                                {
                                    await createTableCommand.ExecuteNonQueryAsync();
                                }

                            }

                            //string checkTriggerExistsSql = $@"
                            //                                IF NOT EXISTS (SELECT 1 FROM sys.triggers WHERE name = 'trg_{historyTableName}')
                            //                                BEGIN
                            //                                    DECLARE @sql NVARCHAR(MAX);
                            //                                    SET @sql = 'CREATE TRIGGER trg_{historyTableName}
                            //                                        ON {tableName}
                            //                                        AFTER UPDATE
                            //                                        AS
                            //                                        BEGIN
                            //                                            INSERT INTO {historyTableName} ({insertColumns})
                            //                                            SELECT {insertColumns} FROM deleted;
                            //                                        END';
                            //                                    EXEC sp_executesql @sql;
                            //                                END";


                            string createOrAlterTriggerSql = $@"
                                                        CREATE OR ALTER TRIGGER trg_{historyTableName}
                                                        ON {tableName}
                                                        AFTER UPDATE
                                                        AS
                                                        BEGIN
                                                            INSERT INTO {historyTableName} ({insertColumns})
                                                            SELECT {insertColumns} FROM deleted;
                                                        END;";

                            using (var createTriggerCommand = new SqlCommand(createOrAlterTriggerSql, sqlConnection, sqlTransaction))
                            {
                                await createTriggerCommand.ExecuteNonQueryAsync();
                            }

                        }
                        else
                        {
                            //Delete trigger if not updating hstory table
                            if (!string.IsNullOrWhiteSpace(historyTableName))
                            {
                              //  await DeleteTriggerAsync(sqlConnection,$"DROP TRIGGER IF EXISTS  trg_{historyTableName}");

                                using (SqlCommand command = new SqlCommand($"DROP TRIGGER IF EXISTS  trg_{historyTableName}", sqlConnection, sqlTransaction))
                                {
                                    await command.ExecuteNonQueryAsync();
                                }

                            }
                            
                        }
                       

                        for (int i = 0; i < parquetReader.RowGroupCount; i++)
                        {

                            using (ParquetRowGroupReader groupReader = parquetReader.OpenRowGroupReader(i))
                            {
                                await FillDataTableV2(dataTable, groupReader, dataFields,fileName,fileUploadedId);

                                //Bulk Insert into Temp Table
                                using (var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.TableLock, sqlTransaction))
                                {
                                    bulkCopy.BulkCopyTimeout = 0;
                                    bulkCopy.BatchSize = dataTable.Rows.Count > 300000 ? 100000 : 50000;
                                    bulkCopy.DestinationTableName = tempTableName; // Temporary table

                                    foreach (System.Data.DataColumn column in dataTable.Columns)
                                    {
                                        bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                                    }

                                    await bulkCopy.WriteToServerAsync(dataTable);
                                }

                                string mergeQuery = string.Empty;

                                if (updatedNewColumn)
                                {
                                    mergeQuery = $@"MERGE INTO {tableName} AS target
                                                    USING {tempTableName} AS source
                                                    ON {onCondition}  
                                                    WHEN MATCHED THEN 
                                                        UPDATE SET {updateColumns}
                                                    WHEN NOT MATCHED THEN 
                                                        INSERT ({insertColumns}) 
                                                        VALUES ({insertValues});";

                                }
                                else
                                {
                                    mergeQuery = $@"MERGE INTO {tableName} AS target
                                                    USING {tempTableName} AS source
                                                    ON {onCondition}  
                                                    WHEN MATCHED AND source.hashByte != target.hashByte THEN 
                                                        UPDATE SET {updateColumns}
                                                    WHEN NOT MATCHED THEN 
                                                        INSERT ({insertColumns}) 
                                                        VALUES ({insertValues});";

                                }





                                //Execute Dynamic MERGE Query
                                using (var mergeCommand = new SqlCommand(mergeQuery, sqlConnection, sqlTransaction))
                                {
                                    await mergeCommand.ExecuteNonQueryAsync();
                                }

                               

                                totalInsertedRecords += dataTable.Rows.Count;
                                dataTable.Clear();
                            }
                        }

                        //Drop the Temporary Table
                        using (var dropTableCommand = new SqlCommand($"DROP TABLE {tempTableName};", sqlConnection, sqlTransaction))
                        {
                            await dropTableCommand.ExecuteNonQueryAsync();
                        }

                        await sqlTransaction.CommitAsync();
                        
                    }
                    catch (Exception commitEx)
                    {
                        totalInsertedRecords = 0;
                        await sqlTransaction.RollbackAsync();
                        _logger.LogError($"Error: records not inserted due to error: {commitEx.Message} for {flpConfigurationId}");
                        
                    }
                }
                return totalInsertedRecords;
            }

           
        }





        public async Task<List<string>> GetAllColumnNamesAsync(SqlConnection  sqlConnection,SqlTransaction sqlTransaction, string connectionString, string tableName)
        {
            // SQL query to get all column names for a given table
            var query = @"SELECT COLUMN_NAME 
                  FROM INFORMATION_SCHEMA.COLUMNS 
                  WHERE TABLE_NAME = @TableName 
                  ORDER BY ORDINAL_POSITION";

            var columnNames = new List<string>();

            using (SqlCommand command = new SqlCommand(query, sqlConnection, sqlTransaction))
            {
                // Add parameters to prevent SQL injection
                command.Parameters.AddWithValue("@TableName", tableName);               
             
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

            return columnNames;
        }



        private async Task CreateSqlTableIfNotExistsAsync(string connectionString, string tableName, DataField[] dataFields)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var tableExists = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}'") > 0;

                if (!tableExists)
                {
                    var createdQuery = GetCreateTableSql(tableName, dataFields);
                    await connection.ExecuteAsync(createdQuery);
                }

            }
        }


        //private async Task<bool> CheckTableExistOrNot(SqlConnection con, string connectionString, string tableName)
        //{
        //    using (var connection = new SqlConnection(connectionString))
        //    {
        //        await connection.OpenAsync();
        //        var tableExists = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}'") > 0;
        //        return tableExists;               
        //    }
        //}


        //private async Task<bool> CheckTableExistOrNot(SqlConnection con, string tableName, SqlTransaction sqlTransaction)
        //{
        //    const string query = "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
        //    return await con.ExecuteScalarAsync<int>(query, sqlTransaction, new { TableName = tableName }) == 1;
        //}


        private async Task<bool> CheckTableExistOrNot(SqlConnection con, SqlTransaction transaction, string tableName)
        {
            const string query = "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";

            using (var command = new SqlCommand(query, con, transaction)) //Assign transaction
            {
                command.Parameters.AddWithValue("@TableName", tableName);
                var result = await command.ExecuteScalarAsync();
                return result != null;
            }
        }



        private string GetCreateTableSql(string tableName, DataField[] dataFields)
        {
            var columnsSql = string.Join(", ", dataFields.Select(df => $"[{df.Name}] {FlpConfigurationHelper.GetSqlDataType(df.ClrType)}"));
            return $"CREATE TABLE [{tableName}] ({columnsSql})";
        }


        private string GenerateTempTableQuery(string tableName, DataField[] dataFields)
        {
            var columnsSql = string.Join(", ", dataFields.Select(df => $"[{df.Name}] {FlpConfigurationHelper.GetSqlDataType(df.ClrType)}"));

            // Manually add insertionDateTime and hashByte columns
            columnsSql += ", [insertionDateTime] DATETIME, [hashByte] VARBINARY(MAX), [fileUploadId] NVARCHAR(MAX), [fileName] NVARCHAR(MAX) ";

            return $"CREATE TABLE [{tableName}] ({columnsSql})";
        }

      

        private  string GetInsertSql(DataField[] dataFields, string tableName)
        {
            var columns = string.Join(", ", dataFields.Select(df => $"[{df.Name}]"));
            var values = string.Join(", ", dataFields.Select(df => $"@{df.Name.Replace(" ", "_")}"));
            return $"INSERT INTO [{tableName}] ({columns}) VALUES ({values})";
        }
       

        // Creates a DataTable with columns matching the Parquet data fields
        private  DataTable CreateDataTable(DataField[] dataFields)
        {
            var dataTable = new DataTable();
            foreach (var dataField in dataFields)
            {
                dataTable.Columns.Add(dataField.Name, dataField.ClrType);
            }

            // Add InsertionDateTime column
            dataTable.Columns.Add("insertionDateTime", typeof(DateTime));
            dataTable.Columns.Add("hashByte",  typeof(byte[]));
            return dataTable;
        }


        private DataTable CreateDataTableV2(DataField[] dataFields)
        {
            var dataTable = new DataTable();
            foreach (var dataField in dataFields)
            {
                dataTable.Columns.Add(dataField.Name, dataField.ClrType);
            }

            // Add InsertionDateTime column
            dataTable.Columns.Add("fileUploadId", typeof(string));
            dataTable.Columns.Add("fileName", typeof(string));
            dataTable.Columns.Add("insertionDateTime", typeof(DateTime));
            dataTable.Columns.Add("hashByte", typeof(byte[]));
            return dataTable;
        }

        private DataTable CreateDataTableV3(DataField[] dataFields)
        {
            var dataTable = new DataTable();
            foreach (var dataField in dataFields)
            {
                dataTable.Columns.Add(dataField.Name, dataField.ClrType);
            }
            return dataTable;
        }

        // Fills the DataTable with data from the Parquet row group
        private  async Task FillDataTable(DataTable dataTable, ParquetRowGroupReader groupReader, DataField[] dataFields)
        {
            foreach (var dataField in dataFields)
            {
                DataColumn column = await groupReader.ReadColumnAsync(dataField);
                Array values = column.Data;

                for (int i = 0; i < values.Length; i++)
                {
                    if (dataTable.Rows.Count <= i)
                    {                       
                        dataTable.Rows.Add(dataTable.NewRow());
                        dataTable.Rows[i]["insertionDateTime"] = DateTime.UtcNow;
                    }
                 
                    dataTable.Rows[i][dataField.Name] = values?.GetValue(i) ?? DBNull.Value;


                }
            }

            foreach (DataRow row in dataTable.Rows)
            {
                var value = CalculateRowHash(row, dataTable);
                row["hashByte"] = value;
            }
        }


        private async Task FillDataTableV2(DataTable dataTable, ParquetRowGroupReader groupReader, DataField[] dataFields,string fileName,string fileUploadId)
        {
            foreach (var dataField in dataFields)
            {
                DataColumn column = await groupReader.ReadColumnAsync(dataField);
                Array values = column.Data;

                for (int i = 0; i < values.Length; i++)
                {
                    if (dataTable.Rows.Count <= i)
                    {
                        dataTable.Rows.Add(dataTable.NewRow());
                        dataTable.Rows[i]["insertionDateTime"] = DateTime.UtcNow;
                    }

                    dataTable.Rows[i][dataField.Name] = values?.GetValue(i) ?? DBNull.Value;


                }
            }

            foreach (DataRow row in dataTable.Rows)
            {
                var value = CalculateRowHashV2(row, dataTable);
                row["hashByte"] = value;
                row["fileUploadId"] = fileUploadId;
                row["fileName"] = fileName;
            }
        }

        private async Task FillDataTableV3(DataTable dataTable, ParquetRowGroupReader groupReader, DataField[] dataFields)
        {
            foreach (var dataField in dataFields)
            {
                DataColumn column = await groupReader.ReadColumnAsync(dataField);
                Array values = column.Data;

                for (int i = 0; i < values.Length; i++)
                {
                    if (dataTable.Rows.Count <= i)
                    {
                        dataTable.Rows.Add(dataTable.NewRow());
                        //dataTable.Rows[i]["insertionDateTime"] = DateTime.UtcNow;
                    }
                    dataTable.Rows[i][dataField.Name] = values?.GetValue(i) ?? DBNull.Value;
                }
            }            
        }

        private byte[] CalculateRowHash(DataRow row, DataTable dataTable)
        {
            StringBuilder combinedValues = new StringBuilder();

            // Combine all column values except the HashByte column itself
            foreach (System.Data.DataColumn column in dataTable.Columns)
            {
                if (column.ColumnName != "hashByte" && column.ColumnName != "insertionDateTime")
                {
                    combinedValues.Append(row[column]?.ToString() ?? string.Empty);
                }
            }
            // Hash the concatenated string           
            var hashByte = Crypto.ComputeHash(combinedValues.ToString());
            return hashByte;
        }

        private byte[] CalculateRowHashV2(DataRow row, DataTable dataTable)
        {
            StringBuilder combinedValues = new StringBuilder();

            // Combine all column values except the HashByte column itself
            foreach (System.Data.DataColumn column in dataTable.Columns)
            {
                if (column.ColumnName != "hashByte" && column.ColumnName != "insertionDateTime" && column.ColumnName != "fileName" && column.ColumnName != "fileUploadId")
                {
                    combinedValues.Append(row[column]?.ToString() ?? string.Empty);
                }
            }
            // Hash the concatenated string           
            var hashByte = Crypto.ComputeHash(combinedValues.ToString());
            return hashByte;
        }

    }
}
