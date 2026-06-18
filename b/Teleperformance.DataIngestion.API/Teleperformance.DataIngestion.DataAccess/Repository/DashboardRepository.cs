using Dapper;
using Microsoft.Extensions.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Repository.v1._0;
using Teleperformance.DataIngestion.DataAccess.Services.v1._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.Dashboard;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Entities.v1._0.Dashboard;
using Teleperformance.DataIngestion.Models.Entities.v1._0.UploadFileStatus;

namespace Teleperformance.DataIngestion.DataAccess.Repository
{
    public class DashboardRepository:IDashboardRepository
    {
        private readonly ILogger<StatusRepository> _logger;
        private readonly IDapperService _dapperService;

        public DashboardRepository(ILogger<StatusRepository> logger, IDapperService dapperService)
        {
            _logger = logger;
            _dapperService = dapperService;
        }
        public async Task<GetProcessListResponse?> GetProcessList(GetProcessListRequest getProcessListRequest)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@RegionId", getProcessListRequest.RegionId);
            parameters.Add("@SubRegionId", getProcessListRequest.SubRegionId);
            parameters.Add("@ClientId", getProcessListRequest.ClientId);
            parameters.Add("@FromDate", getProcessListRequest.FromDate);
            parameters.Add("@ToDate", getProcessListRequest.ToDate);
            parameters.Add("@noOfRecords", getProcessListRequest.noOfRecords);
            parameters.Add("@securityGroupId", getProcessListRequest.securityGroupId);
            var result = await _dapperService.GetSingleRowAsync<GetProcessListResponse>("sel_CountProcessList", parameters, commandType: CommandType.StoredProcedure);
            return result;
        }

        public async Task<GetFileListResponse?> GetFileList(GetFileListRequest getFileListRequest)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@RegionId", getFileListRequest.RegionId);
            parameters.Add("@SubRegionId", getFileListRequest.SubRegionId);
            parameters.Add("@ClientId", getFileListRequest.ClientId);
            parameters.Add("@FromDate", getFileListRequest.FromDate);
            parameters.Add("@ToDate", getFileListRequest.ToDate);
            parameters.Add("@noOfRecords", getFileListRequest.noOfRecords);
            parameters.Add("@securityGroupId", getFileListRequest.securityGroupId);
            var result = await _dapperService.GetSingleRowAsync<GetFileListResponse>("sel_CountFileListDetails", parameters, commandType: CommandType.StoredProcedure);
            return result;
        }

        public async Task<GetClientListResponse?> GetClientList(GetFileListRequest GetClientListRequest)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@RegionId", GetClientListRequest.RegionId);
            parameters.Add("@SubRegionId", GetClientListRequest.SubRegionId);
            parameters.Add("@ClientId", GetClientListRequest.ClientId);
            parameters.Add("@FromDate", GetClientListRequest.FromDate);
            parameters.Add("@ToDate", GetClientListRequest.ToDate);
            parameters.Add("@noOfRecords", GetClientListRequest.noOfRecords);
            parameters.Add("@securityGroupId", GetClientListRequest.securityGroupId);
            var result = await _dapperService.GetSingleRowAsync<GetClientListResponse>("sel_DIClientsList", parameters, commandType: CommandType.StoredProcedure);
            return result;
        }

        public async Task<IEnumerable<DashboardRealTimeProcessingResponse?>> DashboardRealTimeProcessingStatusList(GetFileListRequest getDashboardStatusList)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@RegionId", getDashboardStatusList.RegionId);
            parameters.Add("@SubRegionId", getDashboardStatusList.SubRegionId);
            parameters.Add("@ClientId", getDashboardStatusList.ClientId);
            parameters.Add("@FromDate", getDashboardStatusList.FromDate);
            parameters.Add("@ToDate", getDashboardStatusList.ToDate);
            parameters.Add("@noOfRecords", getDashboardStatusList.noOfRecords);
            parameters.Add("@securityGroupId", getDashboardStatusList.securityGroupId);
            var result = await _dapperService.GetMultipleRowsAsync<DashboardRealTimeProcessingResponse?>("sel_realTimeProcessingStatusList", parameters, commandType: CommandType.StoredProcedure);
            return result;
        }

        public async Task<IEnumerable<CountFileUploadsByProcessTypeResponse?>> CountFileUploadsByProcessType(GetFileListRequest countFileUploadsByProcessTypeRequest)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@RegionId", countFileUploadsByProcessTypeRequest.RegionId);
            parameters.Add("@SubRegionId", countFileUploadsByProcessTypeRequest.SubRegionId);
            parameters.Add("@ClientId", countFileUploadsByProcessTypeRequest.ClientId);
            parameters.Add("@FromDate", countFileUploadsByProcessTypeRequest.FromDate);
            parameters.Add("@ToDate", countFileUploadsByProcessTypeRequest.ToDate);
            parameters.Add("@noOfRecords", countFileUploadsByProcessTypeRequest.noOfRecords);
            parameters.Add("@securityGroupId", countFileUploadsByProcessTypeRequest.securityGroupId);
            var result = await _dapperService.GetMultipleRowsAsync<CountFileUploadsByProcessTypeResponse?>("sel_CountFileUploadsByProcessType", parameters, commandType: CommandType.StoredProcedure);
            return result;
        }

        public async Task<IEnumerable<DIFrameworkUtilizationResponse?>> DIFrameworkUtilization(string securityGroupId)
        {
            var parameters = new DynamicParameters();   //@securityGroupId
              parameters.Add("@securityGroupId", securityGroupId);

            var result = await _dapperService.GetMultipleRowsAsync<DIFrameworkUtilizationResponse?>("sel_DIFrameworkUtilization", parameters, commandType: CommandType.StoredProcedure);
            return result;
        }

        public async Task<IEnumerable<UtilizationByRegion?>> GetUtilizationByRegionList(string securityGroupId)
        {
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@securityGroupId", securityGroupId);
                var result = await _dapperService.GetMultipleRowsAsync<UtilizationByRegion?>("sel_DIFrameworkUtilizationByRegion", parameters, commandType: CommandType.StoredProcedure);
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
