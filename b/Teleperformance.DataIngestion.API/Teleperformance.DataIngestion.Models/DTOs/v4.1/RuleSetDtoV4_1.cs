using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.DTOs.v1._0.FileConfiguration;

namespace Teleperformance.DataIngestion.Models.DTOs.v4._1
{
    public class RuleSetDtoV4_1
    {
        public int id { get; set; }
        public int ruleTypeId { get; set; }
        public int? subRuleId { get; set; }
        //public string ruleColumnName { get; set; }
        public string ruleDescription { get; set; }
        public string prompt { get; set; }
        public string format { get; set; }
        public int? patternId { get; set; }
        public int? conditionId { get; set; }
        public int? fromValue { get; set; }
        public int? toValue { get; set; }
        public bool? isCombinationRule { get; set; }

        private string _ruleColumnNameRaw;

        public string[] ruleColumnName => _ruleColumnNameRaw?.Split(',') ?? Array.Empty<string>();

        public string ruleColumnNameRaw
        {
            get => _ruleColumnNameRaw;
            set => _ruleColumnNameRaw = value;
        }
    }
}
