using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingProcess.TableSchemaValidation
{
    public class MappingTableDbRequest
    {
        public long id { get; set; }
        public string ProcessName { get; set; }
        public string FlpConfigurationId { get; set; }
        public string TableName { get; set; }
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public string ActionType { get; set; }
    }
}
