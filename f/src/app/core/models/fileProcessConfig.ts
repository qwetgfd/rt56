import { FileColumnMapping, RegexItem } from "./additionalSettings";
import { SecurityGroup } from "./userDetails";
import { ExcelRule, PayLoad } from "./DataInsider";

export interface FileProcessConfig {
  flpConfigurationId: string;
  processName: string;
  locationTypeId: number;
  senderCommunicationEmail: string;
  createdBy: string;
  userName: string;
  description: string;
  processTypeId: number;
  regionId: string;
  subRegionId: string;
  clientId: string;
  fileType: number; //= 0
  searchStringInFileName: string;
  serverLocationId: number;
  baseFolderName: string;
  sourceFolderLocation: string;
  scheduledId: number;
  scheduleValue: string;
  scheduledDate?: string;
  scheduledTime?: string;
  scheduledEndDate?: string;
  scheduledEndTime?: string;
  blobStorageAccount: string;
  blobContainerName: string;
  blobSourcePath: string;
  configurationId: string;
  hourFrequency: number;
  updateSchedular: boolean;
  weekDays: number[];
  region: string;
  subRegion: string;
  clientName: string;
  dataSource: number; //flpProcessingServerTypeId
  deltaStorageAccountId: string;
  deltaContainerName: string;
  deltaSource: string;

  campaignId : string;
  internalCampaignId : string;

  fileConfigurations: FlpFileConfigList[];
  configurationTableMappings: ConfigurationTableMapping[];
  securityGroups: SecurityGroup[];

  // #region Sharepoint Workspace - AY
  sharePointApplicationId?: string;
  sharePointApplicationSiteId?: string;
  sharePointLibraryName?: string;
  sharePointFolderPath?: string;
  // #endregion
}

export interface FlpFileConfigList {
  flpConfigurationId: string;
  delimiter: string;
  quoteCharacter: string;
  isHeaderProvided: boolean;
  skipRows: number;
  skipFooterRows: number;
  keyColumnList: string;
  columnNameList: string;
  convertDatatypesColumnList: string;
  dedup: string;
  ignoreDuplicateRows: boolean;
  doNotArchiveFile: boolean;
  keepFirstRow: boolean;

  db_file_column_name_list: string;
  spanishToEnglish: boolean;
  romanNumeralsOnly: boolean;
  skipEmptyLines: boolean;
  tabName: string;
  landingLayerFileExtension : string;
  landingLayerRegex : RegexItem[];
  landingLayerPrefix: string;
  dateFormatId: string;
  timeFormatId: string;
}

export interface LandingLayerDestinationSettings{
  landingLayerAcceptedPath : string;
  landingLayerRejectedPath : string;
}

export interface ConfigurationTableMapping extends LandingLayerDestinationSettings {
  flpConfigurationId: string;
  tableName: string;
  databaseConfigurationId: number;
  databaseName?: string;
  dropMainTable: boolean;
  validateFileSchema: boolean;
  dropHistoryTable: boolean;
  mergeData: boolean;
  createHistoryTable: boolean;
  deltaJobId: string;
  tabName: string;
  deltaSource: string;


}





export interface ServerDetail {
  fileServerId: number;
  serverName: string;
}
export interface StorageAccount {
  storageAccountId: number;
  storageAccountName: string;
  containerName: string;
  configurationProcessType : number;
}

export interface FileConfigurationDetails {
  flpConfigurationId: string;
  process_name: string;
  locationTypeId: number;
  locationName: string;
  processType: string;
  sender_communication_email: string;
  userName: string;
  description: string;
  processTypeId: number;
  regionId: number;
  regionName: string;
  subRegionId: string;
  subRegionName: string;
  clientId: number;
  clientName: string;
  search_string_in_file_name: string;
  databaseName: string;
  tableName: string;
  sourcePath: string;
  sharedServerName: string;
  folderName: string;
  destinationPath: string;
  storageAccountId: number;
  storageContainerName: string;
  fileServerId: number;
  sharedLocationServerInfoId: string;
  blobStorageAccountId: number;
  blobStorageAccountName: string;
  blobStorageContainerName: string;
  scheduleTypeId: string;
  schedulerType: string;
  scheduleValue: string;
  scheduleStartDate: Date;
  scheduleStartTime: string;
  scheduleEndDate?: string;
  scheduleEndTime?: string;
  hourFrequency: number;
  weekDays: number[];
  dataSource: number; //flpProcessingServerTypeId
  deltaStorageAccountId: string;
  deltaStorageAccountName: string;
  deltaContainerName: string;
  deltaSource: string;
  campaignId : string;
  internalCampaignId : string;
  // #region Sharepoint Workspace - AY
  sharePointApplicationId?: string;
  sharePointApplicationSiteId?: string;
  sharePointLibraryName?: string;
  sharePointFolderPath?: string;
  sharePointApplicationName?: string;
  sharePointSiteName?: string;
  // #endregion
  fileConfigurationDetails: FlpFileConfigList[];
  configurationTableMappingDetails: ConfigurationTableMapping[];
  customSchedulerDetails: CustomSchedulerDetail[];
  fileColumnMapping: FileColumnMapping[];
  configurationSecurityGroupMappingList: SecurityGroup[];
  flpRuleSet: ExcelRule[]
}
export class CustomSchedulerDetail {
  frequencyHoursId: number;
  weekDaysId: number;
}

export interface SchedulerType {
  schedulerTypeId: number;
  schedulerType: string;
}

export interface WeekDayName {
  id: number;
  weekDayName: string;
}
export interface FrequencyHour {
  id: number;
  frequencyHour: string;
}

export interface LogUploadedFileRequest {
  fileName: string;
  fileSize: number;
  uploadedDateTime: string;
  uploadedBy: string;
}