using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v3._0.DataSliceAPIService
{
    public class DataSliceAPIDataDto
    {
        public  int client_ident { get; set; }
        public  string client_abbr_name { get; set; }
        public  string client_full_name { get; set; }
        public  int mainregion_ident { get; set; }
        public  string mainregion { get; set; }
        public  int region_ident { get; set; }
        public  string region { get; set; }
        public  string subsubregion_code { get; set; }
        public  string subsubregion { get; set; }
    }
}
