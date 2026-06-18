using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingConfigurationProcess;
using static Dapper.SqlMapper;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v1._0
{
    public class FileLoadingProcessRepository : IFileLoadingProcessRepository
    {
        private readonly ILogger<FileLoadingProcessRepository> _logger;
        private readonly IDapperService _dapperService;

        public FileLoadingProcessRepository(ILogger<FileLoadingProcessRepository> logger, IDapperService dapperService)
        {
            _logger = logger;
            _dapperService = dapperService;
        }  
        
        public async Task<bool> InsertFileProcessStatus(FileProcessLogHistoryDto logHistory)
        {
            
            var dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@processId", logHistory.processId);
            dynamicParameters.Add("@tableName", logHistory.tableName);
            dynamicParameters.Add("@messageType", logHistory.messageType);
            dynamicParameters.Add("@message", logHistory.message);
            dynamicParameters.Add("@totalRows", logHistory.totalRows);
            dynamicParameters.Add("@dateTimeReceived", logHistory.dateTimeReceived);
            dynamicParameters.Add("@processName", logHistory.processName);
            dynamicParameters.Add("@loginid", logHistory.loginid);
            dynamicParameters.Add("@fileType", logHistory.fileType);
            dynamicParameters.Add("@flpConfigurationId", logHistory.flpConfigurationId);
            dynamicParameters.Add("@flpFileLogStatusId", logHistory.flpFileLogStatusId);
            dynamicParameters.Add("@activityProcessStatusId", logHistory.activityProcessStatusId);
            dynamicParameters.Add("@fileUploadedId", logHistory.fileUploadedId);
            dynamicParameters.Add("@processTypeId", logHistory.processTypeId);
            dynamicParameters.Add("@databricksAPIResponse", logHistory.databricksAPIResponse);


            var dbResponse = await _dapperService.GetSingleRowAsync<bool>("[commit_flpProcessLogHistory]", dynamicParameters, commandType: CommandType.StoredProcedure);
            return dbResponse;
        }


        public async Task<(bool, bool)> dropMainTableAndHistoryTable(bool paramDropManinTable, bool paramDropHistoryTable, string mainTable, string historyTable,string connectionString)
        {
            bool retMainTable = false;
            bool retHistoryTable = false;
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    if (paramDropManinTable)
                    {
                        var dropMainTable = $"IF OBJECT_ID(N'{mainTable}', N'U') IS NOT NULL DROP TABLE {mainTable};";
                        await connection.ExecuteAsync(dropMainTable);
                        retMainTable = true;

                    }
                    //Currently not in using
                    if (paramDropHistoryTable)
                    {
                        var dropHistoryTbl = $"IF OBJECT_ID(N'{historyTable}', N'U') IS NOT NULL DROP TABLE {historyTable};";
                        await connection.ExecuteAsync(dropHistoryTbl);
                        retHistoryTable = true;

                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message.ToString());
                

            }
            return (retMainTable, retHistoryTable);
        }

        public async Task<DatabaseResponse> AddDatabricksJsonFileColumn(string fileURL, string columns, string flpConfigurationId, string uploadFileId, string tabName)
        {

            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@fileURL", fileURL);
                dynamicParameters.Add("@columns", columns);
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@uploadFileId", uploadFileId);
                dynamicParameters.Add("@tabName", tabName);
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_databricksJsonFileColumn]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                throw;
            }
        }


    }
}
