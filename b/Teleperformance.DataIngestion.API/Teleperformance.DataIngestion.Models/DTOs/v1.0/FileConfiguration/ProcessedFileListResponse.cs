using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration
{

    public class ProcessedFileListResponse
    {
        
        public string ConfigurationId { get; set; }
        public string FileName { get; set; }
        public string ProcessName { get; set; }
        public string CreatedDate { get; set; }        
        public string StatusName { get; set; }
        public string RegionName { get; set; }
        public string SubRegionName { get; set; }
        public string ClientName { get; set; }
        public string DatabaseName { get; set; }
        public string TableName { get; set; }
        public string CreatedBy { get; set; }
        public string LoginId { get; set; }
        public int TotalRecords { get; set; }
        public int ProcessedRecords { get; set; }
        public int DuplicateRecords { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string FileId { get; set; }
        public string Description { get; set; }
        public string TabName { get; set; }
    }

    public class ProcessedFileResponse
    {
        public List<ProcessedFileListResponse> Response { get; set; }
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class PaginationInfo
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
