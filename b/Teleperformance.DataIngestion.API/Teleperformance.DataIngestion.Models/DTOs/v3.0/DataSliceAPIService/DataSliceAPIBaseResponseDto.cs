using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService
{
    public class DataSliceAPIBaseResponseDto
    {
        public int responseCode { get; set; }
        public List<string> responseMessage { get; set; }
    }
}
