using Teleperformance.DataIngestion.Models.Enums.v4._0;

namespace Teleperformance.DataIngestion.Models.Helpers
{
    public static class EnumHelper
    {
        public static int? GetLifeCycleStateEnumValueFromDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return null;
            }
            if (Enum.TryParse<LifeCycleStateEnum>(description.ToUpper(), true, out var lifeCycleStateEnumValue))
            {
                return (int)lifeCycleStateEnumValue;
            }
            return null;
        }


        public static int? GetResultStateEnumEnumValueFromDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return null;
            }
            if (Enum.TryParse<ResultStateEnum>(description.ToUpper(), true, out var resultStateEnumValue))
            {
                return (int)resultStateEnumValue;
            }
            return null;
        }
    }
}
