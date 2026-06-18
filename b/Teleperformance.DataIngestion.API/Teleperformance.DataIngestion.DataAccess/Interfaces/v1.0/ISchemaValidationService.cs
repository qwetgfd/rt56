using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingProcess.TableSchemaValidation;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0
{
    public interface ISchemaValidationService
    {
       // Task<MappingTableSchemaResult?> CheckTableSchemaAndCreatTable(string parquetFilePath, string connectionString, string processName, string tableName);


        Task<MappingTableSchemaResult?> CreateBronzeTableFromSharedLocation(string parquetFilePath, string connectionString, string processName, string tableName, string flpConfigurationId, string tabName, SharedLocationDestinationServerDto slDestinationServerDto, ParquetFileResponseDto resultResponse);

        Task<MappingTableSchemaResult?> CreateBronzeTableFromBlob(BlobClient blParquetBlobClient, string connectionString, string processName, string tableName, string flpConfigurationId, string tabName, ParquetFileResponseDto resultResponse);
    }
}
