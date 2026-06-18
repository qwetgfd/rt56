using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0
{
    public class DatabaseResponse
    {
        public int RecordId { get; set; }
        public string? Result { get; set; }
        public string? Message { get; set; }
    }
}
