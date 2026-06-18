using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Enums
{

    public enum ExcelFileType
    {
        XLS,
        XLSX,
        XLSB
    }

    public static class FileTypeExtensions
    {
        public static string GetExtension(this ExcelFileType fileType)
        {
            return fileType switch
            {
                ExcelFileType.XLS => ".xls",
                ExcelFileType.XLSX => ".xlsx",
                ExcelFileType.XLSB => ".xlsb",
                _ => throw new ArgumentOutOfRangeException(nameof(fileType), fileType, null)
            };
        }
    }



}
