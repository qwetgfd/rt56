using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingProcess.TableSchemaValidation
{
    public class ParquetColumnMapping
    {
        public int Id { get; set; }
        public string ParquetColMappingId { get; set; }
        public string ProcessName { get; set; }
        public string TableName { get; set; }
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public bool Active { get; set; }
    }
}
