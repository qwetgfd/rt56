using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Models.v4._1
{
    public class ColumnTypeItem
    {
        [JsonPropertyName("column")]
        public string Column { get; set; }

        [JsonPropertyName("dataType")]
        public string DataType { get; set; }
    }

    public class ColumnWithDataTypePayload
    {
        [JsonPropertyName("columnWithDataTypeMessage")]
        public List<ColumnTypeItem> ColumnWithDataTypeMessage { get; set; } = new();
    }

}
