using NPOI.SS.UserModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Helpers
{
    public class XLSBWorkbookModel
    {
        public IWorkbook workbook { get; set; }
        public int emptyRowCount { get; set; }
    }
}
