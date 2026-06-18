using Dapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v3._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v3._0
{
    public class ChangeProcessStatusRepository : IChangeProcessStatusRepository
    {
        private readonly ILogger<ChangeProcessStatusRepository> _logger;
        private readonly IDapperService _dapperService;

        public ChangeProcessStatusRepository(ILogger<ChangeProcessStatusRepository> logger, IDapperService dapperService)
        {
            _logger = logger;
            _dapperService = dapperService;
        }
        public async Task<DatabaseResponse> UpdateProcessStatus()
        {
            try
            {
                var dbResponse = await _dapperService.GetSingleRowAsync<DatabaseResponse>("[commit_changeProcessStatus]", null, commandType: CommandType.StoredProcedure);
                return dbResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,"ProcessStatus "+ ex.Message);
                return null;
            }

        }
    }
}
