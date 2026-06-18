using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Entities.v1._0.FileLoadingProcess.TableSchemaValidation
{
    public class MappingTableSchemaResult
    {
        public bool MatchSchema { get; set; }
        public string ErrorMessage { get; set; }
        public bool TableCreated { get; set; }
        public bool FirstTimeTableCreated { get; set; }

    }
}
