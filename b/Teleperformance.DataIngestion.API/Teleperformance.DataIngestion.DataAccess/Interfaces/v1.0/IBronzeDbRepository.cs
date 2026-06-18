using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingProcess.TableSchemaValidation;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface IBronzeDbRepository
    {
        Task<bool> ConvertParquetToSqlAsync(string parquetFilePath, string connectionString, string tableName);
        Task<(bool Success, int TotalInsertedRecords)> InsertDataInTempTableFromParquetStream(BlobClient parquetBlobClient, string flpConfigurationId, string connectionString, string tableName);
        Task<(bool Success, int TotalInsertedRecords)> InsertDataFromParquetStreamV2(BlobClient parquetBlobClient, string flpConfigurationId, string connectionString, string tableName, string uploadedFileId, string fileName);
        Task<(bool Success, int TotalInsertedRecords)> InsertDataFromParquetStreamV2(BlobClient parquetBlobClient, string flpConfigurationId, string connectionString, string tableName, bool mergeData, bool createHistory, string historyTableName, string uploadFileId, string fileName);
        Task<(bool Success, int TotalInsertedRecords)> InsertDataFromParquetStreamV3(BlobClient parquetBlobClient, string flpConfigurationId, string connectionString, string tableName, List<string> keyColumns, bool createHistory, string historyTableName, string uploadFileId, string fileName);
        Task<(bool Success, int TotalInsertedRecords)> InsertDataFromParquetSharedLocationAsyncV3(string parquetFilePath, string connectionString, string tableName, string flpConfigurationId, SharedLocationDestinationServerDto slDestinationServerDto, List<string> keyColumns, bool createHistory, string historyTableName, string uploadFileId, string fileName);
        Task<bool> InsertDataFromParquetStreamAsync(string parquetFilePath, string connectionString, string tableName);
        Task<bool> InsertDataFromParquetStream(BlobClient parquetBlobClient, string connectionString, string tableName);
        Task<(bool Success, int TotalInsertedRecords)> InsertDataFromParquetSharedLocationAsync(string parquetFilePath, string connectionString, string tableName, string flpConfigurationId, SharedLocationDestinationServerDto slDestinationServerDto, string uploadFileId, string fileName);
        Task<(bool Success, int TotalInsertedRecords)> InsertDataFromParquetSharedLocationAsync(string parquetFilePath, string connectionString, string tableName, string flpConfigurationId, SharedLocationDestinationServerDto slDestinationServerDto, bool mergeData, bool createHistory, string historyTableName, string uploadFileId, string fileName);
        Task<DatabaseResponse> CommitMappingColumnList(string processName, string tableName, string columnName, string dataType, string actionType, string flpConfigurationId, string tabName);
        Task<IEnumerable<ParquetColumnMapping>> GetMappingColumnList(string flpConfigurationId, string tableName);
    }
}
