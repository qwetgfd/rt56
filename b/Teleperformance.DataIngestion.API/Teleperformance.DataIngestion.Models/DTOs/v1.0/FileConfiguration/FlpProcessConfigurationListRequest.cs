
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Helpers;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration
{
    public class FlpProcessConfigurationListRequest : ClsSecurityGroupBase
    {
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        [ScriptHtmlAttributeValidation(ErrorMessage = "Invalid SearchOnColumn")]
        public string? SearchOnColumn { get; set; }
        [ScriptHtmlAttributeValidation(ErrorMessage = "Invalid CreatedBy")]
        public string? CreatedBy { get; set; }
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public int TotalCount { get; set; }
        [ScriptHtmlAttributeValidation(ErrorMessage = "Invalid SearchValue")]
        public string? SearchValue { get; set; }
        public bool isActive { get; set; } = true;
    }
}
