using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService
{
    public class DataSliceGetDataRequestDto 
    {
        public string consumerApplicationId { get; set; }
        public string sourceDataObjId { get; set; }
        public int pageNo { get; set; }
        public int pageSize { get; set; }
        public string filter { get; set; }
    }

}
