import { Data } from "@angular/router";
import { AdditionalSettings } from "./additionalSettings";
import { ColumnNameDatatypeName } from "./columnNameDatatypeName";
import {  ProcessSettings } from "./fileConfiguration";
import { DatabaseSettings } from "./databaseSettings";
import { ExcelRule, flpConfigurationRuleSet } from "./DataInsider";

export interface LocalFileDataSourceType {    
    fileInput : File;
    configuration : string;
    additionalSettings : AdditionalSettings | null;
    columnNameDatatypeNames : ColumnNameDatatypeName[];
}



export interface FileSettings { 
    tabName:string;  
    ignoreSheet: boolean;
    additionalSettings : AdditionalSettings | null;
    columnNameDatatypeNames : ColumnNameDatatypeName[]; 
    databaseSettings : DatabaseSettings | null;
    ruleSet : ExcelRule[];
}

// export interface LocalFileDataSourceType {    
//     fileInput : File;
//     configuration : string;
//     processSetting : ProcessSetting;
//     fileSettings : FileSettings[];
// }
 
 
export interface LocalFileDataSourceTypev4_1 {    
    fileInput : File;
    processSettings : ProcessSettings;
    fileSettings : FileSettings[];    
}