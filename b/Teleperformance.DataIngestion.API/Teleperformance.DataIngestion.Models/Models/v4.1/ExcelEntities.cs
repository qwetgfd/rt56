using System;
using System.Globalization;
using NPOI.SS.UserModel;
using NPOI.SS.Util;     // DateUtil
using NPOI.SS.Format;  // BuiltinFormats

public class CellDateFormatInfo
{
    public bool HasCellStyle { get; set; }
    public bool IsDateCell { get; set; }                 // based on DateUtil.IsCellDateFormatted
    public short DataFormatIndex { get; set; }           // the format index
    public string DataFormatString { get; set; }         // the format string (as Excel stores)
    public string BuiltinFormatString { get; set; }      // if built-in, this is non-null
    public bool IsBuiltInFormat { get; set; }            // BuiltinFormats.GetBuiltinFormat(index) matched
    public bool IsCustomFormat { get; set; }             // derived from built-in detection
    public bool LooksLikeDateFormat { get; set; }        // based on IsADateFormat(index, string)
    public bool UsesTwoDigitYear { get; set; }           // contains "yy" but not "yyyy"
    public bool UsesFourDigitYear { get; set; }          // contains "yyyy"
}

public static class ExcelFormatInspector
{
    public static CellDateFormatInfo Inspect(ICell cell)
    {
        var info = new CellDateFormatInfo();

        if (cell == null || cell.CellStyle == null)
        {
            info.HasCellStyle = false;
            return info;
        }

        try
        {
            info.HasCellStyle = true;
            var style = cell.CellStyle;
            info.DataFormatIndex = style.DataFormat;
            info.DataFormatString = style.GetDataFormatString();

            // Built-in format check
            info.BuiltinFormatString = BuiltinFormats.GetBuiltinFormat(info.DataFormatIndex);
            info.IsBuiltInFormat = !string.IsNullOrEmpty(info.BuiltinFormatString);

            // If there is no recognizable built-in string or strings differ, treat as custom
            // (Some locales may return slightly different casing, so use ordinal comparison.)
            info.IsCustomFormat = !info.IsBuiltInFormat ||
                                  !string.Equals(info.DataFormatString, info.BuiltinFormatString, StringComparison.Ordinal);

            // Check if NPOI/Excel considers the cell as a "date cell"
            info.IsDateCell = DateUtil.IsCellDateFormatted(cell);

            // Check if the *format string* looks like a date format (even if value isn't a date)
            info.LooksLikeDateFormat = DateUtil.IsADateFormat(info.DataFormatIndex, info.DataFormatString);

            // Token analysis for year length
            var fmt = info.DataFormatString ?? string.Empty;

            // Remove locale prefixes like "[$-409]" which can obscure simple searches
            // (We won't mutate 'fmt', just a cleaned copy for token detection)
            string cleaned = RemoveLocaleAndEscapes(fmt);

            // Detect 4-digit vs 2-digit
            info.UsesFourDigitYear = cleaned.Contains("yyyy", StringComparison.Ordinal);
            info.UsesTwoDigitYear = !info.UsesFourDigitYear && cleaned.Contains("yy", StringComparison.Ordinal);
        }
        catch (Exception ex)
        {

           
        }

        return info;
    }

    private static string RemoveLocaleAndEscapes(string fmt)
    {
        // Basic cleanup:
        //  - Remove locale specifiers like "[$-409]"
        //  - Remove bracketed conditions [Red] etc.
        //  - Keep tokens but simplify for searching
        string s = fmt;

        // Strip [$-NNN] locale chunks
        int i = 0;
        while (true)
        {
            int start = s.IndexOf("[$-", StringComparison.Ordinal);
            if (start < 0) break;
            int end = s.IndexOf(']', start);
            if (end < 0) break;
            s = s.Remove(start, end - start + 1);
            if (++i > 5) break; // safety
        }

        // Remove color/condition brackets [Red], [DBNum1], etc.
        i = 0;
        while (true)
        {
            int start = s.IndexOf('[', StringComparison.Ordinal);
            if (start < 0) break;
            int end = s.IndexOf(']', start);
            if (end < 0) break;
            s = s.Remove(start, end - start + 1);
            if (++i > 10) break;
        }

        // Unescape backslashes (Excel uses "\" to escape literal chars)
        s = s.Replace("\\", string.Empty);
        // Remove quoted literals "text"
        s = StripQuotedLiterals(s);

        return s;
    }

    private static string StripQuotedLiterals(string s)
    {
        // Remove content inside double quotes, keep one space
        // Example: mm" months" -> mm
        bool inQuote = false;
        var result = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (c == '"')
            {
                inQuote = !inQuote;
                continue;
            }
            if (!inQuote) result.Append(c);
        }
        return result.ToString();
    }
}