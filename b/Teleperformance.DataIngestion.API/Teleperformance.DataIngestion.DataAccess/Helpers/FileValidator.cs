using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Teleperformance.DataIngestion.Models.Enums;

namespace Teleperformance.DataIngestion.DataAccess.Helpers
{
    public abstract class FileValidator
    {

        public  static bool IsValidExcelFile(string blobName)
        {
            string fileExtension = Path.GetExtension(blobName).ToLowerInvariant();

            // Check against allowed extensions from the enum
            var validExtensions = new[]
            {
                ExcelFileType.XLS.GetExtension(),
                ExcelFileType.XLSX.GetExtension(),
                ExcelFileType.XLSB.GetExtension()
            };

            return validExtensions.Contains(fileExtension);
        }

    }
}
