using Parquet;
using Parquet.Schema;
using System;
using System.Collections.Generic;
using Teleperformance.DataIngestion.Models.Entities.v1._0.FileConfiguration;
using Teleperformance.DataIngestion.Models.Enums.v1._0;
using Teleperformance.DataIngestion.Models.Helpers;

namespace Teleperformance.DataIngestion.DataAccess.Helpers
{
   
    public class ParquetSchemaHelper
    {
        public static ParquetSchema CreateSchema(Dictionary<string, DataTypeDetails> columnDataTypeList)
        {
            var fields = new List<Field>();

            foreach (var entry in columnDataTypeList)
            {
                string columnName = entry.Key;
                DataTypeDetails typeDetails = entry.Value;
              
                Field field;
                if ((string.Compare(typeDetails.DataType, DataTypeOptionsEnum.INT.GetDescription(), false) == 0))
                {
                    field = new DataField<int?>(columnName);
                }
                else if ((string.Compare(typeDetails.DataType, DataTypeOptionsEnum.STRING.GetDescription(), false) == 0))
                {
                    field = new DataField<string?>(columnName);
                }
                else if ((string.Compare(typeDetails.DataType, DataTypeOptionsEnum.BOOL.GetDescription(), false) == 0))
                {
                    field = new DataField<bool?>(columnName);
                }
                else if ((string.Compare(typeDetails.DataType, DataTypeOptionsEnum.FLOAT.GetDescription(), false) == 0))
                {
                    field = new DataField<float?>(columnName);
                }
                else if ((string.Compare(typeDetails.DataType, DataTypeOptionsEnum.LONG.GetDescription(), false) == 0))
                {
                    field = new DataField<long?>(columnName);
                }
                else if ((string.Compare(typeDetails.DataType, DataTypeOptionsEnum.DOUBLE.GetDescription(), false) == 0))
                {
                    field = new DataField<double?>(columnName);
                }
                else if ((string.Compare(typeDetails.DataType, DataTypeOptionsEnum.DATE.GetDescription(), false) == 0))
                {
                    field = new DataField<DateOnly?>(columnName);
                }
                else if ((string.Compare(typeDetails.DataType, DataTypeOptionsEnum.DATETIME.GetDescription(), false) == 0))
                {
                    field = new DataField<DateTime?>(columnName);
                }
                else if ((string.Compare(typeDetails.DataType, DataTypeOptionsEnum.TIME.GetDescription(), false) == 0))
                {
                    field = new DataField<TimeOnly?>(columnName);
                }
                else
                {
                    // Default to string if the type is unknown
                    field = new DataField<string?>(columnName);
                }

                fields.Add(field);
            }

            return new ParquetSchema(fields);
        }

        public static List<string> GetTimeFormats()
        {
            string[] timeFormats =
            {
                "h tt",           // e.g., 2 PM
                "h:mm tt",        // e.g., 2:30 PM
                "hh tt",          // e.g., 02 PM
                "hh:mm tt",       // e.g., 02:30 PM
                "H:mm",           // e.g., 2:30 (24-hour format, no seconds)
                "HH:mm",          // e.g., 14:30 (24-hour format, no seconds)
                "HH:mm:ss",       // e.g., 14:30:45 (24-hour format with seconds)
                "h:mm:ss tt",     // e.g., 2:30:45 PM
                "hh:mm:ss tt",    // e.g., 02:30:45 PM
                "H:mm:ss",        // e.g., 2:30:45 (24-hour format with seconds)
                "h:m tt",         // e.g., 2:5 PM (single-digit minutes without leading zero)
                "h:m:s tt",       // e.g., 2:5:3 PM (single-digit minutes and seconds without leading zero)
                "hh:mm",          // e.g., 02:30
                "HHmm",           // e.g., 1430 (24-hour format without colon)
                "Hmm",            // e.g., 230 (24-hour single-digit hour without colon)
                "hhmm tt",        // e.g., 0230 PM
                "hh:mm:ss",       // e.g., 02:30:45 (12-hour format with seconds)
                 // Additional formats
                "H:mm:ss.fff",    // e.g., 14:30:45.123 (with milliseconds)
                "hh:mm:ss.fff tt",// e.g., 02:30:45.123 PM (12-hour format with milliseconds)
                "HH:mm:ss.fff",   // e.g., 14:30:45.123 (24-hour format with milliseconds)
                "h:mm:ss",        // e.g., 2:30:45 (12-hour format without AM/PM)
                "hh:mm tt",       // e.g., 02:30 AM (with leading zero and AM/PM)
                "H:mm:ss tt",     // e.g., 2:30:45 AM (24-hour without leading zero, with AM/PM)
                "h:mm:ss.ff tt",  // e.g., 2:30:45.12 PM (2 decimal milliseconds)
                "HHmmss",         // e.g., 143045 (compact 24-hour with seconds)
                "h:mm:ss.ff",     // e.g., 2:30:45.12 (12-hour format with 2 decimal milliseconds)
                "h:m:s",          // e.g., 2:3:5 (short format without leading zeros)
                "hh:mm:ss tt zzz",// e.g., 02:30:45 PM -07:00 (time with timezone offset)
                "HH:mm zzz"       // e.g., 14:30 -07:00 (24-hour with timezone offset)
            };
            return timeFormats.ToList();

        }
    }

}
