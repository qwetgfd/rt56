using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Helpers;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.UploadFileStatus
{
    public class StatusRequest
    {
        [ScriptHtmlAttributeValidation(ErrorMessage = "Invalid flpConfigurationId")]
        public string flpConfigurationId { set; get; }

    }
}
