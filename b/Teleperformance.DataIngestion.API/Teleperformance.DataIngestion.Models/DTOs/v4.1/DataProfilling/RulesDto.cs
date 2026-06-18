using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Entities.v1._0;
using Teleperformance.DataIngestion.Models.Helpers;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1.DataProfilling
{
    public class SingularPlurals
    {
        public string sing { get; set; }
        public string plu { get; set; }
    }
    public class RulesDto
    {
        public int ruleId { get; set; }
        public string ruleName { get; set; }
    }
    public class RuleTypesDto
    {
        public int ruleTypeId { get; set; }
        public string ruleTypeName { get; set; }
        public string category { get; set; }
        public string description { get; set; }
    }

    public class SubRulesDto
    {
        public int id { get; set; }
        public int ruleTypeId { get; set; }
        public int subRuleId { get; set; }
        public string subRuleName { get; set; }
    }

    public class PatternsDto : SingularPlurals
    {
        public int patternId { get; set; }
        public int subRuleId { get; set; }
        //public int ruleId { get; set; }
        public string patternName { get; set; }

    }

    public class ConditionalOperatorsDto
    {
        public int conditionalOperatorId { get; set; }
        public string conditionalOperatorName { get; set; }
    }

    public class RuleSetNamesDto
    {
        public string ruleSetNameId { get; set; }
        public string ruleSetName { get; set; }
    }

    public class RuleSetDto
    {
        public int id { get; set; }
        public int SPNameId { get; set; }
        public string ruleSetName { get; set; }
        public string ruleSetNameId { get; set; }
        public int ruleTypeId { get; set; }
        public int? subRuleId { get; set; }
        //public string ruleColumnName { get; set; }
        public string ruleDescription { get; set; }        
        public string prompt { get; set; }
        public string format { get; set; }
        public int? patternId { get; set; }
        public int? conditionId { get; set; }
        public decimal? fromValue { get; set; }
        public decimal? toValue { get; set; }
        public bool? isCombinationRule { get; set; }
        public string description { get; set; }
        public bool isActive { get; set; }
        public bool? isGlobal { get; set; }
        private string _ruleColumnNameRaw;
        private string _ruleColumnNameRaw2;
        public bool? isAllowNullOrEmptySpaces { get; set; }
        public int ruleSetType { get; set; }
        public string[] ruleColumnName => _ruleColumnNameRaw?.Split(',') ?? Array.Empty<string>();
        public string ruleColumnName2 { get; set; }
        //public string[] ruleColumnName2 => _ruleColumnNameRaw2?.Split(',') ?? Array.Empty<string>();

        public string ruleColumnNameRaw
        {
            get => _ruleColumnNameRaw;
            set => _ruleColumnNameRaw = value;
        }
        
        //public string ruleColumnNameRaw2
        //{
        //    get => _ruleColumnNameRaw2;
        //    set => _ruleColumnNameRaw2 = value;
        //}
    }

    public class RuleSetNameListRequest : ClsSecurityGroupBase
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

    public class RuleSetNameListResponse
    {
        public int id { get; set; }
        public string ruleSetName { get; set; }
        public string ruleSetNameId { get; set; }
        public string description { get; set; }
        public string username { get; set; }
        public string creationDateTime { get; set; }
        public string updationDateTime { get; set; }
        public bool isGlobal { get; set; }
        public int ruleCount { get; set; }
        //public int RowNo { get; set; }
    }

    public class RuleSetNameResponse
    {
        public List<RuleSetNameListResponse> Response { get; set; }
        public int TotalCount { get; set; }
    }

    public class ValidationSPNamesDto
    {
        public int SPNameId { get; set; }
        public string SPName { get; set; }
    }
}
