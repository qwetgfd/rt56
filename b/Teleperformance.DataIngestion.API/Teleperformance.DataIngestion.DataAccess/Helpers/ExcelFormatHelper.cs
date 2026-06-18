using System.Globalization;
using System.Text.RegularExpressions;
using ExcelNumberFormat;


namespace Teleperformance.DataIngestion.DataAccess.Helpers
{


    public static class ExcelFormatHelper
    {

        // Call this inside your loop:
        public static string FormatRemovingMidnightOnly(object value, string? formatString, CultureInfo culture, bool isDate1904 = false)
        {
            if (value == null || value == DBNull.Value) return string.Empty;

            // Pass-through for text cells
            if (value is string s) return s;

            // Convert to DateTime if possible
            DateTime? dt = null;
            if (value is DateTime dtx)
            {
                dt = dtx;
            }
            else if (value is double od)
            {
                var adjusted = isDate1904 ? (od + 1462.0) : od; // 1904 system adjustment if needed
                // Ensure od is within valid OADate range if you want extra safety
                dt = DateTime.FromOADate(adjusted);
            }

            // If it's a recognizable date/time:
            if (dt.HasValue)
            {
                // If time portion is exactly midnight -> return date-only
                if (dt.Value.TimeOfDay == TimeSpan.Zero)
                {
                    return dt.Value.ToShortDateString();//.ToString(culture.DateTimeFormat.ShortDatePattern, culture);
                }

                // Otherwise keep time. If you have a cell-specific format, use it;
                // else fall back to a reasonable datetime pattern.
                var fmt = string.IsNullOrWhiteSpace(formatString) ? "g" /* short date + short time */ : formatString!;
                // If you use ExcelNumberFormat:
                // var nf = new NumberFormat(fmt);
                // return nf.Format(dt.Value, culture);
                return dt.Value.ToString(fmt, culture);
            }

            // Not a date-like value -> just ToString with culture
            return Convert.ToString(value, culture) ?? string.Empty;
        }

    }



}
