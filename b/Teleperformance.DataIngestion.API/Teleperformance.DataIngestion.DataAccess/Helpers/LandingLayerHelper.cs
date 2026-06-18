using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.DataAccess.Helpers
{
    public class LandingLayerHelper
    {
        /// <summary>
        /// Builds the target file name by applying prefix, date format, and time format in order.
        /// Rules:
        /// - If prefix present => {prefix}_{base}
        /// - If date present => append _{date}
        /// - If time present => append _{time}
        /// - Keeps original extension; sanitizes invalid filename chars.
        /// </summary>
        public static string BuildTargetFileName(string originalFileName, string? prefix, string? dateFormat, string? timeFormat, DateTime? now = null)
        {
            if (string.IsNullOrWhiteSpace(originalFileName)) return originalFileName;

            now ??= DateTime.Now;
            var baseName = FlpConfigurationHelper.GetFileNameWithoutExtension(originalFileName);
            var extension = FlpConfigurationHelper.GetFileExtension(originalFileName); // includes leading '.'

            // Build name parts
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(prefix))
            {
                parts.Add(prefix!.Trim());
            }

            parts.Add(baseName);

            if (!string.IsNullOrWhiteSpace(dateFormat))
            {
                string datePart;
                try
                {
                    datePart = now.Value.ToString(dateFormat, CultureInfo.InvariantCulture);
                }
                catch
                {
                    // If date format invalid, fallback to yyyyMMdd
                    datePart = now.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                }
                parts.Add(datePart);
            }

            if (!string.IsNullOrWhiteSpace(timeFormat))
            {
                string timePart;
                try
                {
                    timePart = now.Value.ToString(timeFormat, CultureInfo.InvariantCulture);
                }
                catch
                {
                    // If time format invalid, fallback to HHmmss
                    timePart = now.Value.ToString("HHmmss", CultureInfo.InvariantCulture);
                }
                parts.Add(timePart);
            }

            // Join with underscore
            var nameJoined = string.Join("_", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

            // Replace invalid file name characters with underscore
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var ch in invalid)
            {
                nameJoined = nameJoined.Replace(ch, '_');
            }

            // If no extension on original, keep it empty
            return $"{nameJoined}{extension}";
        }

        /// <summary>
        /// Validates that the extension is present in the allowed extension list.
        /// Supports entries with or without leading dot; comparison is case-insensitive.
        /// If the list is empty, treat as "no restriction".
        /// </summary>
        public static bool IsExtensionAllowed(string extension, List<string> allowedExtensions)
        {
            if (allowedExtensions == null) return true;

            var normalizedList = allowedExtensions
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e =>
                {
                    var s = e.Trim();
                    return s.StartsWith(".") ? s.ToLowerInvariant() : "." + s.ToLowerInvariant();
                })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (normalizedList.Count == 0) return true; // No restriction

            var ext = string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.ToLowerInvariant();
            return normalizedList.Contains(ext);
        }

        /// <summary>
        /// Returns true if any regex from the list matches the file name.
        /// If the list is empty or null, treat as "no restriction" (=true).
        /// Each regex is applied with IgnoreCase.
        /// </summary>
        public static bool IsNamePassingAnyRegex(string fileName, IEnumerable<string> regexList)
        {
            if (regexList == null) return true;

            var list = regexList.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
            if (list.Count == 0) return true;

            foreach (var pattern in list)
            {
                try
                {
                    if (Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Skip invalid regex patterns
                    continue;
                }
            }

            // If there were patterns and none matched, it's not valid
            return false;
        }
    }
}
