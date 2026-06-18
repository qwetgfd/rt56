using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Common;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v1._0
{
    public class SMBLibraryRepository : ISMBLibraryRepository
    {
        private readonly ILogger<SMBLibraryRepository> _logger;
        private readonly IDapperService _dapperService;
        public SMBLibraryRepository(ILogger<SMBLibraryRepository> logger, IDapperService dapperService)
        {
            _logger = logger;
            _dapperService = dapperService;
        }
        public async Task<DatabaseResponse> AddSmbRequestLogMessage(string flpConfigurationId, string message, string info)
        {
            try
            {
                var dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@flpConfigurationId", flpConfigurationId);
                dynamicParameters.Add("@message", message);
                dynamicParameters.Add("@info", info);
                var result = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[add_smbRequestLogMessage]", dynamicParameters, commandType: CommandType.StoredProcedure);
                return result;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }



        }

    }
}
