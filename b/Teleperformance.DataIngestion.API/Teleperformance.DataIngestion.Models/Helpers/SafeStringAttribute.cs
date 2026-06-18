using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Helpers
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public class SafeStringAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value != null)
            {
                string stringValue = value.ToString();
                if (ContainsSqlInjectionPattern(stringValue))
                {
                    return new ValidationResult("Provide valid input string.");
                }
            }

            return ValidationResult.Success;
        }

        private bool ContainsSqlInjectionPattern(string value)
        {
            // Regular expression pattern to detect SQL injection patterns
            //string sqlInjectionPattern = @"(\bor\b|\b1=1\b|--#)";
            string sqlInjectionPattern = @"(\bor\b|\b1=1\b|--#|\$|%|&)";
            return System.Text.RegularExpressions.Regex.IsMatch(value, sqlInjectionPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }
}
