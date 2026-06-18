using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService
{
    public class DataSliceAPITokenRequestDto
    {
        public string apiURL { get; set; }
        public string APIVersion { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
                 
    }


}
