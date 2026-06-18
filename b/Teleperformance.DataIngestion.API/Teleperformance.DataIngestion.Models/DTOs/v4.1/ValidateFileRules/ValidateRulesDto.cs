using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.ValidateFileRules
{
    public class ValidateRulesDto
    {
        public string FlpConfigurationId { get; set; }
        public string UploadFileId { get; set; }
        public string? TabName { get; set; }
        public string? ParquetFileURL { get; set; }
        public bool UIValidation { get; set; }
    }
}
