using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Org.BouncyCastle.Bcpg.Attr.ImageAttrib;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;

namespace Teleperformance.DataIngestion.Models.Entities.v4._1
{
    public class RuleSetEntity
    {
        public string flpConfigurationId { get; set; }
        public int id { get; set; }
        public int ruleTypeId { get; set; }
        public string ruleSetNameId { get; set; }
        public string ruleSetName { get; set; }
        public int? subRuleId { get; set; }
        public string[] ruleColumnName { get; set; }
        public string ruleColumnName2 { get; set; }
        public string ruleDescription { get; set; }
        public string prompt { get; set; }
        public string format { get; set; }
        public int? patternId { get; set; }
        public bool? isCombinationRule { get; set; }
        public bool isActive { get; set; }
        public bool? isGlobal { get; set; }
        public int ruleSetType { get; set; }
        public int? conditionId { get; set; }
        public decimal? fromValue { get; set; }
        public decimal? toValue { get; set; }
        public int? spNameId { get; set; }
        public bool? isAllowNullOrSpace { get; set; }
        public bool isUpdated { get; set; }

    }
}
