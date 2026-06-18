using Dapper;
using Microsoft.Extensions.Logging;
using System.Data;
using Teleperformance.DataIngestion.DataAccess.Helpers;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v1._0;
using Teleperformance.DataIngestion.DataAccess.Interfaces.v2._0;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.FileLoadingConfigurationProcess;
using Teleperformance.DataIngestion.Models.Entities.v1._0.UploadFileStatus;

namespace Teleperformance.DataIngestion.DataAccess.Repository.v1._0
{
    public class StatusRepository : IStatusRepository
    {
        private readonly ILogger<StatusRepository> _logger;
        private readonly IDapperService _dapperService;
        private readonly IHeaderService _headerService;

        public StatusRepository(ILogger<StatusRepository> logger, IDapperService dapperService, IHeaderService headerService)
        {
            _logger = logger;
            _dapperService = dapperService;
            _headerService = headerService;
        }

        public async Task<IEnumerable<ConfigFileStatusResponse>> FlpFileStatus()
        {
            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
            string campaignId = _headerService.GetHeaderValue("campaignId");
            string upn = _headerService.GetHeaderValue("upn");
            
            var dynamicParameters = new DynamicParameters();
            if (!string.IsNullOrWhiteSpace(securityGroupId))
            dynamicParameters.Add("@securityGroupId", securityGroupId);
            if (!string.IsNullOrWhiteSpace(campaignId))
            dynamicParameters.Add("@campaignId", campaignId);
            if (!string.IsNullOrWhiteSpace(upn))
            dynamicParameters.Add("@upn", upn);
            var result = await _dapperService.GetMultipleRowsAsync<ConfigFileStatusResponse>("sel_FileUploadStatus", dynamicParameters, commandType: CommandType.StoredProcedure);
            return result;
        }

        //public async Task<ProcessedFileResponse> GetFlpProcessedFilesListAsync(ProcessedFileListRequestDto request)
        //{
        //    string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
        //    string campaignId = _headerService.GetHeaderValue("campaignId");
        //    string upn = _headerService.GetHeaderValue("upn");

        //    var dynamicParameters = new DynamicParameters();
        //    dynamicParameters.Add("@pageNumber", request.PageNumber);
        //    dynamicParameters.Add("@pageSize", request.PageSize);
        //    //dynamicParameters.Add("@processName",request.ProcessName);
        //    dynamicParameters.Add("@fromDate", request.FromDate);
        //    dynamicParameters.Add("@toDate", request.ToDate);
        //    dynamicParameters.Add("@createdBy", request.CreatedBy);
        //    dynamicParameters.Add("@searchColumnName", request.SearchOnColumn);
        //    dynamicParameters.Add("@searchColumnValue", request.SearchValue);
        //    if (!string.IsNullOrWhiteSpace(securityGroupId))
        //        dynamicParameters.Add("@securityGroupId", securityGroupId);
        //    if (!string.IsNullOrWhiteSpace(campaignId))
        //        dynamicParameters.Add("@campaignId", campaignId);
        //    if (!string.IsNullOrWhiteSpace(upn))
        //        dynamicParameters.Add("@upn", upn);
        //    dynamicParameters.Add("@totalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

        //    var result = await _dapperService.GetMultipleRowsAsync<ProcessedFileListResponse>("sel_flpProcessList", dynamicParameters);
        //    int totalCount = dynamicParameters.Get<int>("@totalCount");

        //    return new ProcessedFileResponse
        //    {
        //        Response = result.ToList(),
        //        TotalCount = totalCount
        //    };
        //}

