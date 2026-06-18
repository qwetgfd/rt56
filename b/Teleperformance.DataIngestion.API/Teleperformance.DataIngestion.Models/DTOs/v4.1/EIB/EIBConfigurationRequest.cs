using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Helpers;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.EIB
{
    public class EIBConfigurationRequest
    {
        public string EIBId { get; set; }
        public string configurationId { get; set; }
        public string eibName { get; set; }
        public string description { get; set; }
        public int? noOfBusinessProcess { get; set; }
        public string updatedDateTime { get; set; }
        public string createdBy { get; set; }
        public string updatedBy { get; set; }
        public bool isActive { get; set; }
        public int? countryId { get; set; }
        public string countryName { get; set; }
        public List<BusinessProcessNames> businessProcessNames { get; set; }
        public List<BusinessProcessDBViewMapping> businessProcessDBViewMapping { get; set; }
        public List<BusinessProcessFileUrlMapping> businessProcessFileUrlMapping { get; set; }
    }

    public class BusinessProcessNames
    {
        public string businessProcessNameId { get; set; } //pk
        public string EIBId { get; set; }
        public string businessProcessName { get; set;}
        public int fieldCount { get; set; }
        public bool isRequired { get; set; }
        public bool isDisabled { get; set; } //we disable if no sheet found but part of overview
        public string creationDateTime { get; set; }
        public bool isActive { get; set; }
        public string createdBy { get; set; }
        public string updatedBy { get; set; }        
    }

    public class BusinessProcessFileUrlMapping
    {
        public string EIBId { get; set; }
        public string businessProcessNameId { get; set; }
        public string fileUrl { get; set; }
        public string filename { get; set; }
    }
    public class BusinessProcessDBViewMapping
    {
        public string bpnViewId { get; set; } //pk
        //public int viewId { get; set; } //fk ? 
        public string businessProcessName { get; set; }
        public string businessProcessNameId { get; set; }
        public string viewName { get; set; }
        public int viewNameId { get; set; }
        public int columnCount { get; set; } //we save this to have a record on how many columns the view when it was mapped
        public string fromColumn { get; set; }
        public string toColumn { get; set; }
        public string updatedBy { get; set; }        
        public bool isActive { get; set; }
    }

    public class PDMProfilingRequestDto
    {

        public int ProcedureNameId { get; set; }
        public string ProcessedBy { get; set; } = string.Empty;

    }
    public class EIBListRequestDto 
    {
        public int pageSize { get; set; }
        public int pageNumber { get; set; }
        [ScriptHtmlAttributeValidation(ErrorMessage = "Invalid SearchOnColumn")]
        public string? searchOnColumn { get; set; }
        [ScriptHtmlAttributeValidation(ErrorMessage = "Invalid CreatedBy")]
        public string? createdBy { get; set; }
        public string? fromDate { get; set; }
        public string? toDate { get; set; }
        public int totalCount { get; set; }
        [ScriptHtmlAttributeValidation(ErrorMessage = "Invalid SearchValue")]
        public string? searchValue { get; set; }
        public bool isActive { get; set; } = true;
    }

    public class EIBResponseDto
    {
        public List<EIBListResponse> Response { get; set; }
        public int? TotalCount { get; set; }        
    }
    public class EIBListResponse
    {
        public string EIBId { get; set; }
        public string EIBName { get; set; }
        public string description { get; set; }
        public string createdBy { get; set; }
        public string creationDateTime { get; set; }
        public string? modifiedDateTime { get; set; }
        public string? generationStartDateTime { get; set; }
        public string? generationEndDateTime { get; set; }
        public string? updatedBy { get; set; }
        public string mappedCount { get; set; }
        public string status { get; set; }
        public string fileUrl { get; set; }
    }
    
    public class EIBCountry
    {
        public int id { get; set; }
        public string countryName { get; set; }
        public string description { get; set; }
    }
}
