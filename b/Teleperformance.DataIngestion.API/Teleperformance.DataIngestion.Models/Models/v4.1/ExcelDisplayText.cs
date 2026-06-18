using System;
using System.Globalization;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace Teleperformance.DataIngestion.Models.Models.v4._1
{
   

    public static class ExcelDisplayText
    {
        /// <summary>
        /// Formats the cell like Excel would, but if the cell's date format uses a 2-digit year (yy),
        /// upgrades it to 4-digit (yyyy) and re-renders, preserving locale, separators, and time.
        /// Works for both built-in and custom formats. Uses FormulaEvaluator when provided.
        /// </summary>
        public static string FormatWithFourDigitYear1(ICell cell, IFormulaEvaluator evaluator = null, CultureInfo culture = null)
        {
            if (cell == null) return string.Empty;
            culture ??= CultureInfo.CurrentCulture;

            var formatter = new DataFormatter(culture);

            // Evaluate formulas so DateUtil/DateCellValue are correct
            if (cell.CellType == CellType.Formula && evaluator != null)
            {
                evaluator.EvaluateFormulaCell(cell);
            }

            // First, get what Excel would normally show
            string asExcelShows = (evaluator != null)
                ? formatter.FormatCellValue(cell, evaluator)
                : formatter.FormatCellValue(cell);

            // If not a date cell, return as is
            if (!DateUtil.IsCellDateFormatted(cell))
                return asExcelShows;

            // No style/format string? Nothing to fix
            var style = cell.CellStyle;
            if (style == null) return asExcelShows;

            string fmt = style.GetDataFormatString();
            if (string.IsNullOrWhiteSpace(fmt)) return asExcelShows;

            // If already yyyy, we’re done
            if (fmt.IndexOf("yyyy", StringComparison.Ordinal) >= 0)
                return asExcelShows;

            // Normalize only yy -> yyyy using a placeholder to protect existing yyyy (rare, but safe)
            const string PH = "__YYYY__";
            string normalizedFmt = fmt
                .Replace("yyyy", PH, StringComparison.Ordinal)
                .Replace("yy", "yyyy", StringComparison.Ordinal)
                .Replace(PH, "yyyy", StringComparison.Ordinal);

            // No change? Return original
            if (normalizedFmt == fmt)
                return asExcelShows;

            // Create a temporary style with the normalized format
            var wb = cell.Sheet.Workbook;
            var tmpStyle = wb.CreateCellStyle();
            tmpStyle.CloneStyleFrom(style);
            short fmtIdx = wb.CreateDataFormat().GetFormat(normalizedFmt);
            tmpStyle.DataFormat = fmtIdx;

            var originalStyle = cell.CellStyle;
            cell.CellStyle = tmpStyle;
            try
            {
                // Ask DataFormatter again to render using the upgraded yyyy
                string fixedText = (evaluator != null)
                    ? formatter.FormatCellValue(cell, evaluator)
                    : formatter.FormatCellValue(cell);
                return fixedText;
            }
            finally
            {
                // Restore original style to avoid mutating the workbook
                cell.CellStyle = originalStyle;
            }
        }


        public static string FormatWithFourDigitYear(ICell cell,IFormulaEvaluator evaluator = null,CultureInfo culture = null)
        {
            if (cell == null) return string.Empty;

            culture ??= CultureInfo.CurrentCulture;
            var formatter = new DataFormatter(culture);

            try
            {
                // Evaluate formulas so DateUtil/DateCellValue are correct
                if (cell.CellType == CellType.Formula && evaluator != null)
                {
                    evaluator.EvaluateFormulaCell(cell);
                }

                // First, get what Excel would normally show
                string asExcelShows = (evaluator != null)
                    ? formatter.FormatCellValue(cell, evaluator)
                    : formatter.FormatCellValue(cell);

                // If not a date cell, return as is
                if (!DateUtil.IsCellDateFormatted(cell))
                    return asExcelShows;

                // No style/format string? Nothing to fix
                var style = cell.CellStyle;
                if (style == null) return asExcelShows;

                string fmt = style.GetDataFormatString();
                if (string.IsNullOrWhiteSpace(fmt)) return asExcelShows;

                // If already yyyy, we’re done
                if (fmt.IndexOf("yyyy", StringComparison.Ordinal) >= 0)
                    return asExcelShows;

                // Normalize only yy -> yyyy
                const string PH = "__YYYY__";
                string normalizedFmt = fmt
                    .Replace("yyyy", PH, StringComparison.Ordinal)
                    .Replace("yy", "yyyy", StringComparison.Ordinal)
                    .Replace(PH, "yyyy", StringComparison.Ordinal);

                if (normalizedFmt == fmt)
                    return asExcelShows;

                // Temporary style
                var wb = cell.Sheet.Workbook;
                var tmpStyle = wb.CreateCellStyle();
                tmpStyle.CloneStyleFrom(style);
                short fmtIdx = wb.CreateDataFormat().GetFormat(normalizedFmt);
                tmpStyle.DataFormat = fmtIdx;

                var originalStyle = cell.CellStyle;
                cell.CellStyle = tmpStyle;

                try
                {
                    return (evaluator != null)
                        ? formatter.FormatCellValue(cell, evaluator)
                        : formatter.FormatCellValue(cell);
                }
                finally
                {
                    // Always restore original style
                    cell.CellStyle = originalStyle;
                }
            }
            catch (Exception ex)
            {
                // IMPORTANT: do not throw, do not crash parquet conversion
                // Optional: log ex here

                return formatter.FormatCellValue(cell);
            }
        }
    }
}
