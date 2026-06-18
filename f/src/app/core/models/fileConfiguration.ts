import { FileColumnMapping, RegexItem } from "./additionalSettings";
import { SecurityGroup } from "./userDetails";

export interface FileConfiguration extends LandingLayer {
    id: number;
    flpConfigurationId: string;
    process_name: string; //Process Name 
    delimiter: string; //CSV Delimiter
    quote_character: string; //CSV Quote Character
    is_header_provided: boolean; //CSV Header -- bool
    skip_empty_lines: boolean;
    skip_rows: number; //CSV Skip Header Rows
    skip_footer_rows: number; //CSV Skip Footer Rows
    skip_header_rows: number; //CSV Skip Footer Rows
    column_name_list: string; //CSV Column Header Name List
    key_column_list: string; //CSV Key Column List
    convert_datatypes_column_list: string; //Convert Datatypes Column List
    sender_communication_email: string; //Sender Communication Email 
    keep_first_row: boolean;

    table_name: string; //Table Name
    database_name: string; //Database Name

    is_create_history_table: boolean; //bool

    drop_main_table: boolean;
    drop_history_table: boolean;
    order_by_column_list_for_dedup: string;

    validate_fileschema: boolean;
    ignore_duplicate_rows: boolean;
    do_not_archive_file: boolean;
    spanish_to_english: boolean;
    roman_numerals_only: boolean;
    search_string_in_file_name: string;
    process_group_name: string;

    notebook_name_to_run: string;
    billing_Client_Name: string;
    description: string;

    is_active: boolean;

    created_by: string; //Created By
    current_timestamp: string; //Created Date
    ccmsid: number;

    region: string;
    subRegion: string;
    clientName: string;

    regionId: string;
    subRegionId: string;
    clientId: string;
    databaseConfigurationId: string;

    mergeData: boolean,
    createHistoryTable: boolean;
    fileColumnMapping: FileColumnMapping[];
    dataSource : number;
    deltaTableName: string;
    deltaServerNameId: number;
    deltaJobId: string;
    deltaStorageAccountId: string;
    deltaContainerName: string;
    deltaSource: string;
    sourcePath : string;
    datalakeStorageAccountPath : string;
    securityGroups : SecurityGroup[];
   
}

export interface ProcessNames {
    id: number;
    //processName :string;
    processNames: string;
}

type AOA = any[][];
export interface KeyValuePair {
    sheetName: string;
    workBook: AOA;
    selected: boolean;
    ignoreSheet: boolean;
    maxRowCount : number;
    newSheet:boolean,
    missingSheet:boolean;
}



export interface FilePreviewNotification {
    sheetName: string;
    // workBook: AOA;
    // selected: boolean;
    noOfDuplicates: number;
    totalCountForDisplay:number; 
    InvalidDataSource: boolean; 
    NewColumnFoundOnSource: boolean;
    MissingColumnFoundOnSource: boolean;
    validate_fileschema:boolean;
}

export interface ProcessSettings {
  flpConfigurationId: string;
  processName: string;
  description: string;
  RegionId: string;
  SubRegionId: string;
  ClientId: string;
  fileType: string;
  region: string;
  subRegion: string;
  clientName: string;
  securityGroups : SecurityGroup[];
  sender_communication_email: string;
  dataSource:number;
  multisheet: boolean;
  sheetReferenceByIndex: boolean;
  sourcePath:string
}

export interface LandingLayer {
    landingLayerPrefix : string;
    landingLayerDateFormatId : number;
    landingLayerTimeFormatId : number;
    landingLayerFileExtension : string;
    regex : string;
}