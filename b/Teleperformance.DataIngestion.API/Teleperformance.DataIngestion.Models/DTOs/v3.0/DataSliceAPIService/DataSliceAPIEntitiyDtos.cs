using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService
{
    public class DataSliceAPIEntitiyDtos
    {
        
    }

    public class DsRegionSubRegionRequestDto
    {
        public int? regionId { get; set; }
    }


    public class DsClientRequestDto
    {
        public int? regionId { get; set; }
        public string subRegionCode { get; set; }
    }


    public class DsRegionResponseDto
    {
        public int region_ident { get; set; }
        public string  region { get; set; }
    }


    public class DsSubRegionResponseDto
    {
        public string subsubregion_code { get; set; }
        public string subsubregion { get; set; }
    }

    public class DsClientResponseDto
    {
        public int client_ident { get; set; }
        public string client_full_name { get; set; }
    }




}
