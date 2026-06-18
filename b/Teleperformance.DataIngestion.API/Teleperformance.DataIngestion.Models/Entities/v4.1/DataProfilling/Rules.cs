using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.DTOs.v4._1.DataProfilling;

namespace Teleperformance.DataIngestion.Models.Entities.v4._1.DataProfilling
{
    public class Rules
    {
        public int id { get; set; }
        public int ruleId { get; set; }
        public string ruleName { get; set; }
        public bool isActive { get; set; }
    }

    public class RuleTypes
    {
        public int id { get; set; }
        public int ruleTypeId { get; set; }
        public string ruleTypeName { get; set; }
        public string category { get; set; }
        public string description { get; set; }
        public bool isActive { get; set; }
    }

    public class SubRules
    {
        public int id { get; set; }
        public int subRuleId { get; set; }
        public int ruleTypeId { get; set; }
        public string subRuleName { get; set; }
        public bool isActive { get; set; }
    }

    public class Patterns : SingularPlurals
    {
        public int id { get; set; }
        public int patternId { get; set; }
        public int subRuleId { get; set; }
        //public int ruleId { get; set; }
        public string patternName { get; set; }
        public bool isActive { get; set; }
    }

    public class ConditionalOperators
    {
        public int id { get; set; }
        public int conditionalOperatorId { get; set; }
        public string conditionalOperatorName { get; set; }
        public bool isActive { get; set; }
    }

    public class RuleSetNames
    {
        public string ruleSetNameId { get; set; }
        public string ruleSetName { get; set; }
    }

    public class RuleSet
    {
        public int id { get; set; }
        public int SPNameId { get; set; }
        public string ruleSetNameId { get; set; }
        public string ruleSetName { get; set; }        
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
        public bool? isAllowNullOrEmptySpaces { get; set; }
        public int ruleSetType { get; set; }
        public string ruleColumnName2 { get; set; }
        private string _ruleColumnNameRaw;

        public string[] ruleColumnName => _ruleColumnNameRaw?.Split(',') ?? Array.Empty<string>();

        public string ruleColumnNameRaw
        {
            get => _ruleColumnNameRaw;
            set => _ruleColumnNameRaw = value;
        }

        //private string _ruleColumnNameRaw2;

        //public string[] ruleColumnName2 => _ruleColumnNameRaw2?.Split(',') ?? Array.Empty<string>();

        //public string ruleColumnNameRaw2
        //{
        //    get => _ruleColumnNameRaw2;
        //    set => _ruleColumnNameRaw2 = value;
        //}

        //public RuleSetNames[] RuleSetNames { get; set; }
    }

    public class ValidationSPNames
    {
        public int id { get; set; }
        public int SPNameId { get; set; }
        public string SPName { get; set; }

    }
}
