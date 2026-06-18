using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.ValidateFileRules
{
    public class ValidationRuleApiResponse
    {
        public string Result { get; set; }
        public string ResponseMessage { get; set; }
        public int FailedRows { get; set; }
    }
}
