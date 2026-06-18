using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.DTOs.v1._0.FileLoadingConfigurationProcess
{
    public class FlpFileBaseConfigurationDto
    {
        public string Delimiter { get; set; }
        public string FileNameString { get; set; }
        public string FlpTabName { get; set; }
        public string QuoteCharacter { get; set; }
        public bool IsHeaderProvided { get; set; }
        public int SkipRows { get; set; }
        public int SkipFooterRows { get; set; }
        public string KeyColumnList { get; set; }
        public string ColumnNameList { get; set; }
        public string ConvertDataTypeColumnNameList { get; set; }
        public string ParquetCompression { get; set; }
        public string OrderByColumnListForDedup { get; set; }
        public bool IgnoreDuplicateRows { get; set; }
        public bool KeepFirstRow { get; set; }
        public bool DoNotArchiveFile { get; set; }
        public bool SpanishToEnglish { get; set; }
        public bool OrdinalToRoman { get; set; }
        public bool SkipEmptyLines { get; set; }
    }
}
