using Azure.Storage.Blobs;
using DocumentFormat.OpenXml.Spreadsheet;
using ExcelDataReader;
using ExcelNumberFormat;
using NPOI.SS.Formula.Eval;
using NPOI.SS.Formula.Functions;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Teleperformance.DataIngestion.Models.Helpers;

namespace Teleperformance.DataIngestion.DataAccess.Helpers
{
    public class ExcelHelper
    {

        public static void excelSheetToCsv(Stream xlsbStream, Stream csvStream, string sheetName, CultureInfo culture = null)
        {
            culture ??= CultureInfo.InvariantCulture;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            using var reader = ExcelReaderFactory.CreateReader(xlsbStream);
            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = false
                }
            });

            var table = dataSet.Tables[sheetName];
            if (table == null)
                throw new ArgumentException($"Sheet '{sheetName}' not found.");

            using var writer = new StreamWriter(csvStream, Encoding.UTF8, leaveOpen: true);

            foreach (DataRow row in table.Rows)
            {
                var values = row.ItemArray.Select(v => $"\"{v?.ToString().Replace("\"", "\"\"")}\"");
                writer.WriteLine(string.Join(",", values));
            }

            writer.Flush();
            csvStream.Position = 0; // Reset stream position if needed
        }

        public static async Task<Stream> ConvertExcelStreamToCsvStreamAsync(
        Stream excelStream,
        string sheetName,
        CultureInfo culture = null)
        {
            culture ??= CultureInfo.InvariantCulture;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            // Ensure seekable input
            if (!excelStream.CanSeek)
            {
                var memStream = new MemoryStream();
                await excelStream.CopyToAsync(memStream).ConfigureAwait(false);
                memStream.Position = 0;
                excelStream = memStream;
            }
            else
            {
                excelStream.Position = 0;
            }

            var outputStream = new MemoryStream();
            using var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true);
            using var reader = ExcelReaderFactory.CreateReader(excelStream);

            bool readFirstSheetOnly = string.IsNullOrWhiteSpace(sheetName);
            bool sheetFound = false;

            // Helper local function: process current sheet to CSV
            async Task ProcessCurrentSheetAsync()
            {
                bool skipFirstNonEmptyRow = true;
                int firstNonEmptyCol = -1;

                while (reader.Read())
                {
                    var rowValues = new List<string>();
                    bool isEmptyRow = true;

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        object rawValue = reader.GetValue(i);
                        string value = rawValue?.ToString() ?? string.Empty;

                        // Try to get the number format per cell/column
                        string format = null;
                        try
                        {
                            format = reader.GetNumberFormatString(i)?.ToLowerInvariant();
                        }
                        catch
                        {
                            // Some providers might not support GetNumberFormatString; fallback gracefully
                            format = null;
                        }

                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            try
                            {
                                // Percent "0%"
                                if (format == "0%")
                                {
                                    if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double percentVal))
                                    {
                                        value = (percentVal * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
                                    }
                                }
                                // Date formats (contains d,m,y)
                                else if (!string.IsNullOrEmpty(format) &&
                                         format.Contains("d") && format.Contains("m") && format.Contains("y"))
                                {
                                    if (DateTime.TryParse(value, culture, DateTimeStyles.None, out DateTime dateVal))
                                    {
                                        try
                                        {
                                            // Render "as Excel would"
                                            var nf = new NumberFormat(format);
                                            value = nf.Format(rawValue, culture) ?? value;
                                        }
                                        catch
                                        {
                                            // Fallback to ISO-like formats depending on hour presence
                                            value = (format.Contains("h") || format.Contains("H"))
                                                ? dateVal.ToString("yyyy-MM-dd HH:mm:ss", culture)
                                                : dateVal.ToString("yyyy-MM-dd", culture);
                                        }
                                    }
                                }
                                // Time-like formats (contains h/H)
                                else if (!string.IsNullOrEmpty(format) &&
                                         (format.Contains("h") || format.Contains("H")))
                                {
                                    if (DateTime.TryParse(value, culture, DateTimeStyles.None, out DateTime timeVal))
                                    {
                                        try
                                        {
                                            var nf = new NumberFormat(format);
                                            value = nf.Format(rawValue, culture) ?? value;
                                        }
                                        catch
                                        {
                                            value = timeVal.ToString("HH:mm:ss", culture);
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Fallback to raw string
                            }

                            isEmptyRow = false;
                            if (firstNonEmptyCol == -1 || i < firstNonEmptyCol)
                                firstNonEmptyCol = i;
                        }

                        // CSV escaping for quotes
                        value = value.Replace("\"", "\"\"");
                        rowValues.Add($"\"{value}\"");
                    }

                    if (isEmptyRow && skipFirstNonEmptyRow)
                        continue;

                    skipFirstNonEmptyRow = false;

                    if (firstNonEmptyCol != -1)
                    {
                        var trimmedRow = rowValues.Skip(firstNonEmptyCol);
                        await writer.WriteLineAsync(string.Join(",", trimmedRow)).ConfigureAwait(false);
                    }
                }
            }

            if (readFirstSheetOnly)
            {
                // Process the first (current) sheet only
                sheetFound = true;
                await ProcessCurrentSheetAsync().ConfigureAwait(false);
            }
            else
            {
                // Iterate sheets until name matches
                do
                {
                    if (reader.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
                    {
                        sheetFound = true;
                        await ProcessCurrentSheetAsync().ConfigureAwait(false);
                        break;
                    }
                } while (reader.NextResult());
            }

            if (!sheetFound)
                throw new ArgumentException($"Sheet '{sheetName}' not found.");

            await writer.FlushAsync().ConfigureAwait(false);
            outputStream.Position = 0;
            return outputStream;
        }

       
        //public static async Task<Stream> ConvertExcelStreamToCsvStreamAsyncV2(Stream excelStream, string sheetName, CultureInfo culture = null)
        //{
        //    culture ??= CultureInfo.InvariantCulture;
        //    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        //    if (!excelStream.CanSeek)
        //    {
        //        var memStream = new MemoryStream();
        //        await excelStream.CopyToAsync(memStream);
        //        memStream.Position = 0;
        //        excelStream = memStream;
        //    }
        //    else
        //    {
        //        excelStream.Position = 0;
        //    }

        //    var outputStream = new MemoryStream();
        //    using var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true);
        //    using var reader = ExcelReaderFactory.CreateReader(excelStream);

        //    var sheetFound = false;

        //    do
        //    {
        //        if (reader.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
        //        {
        //            sheetFound = true;
        //            bool skipFirstNonEmptyRow = true;
        //            int firstNonEmptyCol = -1;

        //            while (reader.Read())
        //            {
        //                var rowValues = new List<string>();
        //                bool isEmptyRow = true;

        //                for (int i = 0; i < reader.FieldCount; i++)
        //                {
        //                    object rawValue = reader.GetValue(i);
        //                    string value = rawValue?.ToString() ?? string.Empty;
        //                    string format = reader.GetNumberFormatString(i)?.ToLowerInvariant();

        //                    if (!string.IsNullOrWhiteSpace(value))
        //                    {
        //                        try
        //                        {
        //                            if (format == "0%")
        //                            {
        //                                if (double.TryParse(value, out double percentVal))
        //                                {
        //                                    value = (percentVal * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        //                                }
        //                            }
        //                            else if (format.Contains("d") && format.Contains("m") && format.Contains("y"))
        //                            {
        //                                if (DateTime.TryParse(value, out DateTime dateVal))
        //                                {

                                           
        //                                    if (!string.IsNullOrEmpty(format))
        //                                    {
        //                                        try
        //                                        {
        //                                            var nf = new NumberFormat(format);
        //                                            value = nf.Format(rawValue, culture) ?? value; // render "as Excel would"
        //                                        }
        //                                        catch (Exception ex)
        //                                        {
        //                                            value = format.Contains("h") || format.Contains("H")
        //                                                ? dateVal.ToString("yyyy-MM-dd HH:mm:ss", culture)
        //                                                : dateVal.ToString("yyyy-MM-dd", culture);

        //                                        }
        //                                    }
        //                                    else
        //                                    {
        //                                        // No format string -> fall back to default ToString respecting culture
                                                
        //                                    }

        //                                    //value = format.Contains("h") || format.Contains("H")
        //                                    //    ? dateVal.ToString("yyyy-MM-dd HH:mm:ss", culture)
        //                                    //    : dateVal.ToString("yyyy-MM-dd", culture);
        //                                }
        //                            }
        //                            else if (format.Contains("h") || format.Contains("H"))
        //                            {
        //                                if (DateTime.TryParse(value, out DateTime timeVal))
        //                                {
        //                                    //value = timeVal.ToString("HH:mm:ss", culture);
        //                                    try
        //                                    {
        //                                        var nf = new NumberFormat(format);
        //                                        value = nf.Format(rawValue, culture) ?? value; // render "as Excel would"
        //                                    }
        //                                    catch (Exception ex)
        //                                    {
        //                                        value = format.Contains("h") || format.Contains("H")
        //                                            ? timeVal.ToString("yyyy-MM-dd HH:mm:ss", culture)
        //                                            : timeVal.ToString("yyyy-MM-dd", culture);

        //                                    }
        //                                }
        //                            }
        //                        }
        //                        catch
        //                        {
        //                            // fallback to raw value
        //                        }

        //                        isEmptyRow = false;
        //                        if (firstNonEmptyCol == -1 || i < firstNonEmptyCol)
        //                            firstNonEmptyCol = i;
        //                    }

        //                    value = value.Replace("\"", "\"\"");
        //                    rowValues.Add($"\"{value}\"");
        //                }

        //                if (isEmptyRow && skipFirstNonEmptyRow)
        //                    continue;

        //                skipFirstNonEmptyRow = false;

        //                if (firstNonEmptyCol != -1)
        //                {
        //                    var trimmedRow = rowValues.Skip(firstNonEmptyCol);
        //                    await writer.WriteLineAsync(string.Join(",", trimmedRow));
        //                }
        //            }

        //            break;
        //        }
        //    } while (reader.NextResult());

        //    if (!sheetFound)
        //        throw new ArgumentException($"Sheet '{sheetName}' not found.");

        //    await writer.FlushAsync();
        //    outputStream.Position = 0;
        //    return outputStream;
        //}




        public static XLSBWorkbookModel ConvertXLSBtoXLSX(Stream xlsbStream, CultureInfo culture = null)
        {
            culture ??= CultureInfo.InvariantCulture;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            if (!xlsbStream.CanSeek)
            {
                var memStream = new MemoryStream();
                xlsbStream.CopyTo(memStream);
                memStream.Position = 0;
                xlsbStream = memStream;
            }
            else
            {
                xlsbStream.Position = 0;
            }
            IWorkbook workbook = new XSSFWorkbook();
            int emptyRowCount = 0;
            bool dataStarted = false;

            using var reader = ExcelReaderFactory.CreateReader(xlsbStream);

            // Process only the first sheet
            if (reader != null)
            {
                var sheet = workbook.CreateSheet(string.IsNullOrWhiteSpace(reader.Name) ? "Sheet" : reader.Name);
                var rowsBuffer = new List<object[]>();
                int maxCols = 0;

                while (reader.Read())
                {
                    var row = new object[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        //var value = reader.GetValue(i);
                        //var format = reader.GetNumberFormatString(i);
                        //row[i] = FormatWithExcelNumberFormat(value, format, culture);
                        var value = reader.GetValue(i);
                        var format = reader.GetNumberFormatString(i);
                        if (format != null && format.Contains("d") && format.Contains("m") && format.Contains("y") && !format.ToLower().Contains("h"))
                        {
                            var strChangedDateFormat = ExcelFormatHelper.FormatRemovingMidnightOnly(value, format, culture);
                            value = strChangedDateFormat;
                        }
                        row[i] = value;//FormatWithExcelNumberFormat(value, format, culture);
                    }
                    rowsBuffer.Add(row);
                    maxCols = Math.Max(maxCols, row.Length);
                }

                for (int rowIndex = 0; rowIndex < rowsBuffer.Count; rowIndex++)
                {
                    var rowData = rowsBuffer[rowIndex];
                    bool isEmptyRow = rowData.All(field => string.IsNullOrWhiteSpace(field?.ToString()));

                    if (!dataStarted && !isEmptyRow)
                        dataStarted = true;

                    if (!dataStarted)
                    {
                        emptyRowCount++;
                        continue;
                    }

                    var sheetRow = sheet.CreateRow(rowIndex);
                    for (int colIndex = 0; colIndex < maxCols; colIndex++)
                    {
                        var cellValue = colIndex < rowData.Length ? rowData[colIndex]?.ToString() ?? string.Empty : string.Empty;
                        sheetRow.CreateCell(colIndex).SetCellValue(cellValue);
                    }
                }
            }

            return new XLSBWorkbookModel
            {
                workbook = workbook,
                emptyRowCount = dataStarted ? emptyRowCount : 0
            };
        }

        public static XLSBWorkbookModel ConvertXLSBtoXLSXV2(Stream xlsbStream, CultureInfo culture = null)
        {
            culture ??= CultureInfo.InvariantCulture;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            if (!xlsbStream.CanSeek)
            {
                var memStream = new MemoryStream();
                xlsbStream.CopyTo(memStream);
                memStream.Position = 0;
                xlsbStream = memStream;
            }
            else
            {
                xlsbStream.Position = 0;
            }
            IWorkbook workbook = new XSSFWorkbook();
            int emptyRowCount = 0;
            bool dataStarted = false;

            using var reader = ExcelReaderFactory.CreateReader(xlsbStream);

            do
            {
                var sheet = workbook.CreateSheet(string.IsNullOrWhiteSpace(reader.Name) ? "Sheet" : reader.Name);
                var rowsBuffer = new List<object[]>();
                int maxCols = 0;

                while (reader.Read())
                {
                    var row = new object[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.GetValue(i);
                        var format = reader.GetNumberFormatString(i);
                        row[i] = FormatWithExcelNumberFormat(value, format, culture);
                    }
                    rowsBuffer.Add(row);
                    maxCols = Math.Max(maxCols, row.Length);
                }

                for (int rowIndex = 0; rowIndex < rowsBuffer.Count; rowIndex++)
                {
                    var rowData = rowsBuffer[rowIndex];
                    bool isEmptyRow = rowData.All(field => string.IsNullOrWhiteSpace(field?.ToString()));

                    if (!dataStarted && !isEmptyRow)
                        dataStarted = true;

                    if (!dataStarted)
                    {
                        emptyRowCount++;
                        continue;
                    }

                    var sheetRow = sheet.CreateRow(rowIndex);
                    for (int colIndex = 0; colIndex < maxCols; colIndex++)
                    {
                        var cellValue = colIndex < rowData.Length ? rowData[colIndex]?.ToString() ?? string.Empty : string.Empty;
                        sheetRow.CreateCell(colIndex).SetCellValue(cellValue);
                    }
                }
            } while (reader.NextResult());

            return new XLSBWorkbookModel
            {
                workbook = workbook,
                emptyRowCount = dataStarted ? emptyRowCount : 0
            };
        }

        public static XLSBWorkbookModel ConvertXLSBtoXLSXV3(Stream xlsbStream, string sheetName, CultureInfo culture = null)
        {
            culture ??= CultureInfo.InvariantCulture;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            if (!xlsbStream.CanSeek)
            {
                var memStream = new MemoryStream();
                xlsbStream.CopyTo(memStream);
                memStream.Position = 0;
                xlsbStream = memStream;
            }
            else
            {
                xlsbStream.Position = 0;
            }

            IWorkbook workbook = new XSSFWorkbook();
            int emptyRowCount = 0;
            bool dataStarted = false;

            using var reader = ExcelReaderFactory.CreateReader(xlsbStream);

            do
            {
                // Skip sheets that don't match the requested sheetName
                if (!string.Equals(reader.Name, sheetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var sheet = workbook.CreateSheet(string.IsNullOrWhiteSpace(reader.Name) ? "Sheet" : reader.Name);
                var rowsBuffer = new List<object[]>();
                int maxCols = 0;
                dataStarted = false;
                while (reader.Read())
                {
                    var row = new object[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.GetValue(i);
                        var format = reader.GetNumberFormatString(i);
                        if(format !=null && format.Contains("d") && format.Contains("m") && format.Contains("y") && !format.ToLower().Contains("h"))
                        {                            
                            var strChangedDateFormat = ExcelFormatHelper.FormatRemovingMidnightOnly(value, format, culture);
                            value = strChangedDateFormat;
                        }
                        row[i] = value;//FormatWithExcelNumberFormat(value, format, culture);
                    }
                    rowsBuffer.Add(row);
                    maxCols = Math.Max(maxCols, row.Length);
                }

                for (int rowIndex = 0; rowIndex < rowsBuffer.Count; rowIndex++)
                {
                    var rowData = rowsBuffer[rowIndex];
                    bool isEmptyRow = rowData.All(field => string.IsNullOrWhiteSpace(field?.ToString()));

                    if (!dataStarted && !isEmptyRow)
                        dataStarted = true;

                    if (!dataStarted)
                    {
                        emptyRowCount++;
                        continue;
                    }

                    var sheetRow = sheet.CreateRow(rowIndex);
                    for (int colIndex = 0; colIndex < maxCols; colIndex++)
                    {
                        var cellValue = colIndex < rowData.Length ? rowData[colIndex]?.ToString() ?? string.Empty : string.Empty;
                        sheetRow.CreateCell(colIndex).SetCellValue(cellValue);
                    }
                }

                // Break after processing the matched sheet
                break;

            } while (reader.NextResult());

            return new XLSBWorkbookModel
            {
                workbook = workbook,
                emptyRowCount = dataStarted ? emptyRowCount : 0
            };
        }

        //public static XLSBWorkbookModel ConvertXLSBtoXLSXV3(Stream xlsbStream,string tabName, CultureInfo culture = null)
        //{
        //    culture ??= CultureInfo.InvariantCulture;
        //    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        //    if (!xlsbStream.CanSeek)
        //    {
        //        var memStream = new MemoryStream();
        //        xlsbStream.CopyTo(memStream);
        //        memStream.Position = 0;
        //        xlsbStream = memStream;
        //    }
        //    else
        //    {
        //        xlsbStream.Position = 0;
        //    }
        //    IWorkbook workbook = new XSSFWorkbook();
        //    int emptyRowCount = 0;
        //    bool dataStarted = false;

        //    using var reader = ExcelReaderFactory.CreateReader(xlsbStream);

        //    do
        //    {
        //        var sheet = workbook.CreateSheet(string.IsNullOrWhiteSpace(reader.Name) ? "Sheet" : reader.Name);
        //        var rowsBuffer = new List<object[]>();
        //        int maxCols = 0;

        //        while (reader.Read())
        //        {
        //            var row = new object[reader.FieldCount];
        //            for (int i = 0; i < reader.FieldCount; i++)
        //            {
        //                var value = reader.GetValue(i);
        //                var format = reader.GetNumberFormatString(i);
        //                row[i] = FormatWithExcelNumberFormat(value, format, culture);
        //            }
        //            rowsBuffer.Add(row);
        //            maxCols = Math.Max(maxCols, row.Length);
        //        }

        //        for (int rowIndex = 0; rowIndex < rowsBuffer.Count; rowIndex++)
        //        {
        //            var rowData = rowsBuffer[rowIndex];
        //            bool isEmptyRow = rowData.All(field => string.IsNullOrWhiteSpace(field?.ToString()));

        //            if (!dataStarted && !isEmptyRow)
        //                dataStarted = true;

        //            if (!dataStarted)
        //            {
        //                emptyRowCount++;
        //                continue;
        //            }

        //            var sheetRow = sheet.CreateRow(rowIndex);
        //            for (int colIndex = 0; colIndex < maxCols; colIndex++)
        //            {
        //                var cellValue = colIndex < rowData.Length ? rowData[colIndex]?.ToString() ?? string.Empty : string.Empty;
        //                sheetRow.CreateCell(colIndex).SetCellValue(cellValue);
        //            }
        //        }
        //    } while (reader.NextResult());

        //    return new XLSBWorkbookModel
        //    {
        //        workbook = workbook,
        //        emptyRowCount = dataStarted ? emptyRowCount : 0
        //    };
        //}
        public static XLSBWorkbookModel ConvertStreamToWorkbook(Stream xlsbStream, CultureInfo culture = null)
        {
            culture ??= CultureInfo.InvariantCulture;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            IWorkbook workbook = new XSSFWorkbook();
            int emptyRowCount = 0;
            bool dataStarted = false;

            using var reader = ExcelReaderFactory.CreateReader(xlsbStream);

            do
            {
                var sheet = workbook.CreateSheet(string.IsNullOrWhiteSpace(reader.Name) ? "Sheet" : reader.Name);
                int rowIndex = 0;
                int maxCols = 0;

                while (reader.Read())
                {
                    var rowData = new object[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.GetValue(i);
                        var format = reader.GetNumberFormatString(i);
                        rowData[i] = FormatWithExcelNumberFormat(value, format, culture);
                    }
                    maxCols = Math.Max(maxCols, rowData.Length);

                    bool isEmptyRow = rowData.All(field => string.IsNullOrWhiteSpace(field?.ToString()));

                    if (!dataStarted && !isEmptyRow)
                        dataStarted = true;

                    if (!dataStarted)
                    {
                        emptyRowCount++;
                        continue;
                    }

                    var sheetRow = sheet.CreateRow(rowIndex++);
                    for (int colIndex = 0; colIndex < rowData.Length; colIndex++)
                    {
                        var cellValue = rowData[colIndex]?.ToString() ?? string.Empty;
                        sheetRow.CreateCell(colIndex).SetCellValue(cellValue);
                    }
                }
            } while (reader.NextResult());

            return new XLSBWorkbookModel
            {
                workbook = workbook,
                emptyRowCount = dataStarted ? emptyRowCount : 0
            };
        }

        public static XLSBWorkbookModel ConvertXLSBtoXLSX_Bk(Stream xlsbStream, CultureInfo culture = null)
        {
            culture ??= CultureInfo.InvariantCulture;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            IWorkbook workbook = new XSSFWorkbook();
            int emptyRowCount = 0;
            bool dataStarted = false;

            using var reader = ExcelReaderFactory.CreateReader(xlsbStream);

            do
            {
                var sheet = workbook.CreateSheet(string.IsNullOrWhiteSpace(reader.Name) ? "Sheet" : reader.Name);
                var rowsBuffer = new List<object[]>();
                int maxCols = 0;

                // Read and format all rows
                while (reader.Read())
                {
                    var row = new object[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.GetValue(i);
                        var format = reader.GetNumberFormatString(i);
                        row[i] = FormatWithExcelNumberFormat(value, format, culture);
                    }
                    rowsBuffer.Add(row);
                    maxCols = Math.Max(maxCols, row.Length);
                }

                // Write to XLSX sheet
                for (int rowIndex = 0; rowIndex < rowsBuffer.Count; rowIndex++)
                {
                    var rowData = rowsBuffer[rowIndex];
                    bool isEmptyRow = rowData.All(field => string.IsNullOrWhiteSpace(field?.ToString()));

                    if (!dataStarted && !isEmptyRow)
                        dataStarted = true;

                    if (!dataStarted)
                    {
                        emptyRowCount++;
                        continue;
                    }

                    var sheetRow = sheet.CreateRow(rowIndex);
                    for (int colIndex = 0; colIndex < maxCols; colIndex++)
                    {
                        var cellValue = colIndex < rowData.Length ? rowData[colIndex]?.ToString() ?? string.Empty : string.Empty;
                        sheetRow.CreateCell(colIndex).SetCellValue(cellValue);
                    }
                }

            } while (reader.NextResult());

            return new XLSBWorkbookModel
            {
                workbook = workbook,
                emptyRowCount = dataStarted ? emptyRowCount : 0
            };
        }

        public static XLSBWorkbookModel ConvertXLSBtoXLSX(Stream xlsbStream)
        {
            //  System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            //    // Read .xlsb stream into DataSet
            //   using var reader = ExcelReaderFactory.CreateReader(xlsbStream);

            //    // Fix: Use ExcelDataReader's DataSetConfiguration to enable AsDataSet functionality
            //   var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration { UseColumnDataType=false});
            var dataSet = ReadToStringDataSet(xlsbStream);
            // Create new XSSFWorkbook
            IWorkbook workbook = new XSSFWorkbook();
            int emptyRowCount = 0;
            bool dataStarted = false;


            //var dataSet1 = reader.AsDataSet(new ExcelDataSetConfiguration { UseColumnDataType = false });

            foreach (DataTable table in dataSet.Tables)
            {
                if (!dataStarted)
                {
                    foreach (DataRow row in table.Rows)
                    {
                        // Check if all fields in the row are null or empty
                        bool isEmpty = row.ItemArray.All(field => field == null || string.IsNullOrWhiteSpace(field.ToString()));
                        if (!isEmpty)
                        {
                            dataStarted = true;
                            break; // Stop counting once data starts
                        }
                        emptyRowCount++;
                    }
                }

                ISheet sheet = workbook.CreateSheet(string.IsNullOrWhiteSpace(table.TableName) ? "Sheet" : table.TableName);

                // Write data
                for (int row = 0; row < table.Rows.Count; row++)
                {
                    IRow dataRow = sheet.CreateRow(row);
                    for (int col = 0; col < table.Columns.Count; col++)
                    {
                        var value = table.Rows[row][col]?.ToString();
                        // var d = reader.GetValue(row);
                        dataRow.CreateCell(col).SetCellValue(table.Rows[row][col]?.ToString() ?? string.Empty);
                    }
                }
            }

            XLSBWorkbookModel workbookModel = new XLSBWorkbookModel
            {
                workbook = workbook,
                emptyRowCount = dataStarted ? emptyRowCount : 0 // Only count empty rows if data has started
            };
            return workbookModel;
        }
        public static DataSet ReadToStringDataSet(Stream excelStream, CultureInfo culture = null)
        {
            culture ??= CultureInfo.InvariantCulture;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var result = new DataSet();

            using var reader = ExcelReaderFactory.CreateReader(excelStream);

            if (reader != null)
            {
                var table = new DataTable(reader.Name); // sheet name
                int maxCols = 0;

                List<string> list = new List<string>();

                // First pass to find max column count (optional; can also grow columns on the fly)
                var rowsBuffer = new System.Collections.Generic.List<object[]>();


                while (reader.Read())
                {
                    var row = new object[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        object value = reader.GetValue(i);
                        string fmt = reader.GetNumberFormatString(i); // "0%", "m/d/yyyy", etc.
                        row[i] = FormatWithExcelNumberFormat(value, fmt, culture);
                    }
                    rowsBuffer.Add(row);
                    maxCols = Math.Max(maxCols, row.Length);
                }

                // Define string columns
                for (int c = 0; c < maxCols; c++)
                {
                    var column = new object[maxCols];
                    table.Columns.Add("Col" + (c + 1), typeof(string));
                }

                // Load rows
                foreach (var row in rowsBuffer)
                {
                    var dr = table.NewRow();
                    for (int c = 0; c < row.Length; c++)
                    {
                        dr[c] = row[c] ?? string.Empty;
                    }
                    table.Rows.Add(dr);
                }

                result.Tables.Add(table);

            }
            ;

            return result;
        }

        private static string FormatWithExcelNumberFormat(object value, string excelFormat, CultureInfo culture)
        {
            if (value == null || value == DBNull.Value)
                return string.Empty;

            // If Excel has a number format (percent, date, currency, etc.)
            if (!string.IsNullOrEmpty(excelFormat))
            {
                // Create a NumberFormat instance with the Excel format string
                var nf = new NumberFormat(excelFormat);

                // Format the value using the culture
                return nf.Format(value, culture);
            }

            // Fallback: just convert to string
            return Convert.ToString(value, culture);
        }



        public static string ConvertExcelStreamToCsv(Stream excelStream, CultureInfo culture = null)
        {
            culture ??= CultureInfo.InvariantCulture;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            if (!excelStream.CanSeek)
            {
                var memStream = new MemoryStream();
                excelStream.CopyTo(memStream);
                memStream.Position = 0;
                excelStream = memStream;
            }
            else
            {
                excelStream.Position = 0;
            }

            var csvBuilder = new StringBuilder();

            using var reader = ExcelReaderFactory.CreateReader(excelStream);

            bool skipFirstNonEmptyRow = true;
            int firstNonEmptyCol = -1;


            while (reader.Read())
            {
                var rowValues = new List<string>();
                bool isEmptyRow = true;

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    object rawValue = reader.GetValue(i);
                    string value = rawValue?.ToString() ?? string.Empty;
                    string format = reader.GetNumberFormatString(i)?.ToLowerInvariant();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        try
                        {
                            if (format == "0%")
                            {
                                if (double.TryParse(value, out double percentVal))
                                {
                                    value = (percentVal * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
                                }
                            }
                            else if (format.Contains("d") && format.Contains("m") && format.Contains("y"))
                            {
                                if (DateTime.TryParse(value, out DateTime dateVal))
                                {
                                    if (format.Contains("h") || format.Contains("H"))
                                    {
                                        // DateTime with time
                                        value = dateVal.ToString("yyyy-MM-dd HH:mm:ss", culture);
                                    }
                                    else
                                    {
                                        // Date only
                                        value = dateVal.ToString("yyyy-MM-dd", culture);
                                    }
                                }
                            }
                            else if (format.Contains("h") || format.Contains("H"))
                            {
                                if (DateTime.TryParse(value, out DateTime timeVal))
                                {
                                    value = timeVal.ToString("HH:mm:ss", culture);
                                }
                            }
                            else //if (double.TryParse(value, out double numVal))
                            {
                                // value = numVal.ToString("G", culture); // General number format
                            }
                        }
                        catch
                        {
                            // fallback to raw value
                        }

                        isEmptyRow = false;
                        if (firstNonEmptyCol == -1 || i < firstNonEmptyCol)
                            firstNonEmptyCol = i;
                    }

                    // Escape quotes for CSV
                    value = value.Replace("\"", "\"\"");
                    rowValues.Add($"\"{value}\"");
                }

                if (isEmptyRow && skipFirstNonEmptyRow)
                    continue;

                skipFirstNonEmptyRow = false;

                if (firstNonEmptyCol != -1)
                {
                    var trimmedRow = rowValues.Skip(firstNonEmptyCol);
                    csvBuilder.AppendLine(string.Join(",", trimmedRow));
                }
            }


            return csvBuilder.ToString();
        }

        public static string ConvertExcelStreamToCsv_bk(Stream excelStream)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            if (!excelStream.CanSeek)
            {
                var memStream = new MemoryStream();
                excelStream.CopyTo(memStream);
                memStream.Position = 0;
                excelStream = memStream;
            }
            else
            {
                excelStream.Position = 0;
            }

            using var reader = ExcelReaderFactory.CreateReader(excelStream);
            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                UseColumnDataType = false,
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = false
                }
            });



            var csvBuilder = new System.Text.StringBuilder();

            if (dataSet.Tables.Count > 0)
            {
                DataTable table = dataSet.Tables[0]; // Only first sheet

                // Find the first non-empty column
                int firstNonEmptyCol = -1;
                for (int col = 0; col < table.Columns.Count; col++)
                {
                    bool isColumnEmpty = true;
                    foreach (DataRow row in table.Rows)
                    {
                        var value = row[col]?.ToString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            isColumnEmpty = false;
                            break;
                        }
                    }
                    if (!isColumnEmpty)
                    {
                        firstNonEmptyCol = col;
                        break;
                    }
                }

                if (firstNonEmptyCol != -1)
                {
                    bool skipFirstNonEmptyRow = true;
                    foreach (DataRow row in table.Rows)
                    {
                        if (skipFirstNonEmptyRow)
                        {
                            var rowEmpty = row.ItemArray.All(field => field == null || string.IsNullOrWhiteSpace(field.ToString()));
                            if (rowEmpty)
                                continue;
                        }

                        for (int col = firstNonEmptyCol; col < table.Columns.Count; col++)
                        {
                            skipFirstNonEmptyRow = false;
                            var value = row[col]?.ToString()?.Replace("\"", "\"\"") ?? string.Empty;
                            csvBuilder.Append($"\"{value}\"");
                            if (col < table.Columns.Count - 1)
                                csvBuilder.Append(",");
                        }
                        csvBuilder.AppendLine();
                    }
                }
            }

            return csvBuilder.ToString();
        }


        // Minimal NumberFormat wrapper placeholder if you use a custom renderer.
        // If you're using ExcelDataReader's IFormatProvider approach, adapt accordingly.
        private sealed class NumberFormat
        {
            private readonly string _format;
            public NumberFormat(string format) => _format = format;

            public string Format(object value, IFormatProvider provider)
            {
                // This is a placeholder. If you have a real NumberFormat implementation
                // (e.g., from a library/util), plug it in here.
                // For demonstration, try DateTime or numeric custom formatting fallback:
                if (value is DateTime dt)
                {
                    // Try interpreting Excel-like format strings in a basic way
                    // You might want a more robust mapping here.
                    return dt.ToString(_format.Replace("m", "M"), provider);
                }
                if (value is IFormattable f)
                {
                    return f.ToString(_format, provider);
                }
                return value?.ToString();
            }
        }



    }


   
}
