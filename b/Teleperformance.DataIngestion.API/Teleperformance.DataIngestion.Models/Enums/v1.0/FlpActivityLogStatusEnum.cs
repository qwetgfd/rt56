using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Enums.v1._0
{
    public enum FlpActivityLogStatusEnum
    {
        FileMovedToTempStorage = 1,
        DeletedFileFromMainLocation = 2,
        ConversionToParqetFileMovedToParquetLocation = 3,
        FileSchemaValidated = 4,
        DataInsertedToBronzeTable = 5,
        FileDeletedFromTemp = 6,
        ParquetFileArchived = 7,
        ParquetFileFailed = 8,//Not in use
         // Error=9,
        DroppedMainTable = 10,
        DeletedParquetLocation = 11,
        PlacedParquetFile =12,
        DatabricksJobProcess=13,
        WritingDataBricksCatalog = 14,
        UIValidation=15,
        DropedSilverTable =16,
        SilverTableDataInserted = 17,
        BEValidation = 18,
        DropedGoldTable =19,
        GoldTableDataInserted=20,
        //LandingLayerFileValidation=21,
        //FilePacedInLandingFolder=22,
        //FilePlacedInRejectedFolder=23
        FileProcessCreated = 21,
        ValidationFileForExtension = 22,
        ValidationFileForRegex = 23,
        MovedFileToLandingLayer = 24,
        MovedFileToRejectedFolder = 25,
        DeletedFilesFromFolder = 26,
        ProcessCompleted = 27
    }
}