        public async Task<ProcessedFileResponse> GetFlpProcessedFilesListAsync(ProcessedFileListRequestDto request)
        {
            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
            string campaignId = _headerService.GetHeaderValue("campaignId");
            string upn = _headerService.GetHeaderValue("upn");

            var dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@pageNumber", request.PageNumber);
            dynamicParameters.Add("@pageSize", request.PageSize);
            //dynamicParameters.Add("@processName",request.ProcessName);
            dynamicParameters.Add("@fromDate", request.FromDate);
            dynamicParameters.Add("@toDate", request.ToDate);
            dynamicParameters.Add("@createdBy", request.CreatedBy);
            dynamicParameters.Add("@searchColumnName", request.SearchOnColumn);
            dynamicParameters.Add("@searchColumnValue", request.SearchValue);
            if (!string.IsNullOrWhiteSpace(securityGroupId))
                dynamicParameters.Add("@securityGroupId", securityGroupId);
            if (!string.IsNullOrWhiteSpace(campaignId))
                dynamicParameters.Add("@campaignId", campaignId);
            if (!string.IsNullOrWhiteSpace(upn))
                dynamicParameters.Add("@upn", upn);

            //dynamicParameters.Add("@difRequest", "Yes");
            dynamicParameters.Add("@internalDIFRequest",true,DbType.Boolean);
            dynamicParameters.Add("@totalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

            using var connection = _dapperService.CreateConnection();
            using var multi = await connection.QueryMultipleAsync("sel_flpProcessList", dynamicParameters, commandType: CommandType.StoredProcedure, commandTimeout: 300);

            // Read the main data result set
            var result = multi.Read<ProcessedFileListResponse>().ToList();

            // Read the pagination information result set
            var paginationInfo = multi.Read<PaginationInfo>().FirstOrDefault();

            // Fallback to output parameter if pagination info is not available
            int totalCount = paginationInfo?.TotalCount ?? dynamicParameters.Get<int>("@totalCount");
            int pageNumber = paginationInfo?.PageNumber ?? request.PageNumber;
            int pageSize = paginationInfo?.PageSize ?? request.PageSize;
            int totalPages = paginationInfo?.TotalPages ?? (totalCount > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0);

            return new ProcessedFileResponse
            {
                Response = result,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages
            };



        }

        public async Task<ProcessedFileResponse> GetFlpProcessedFilesListAsyncV2(ProcessedFileListRequestDto request)
        {
            string securityGroupId = _headerService.GetHeaderValue("x-tpdi-api-sg");
            string campaignId = _headerService.GetHeaderValue("campaignId");
            string upn = _headerService.GetHeaderValue("upn");

            

            string searchOnColumn = request?.SearchOnColumn??"";
            string searchValue = request?.SearchValue??"";
            if (!string.IsNullOrWhiteSpace(searchOnColumn) && !string.IsNullOrWhiteSpace(searchValue))
            {
                var dict = FlpConfigurationHelperV4_1.GetSearchColumnMapping();
                var columnNames = searchOnColumn.Split(',')
                                                .Select(cn => cn.Trim())
                                                .Where(cn => !string.IsNullOrWhiteSpace(cn));

                var mappedColumns = columnNames.Select(cn => dict.TryGetValue(cn, out var mappedName) ? mappedName : cn);
                searchOnColumn = string.Join(",", mappedColumns);
            }
            else
            {
                searchOnColumn = "";
            }


                var dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@pageNumber", request.PageNumber);
            dynamicParameters.Add("@pageSize", request.PageSize);
            //dynamicParameters.Add("@processName",request.ProcessName);
            dynamicParameters.Add("@fromDate", request.FromDate);
            //dynamicParameters.Add("@toDate", request.ToDate);

            if (!string.IsNullOrWhiteSpace(request.ToDate))
            {
                if (DateTime.TryParse(request.ToDate, out var toDate))
                {
                    // If the time is midnight (i.e., not specified by the user), set it to the end of the day.
                    if (toDate.TimeOfDay == TimeSpan.Zero)
                    {
                        dynamicParameters.Add("@toDate", toDate.Date.AddDays(1).AddTicks(-1));
                    }
                    else
                    {
                        dynamicParameters.Add("@toDate", toDate);
                    }
                }
            }
            else
            {
                dynamicParameters.Add("@toDate", null);
            }

            dynamicParameters.Add("@createdBy", request.CreatedBy);
            if(!string.IsNullOrWhiteSpace(searchOnColumn))
             dynamicParameters.Add("@searchColumnName", searchOnColumn);
            if(!string.IsNullOrWhiteSpace(request.SearchValue))
             dynamicParameters.Add("@searchColumnValue", request.SearchValue);
            if (!string.IsNullOrWhiteSpace(securityGroupId))
                dynamicParameters.Add("@securityGroupId", securityGroupId);
            if (!string.IsNullOrWhiteSpace(campaignId))
                dynamicParameters.Add("@campaignId", campaignId);
            if (!string.IsNullOrWhiteSpace(upn))
                dynamicParameters.Add("@upn", upn);

            dynamicParameters.Add("@internalDIFRequest",false,DbType.Boolean);
            dynamicParameters.Add("@totalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

            using var connection = _dapperService.CreateConnection();
            using var multi = await connection.QueryMultipleAsync("[sel_flpProcessList]", dynamicParameters, commandType: CommandType.StoredProcedure, commandTimeout: 300);

            // Read the main data result set
            var result = multi.Read<ProcessedFileListResponse>().ToList();

            // Read the pagination information result set
            var paginationInfo = multi.Read<PaginationInfo>().FirstOrDefault();

            // Fallback to output parameter if pagination info is not available
            int totalCount = paginationInfo?.TotalCount ?? dynamicParameters.Get<int>("@totalCount");
            int pageNumber = paginationInfo?.PageNumber ?? request.PageNumber;
            int pageSize = paginationInfo?.PageSize ?? request.PageSize;
            int totalPages = paginationInfo?.TotalPages ?? (totalCount > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0);

            return new ProcessedFileResponse
            {
                Response = result,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages
            };


        }
       
        public async Task<IEnumerable<ConfigFileStatusResponse>> StatusFlpconfigurationID(StatusRequest statusRequest)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@flpConfigurationId", statusRequest.flpConfigurationId);
            var result = await _dapperService.GetMultipleRowsAsync<ConfigFileStatusResponse>("sel_FileUploadStatusByProcessId", parameters, commandType: CommandType.StoredProcedure);
            return result;
        }

        public async Task<IEnumerable<FileStatusReportResponse>> StatusUploadFileReport(FileUploadDetailedStatusRequest fileUploadDetailedStatusRequest)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@flpConfigurationId", fileUploadDetailedStatusRequest.flpConfigurationId);
            parameters.Add("@uploadFileId", fileUploadDetailedStatusRequest.uploadFileId);
            if (!string.IsNullOrWhiteSpace(fileUploadDetailedStatusRequest.tabName))
            {
                parameters.Add("@tabName", fileUploadDetailedStatusRequest.tabName);
            }
            var result = await _dapperService.GetMultipleRowsAsync<FileStatusReportResponse>("sel_GetDetailedFileUploadStatus", parameters, commandType: CommandType.StoredProcedure);
            return result;
        }

    }



}
