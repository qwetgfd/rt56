export interface ColumnNameDatatypeName {
    // isEditable :boolean;
    index : number;
    ColumnName: string;
    DbColumnName : string;
    DatatypeName: string;
    //DataTypeId : number;
    willInclude : boolean;
    newColumn : boolean;
    ColumnKey : boolean;
    missingColumn : boolean;
    invalidDataType  :boolean;
    willAddNewColumn : boolean;
    invalidColumnName : boolean;
    columnForDedeup : boolean;
    dateTimeFormatId : number;
    isDuplicateColumn : boolean;
    useMultipleSheets : boolean
}

export interface SheetValidationData {
  data: ColumnNameDatatypeName[];
  validateSchema: boolean;
}




export interface ColumnNameDatatypeNameForOfflineMode {
    // isEditable :boolean;
    index : number;
    ColumnName: string;
    DbColumnName : string;
    OgColumnName: string;
    OgDbColumnName : string;
    DatatypeName: string;
    //DataTypeId : number;
    willInclude : boolean;
    newColumn : boolean;
    ColumnKey : boolean;
    missingColumn : boolean;
    invalidDataType  :boolean;
    willAddNewColumn : boolean;
    invalidColumnName : boolean;
    columnForDedeup : boolean;
    dateTimeFormatId : number;
    isDuplicateColumn : boolean;
    useMultipleSheets : boolean;
}


