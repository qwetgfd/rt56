import { ColumnNameDatatypeName, ColumnNameDatatypeNameForOfflineMode } from './columnNameDatatypeName';
import { ExcelRule, flpConfigurationRuleSet, RuleSetNames } from './DataInsider';
import { DIDatabaseNames } from './DIDatabaseNames';
import { SecurityGroup } from './userDetails';

export interface AdditionalSettings extends RuleSetAdditionalSettings, LandingLayerFormValues {
  flpConfigurationId: string;
  processName: string;

  description: string;

  delimiter: string;
  flexCheckHasHeaders: boolean;
  txtQuoteCharacter: string;
  txtEscapeCharacter: string;
  txtEncoding: string;
  flexCheckOrderByColumnListForDedup: boolean;
  order_by_column_list_name: string; //ex firstname
  order_by_column_list_name_sort_dir: string; //ex desc
  order_by_column_list_for_dedup: string; //ex firstname desc
  is_active: boolean;
  flexCheckSkipEmptyLines: boolean;
  do_not_archive_file: boolean;
  spanish_to_english: boolean;
  roman_numerals_only: boolean;
  ignore_duplicate_rows: boolean;

  csv_column_name_list: string;
  keep_first_row: boolean; //1asc,0desc
  skip_footer_rows: number;
  skip_header_rows: number;

  //flexCheckQuoteCharacter: string;
  flexCheckEscapeCharacter: string;

  tableName: string;
  databaseNameId: number; // selected
  databaseName: string; // selected
  databaseNames: DIDatabaseNames[];
  databaseConfigurationId: string;
  validate_fileschema: boolean;
  drop_history_table: boolean;
  drop_main_table: boolean;

  RegionId: string;
  SubRegionId: string;
  ClientId: string;
  fileType: string;

  key_columns: string;
  convert_datatypes_column_list: string;
  column_name_list: string;
  sender_communication_email: string;

  file_column_mapping: FileColumnMapping[];
  region: string;
  subRegion: string;
  clientName: string;

  mergeData: boolean;
  createHistoryTable: boolean;

  dataSource: number; //flpProcessingServerTypeId
  deltaTableName: string;
  deltaServerNameId: number;
  deltaJobId: string;
  deltaStorageAccountId: string;
  deltaContainerName: string;
  deltaSource: string;
  sourcePath: string;
  securityGroups: SecurityGroup[];
  frmSubmitted: boolean;
  ignoreSheet: boolean;
  newSheet: boolean;
  missingSheet: boolean;

  campaignId : string;
  internalCampaignId : string;

}


export interface RegexItem {
  regex: string;
  description: string;
}


interface LandingLayerFormValues {
  landingLayerFileExtension: number[];
  landingLayerRegex: RegexItem[];
  landingLayerDateformat : number;
  landingLayerTimeformat : number;
  landingLayerPrefix : string;
  landingLayerAcceptedPath : string;
  landingLayerRejectedPath : string;
}

export interface FileColumnMapping {
  fileColumn: string;
  dbColumn: string;
  dataType: string;
  formatId: number;
  dataTypeId: number
}

export interface NewProcessSettings {
  flpConfigurationId: string;
  processName: string;
  processType: number;
  description: string;

  delimiter: string;
  flexCheckHasHeaders: boolean;
  flexCheckSkipEmptyLines: boolean;
  txtQuoteCharacter: string;
  txtEscapeCharacter: string;
  flexCheckOrderByColumnListForDedup: boolean;
  order_by_column_list_name: string; //ex firstname
  order_by_column_list_name_sort_dir: string; //ex desc
  order_by_column_list_for_dedup: string; //ex firstname desc
  is_active: boolean;
  do_not_archive_file: boolean;
  spanish_to_english: boolean;
  ignore_duplicate_rows: boolean;

  csv_column_name_list: string;
  keep_first_row: boolean; //1asc,0desc

  //flexCheckQuoteCharacter: string;
  flexCheckEscapeCharacter: string;
  tableName: string;
  databaseNameId: number; // selected
  databaseName: string; // selected
  databaseNames: DIDatabaseNames[];
  databaseConfigurationId: number;
  is_validate_fileschema_with_target_table: boolean;
  drop_history_table: boolean;
  drop_main_table: boolean;

  RegionId: string;
  SubRegionId: string;
  ClientId: string;
  fileType: string;
  //added below fields for location/storage/blob
  serverLocationId: number;
  baseFolderName: string;
  sourceFolderLocation: string;

  scheduledId: number;
  scheduledDate: string;
  scheduledTime: string;

  blobStorageAccount: string;
  blobContainerName: string;
  blobSourcePath: string;

  search_string_in_file_name: string;
  key_column_list: string;
  column_name_list: string;
  sender_communication_email: string;
}

export interface FilePreviewValues {
  paramChanged: boolean;
  file: any;
  fileName: string;
  recordHeaders: ColumnNameDatatypeNameForOfflineMode[];
  // columnNamesSelected : string;
  // keyColumnNamesSelected : string;
  // columnsForDedupSelected : string;
  delimiter: string;
  flexCheckSkipEmptyLines: boolean;
  hasHeaders: boolean;
  txtQuoteCharacter: string;
  skip_header_rows: number;
  skip_footer_rows: number;
  recordsArrayForDisplay: any[];
  headersRow: string[] | undefined;
  spanish_to_english: boolean;
  roman_numerals_only: boolean;
}
export interface AdditionalSettingsV4_1 {
  delimiter: string;//
  flexCheckHasHeaders: boolean;//
  txtQuoteCharacter: string;//
  flexCheckOrderByColumnListForDedup: boolean;
  order_by_column_list_name: string; //ex firstname
  order_by_column_list_name_sort_dir: string; //ex desc
  order_by_column_list_for_dedup: string;// //ex firstname desc
  flexCheckSkipEmptyLines: boolean;//
  do_not_archive_file: boolean;//
  spanish_to_english: boolean;//
  roman_numerals_only: boolean;//
  ignore_duplicate_rows: boolean;//
  keep_first_row: boolean; //1asc,0desc
  skip_footer_rows: number;//
  skip_header_rows: number;//
  key_columns: string;
  convert_datatypes_column_list: string;
  column_name_list: string;//
  sender_communication_email: string;
  skip_empty_lines: boolean;

}

export interface RuleSetAdditionalSettings {
  ruleSet: ExcelRule[];
  ruleSetNameId: string; // RuleSetNames[]; this is the ruleSetNameId change it 
  ruleSetName: string;
  ruleType: string;
  subRuleType: string;
  patternType: string;
  ruleColumnName: string;
  isCombinationRule: boolean;
  requiredRuleDescription: string;
  uniqueRuleDescription: string;
  formatType: string;
  valueType: string;
  conditionType: string;
  aiPrompt: string;
  fromValue: number;
  toValue: number;
  spName: string;
}


