using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Entities.v3._0.ProcessConfiguration;

namespace Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService
{
    public class DataSliceGetDataResponseDto : DataSliceAPIBaseResponseDto
    {
        public Result result { get; set; }
    }

    public class Result
    {
        public  DataSlicePaginationDto pagination {get;set;}
        public List<DataSliceAPIDataDto> data { get;set;}
    }

    public class FillDataCacheResponse
    {
        public List<DataSliceAPIDataDto> GetDataList { get; set; }
        public List<SecurityGroupMapping> SecurityGroupMappingsList { get; set; }
    }
}
