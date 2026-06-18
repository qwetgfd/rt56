using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Enums.v1._0
{
    public enum DataTypeOptionsEnum
    {
        [Description("datetime")]
        DATETIME,
        [Description("date")]
        DATE,
        [Description("time")]
        TIME,
        [Description("bool")]
        BOOL,
        [Description("int")]
        INT,
        [Description("long")]
        LONG,
        [Description("float")]
        FLOAT,
        [Description("double")]
        DOUBLE,
        [Description("string")]
        STRING

    }
}
