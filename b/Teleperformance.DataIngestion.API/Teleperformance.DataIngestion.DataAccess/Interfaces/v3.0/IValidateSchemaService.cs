using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.DTOs.v3._0.ProcessConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingProcess.TableSchemaValidation;

namespace Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0
{
    public interface IValidateSchemaService
    {
       
        Task<Dictionary<string, FlpFileColumnMappingDto>> ValidateFileSchema(
    string flpConfigurationId, bool validateFileSchema, bool isHeaderProvided, List<string> fileHeaders, string processName, string tableName, IEnumerable<FlpFileColumnMappingDto> columnList=null);

        Task<MappingTableSchemaResult?> CreateBronzeTableFromSharedLocation(string parquetFilePath, string connectionString, string processName, string tableName, string flpConfigurationId, string tabName, SharedLocationDestinationServerDto slDestinationServerDto, ParquetFileResponseDto resultResponse);
        Task<MappingTableSchemaResult?> CreateBronzeTableFromBlob(BlobClient blParquetBlobClient, string connectionString, string processName, string tableName, string flpConfigurationId, string tabName, ParquetFileResponseDto resultResponse);
        Task<MappingTableSchemaResult?> CreateTempTableFromBlob(BlobClient blParquetBlobClient, string connectionString, string processName, string tableName, string flpConfigurationId, string tabName, ParquetFileResponseDto resultResponse);
        Task<IEnumerable<FlpFileColumnMappingDto>> GetFileColumnList(string flpConfigurationId);
        Task<IEnumerable<FlpFileColumnMappingDto>> GetFileColumnListV2(string flpConfigurationId);
        Task<IEnumerable<FlpFileColumnMappingDto>> GetFileColumnListV2ByTabName(string flpConfigurationId, string tabName);
    }
}
