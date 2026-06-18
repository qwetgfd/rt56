using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Enums.v3._0
{
    public enum DatatypeNamesEnum
    {
        BOOL=17,
        DATETIME=18,
        DOUBLE=19,
        FLOAT=20,
        INT=21,
        LONG=22,
        STRING=23,
        DATE=24,
        TIME=25
    }

    public enum FileProcessingServerType
    {
        SQLServer = 1,
        DataLake = 2,
        LandingLayer=3
    }
}
