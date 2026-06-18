export interface FlpProcessConfiguration {
  id: number;
  configurationId: string;
  processName: string;
  senderEmail: string;
  createdBy: string;
  loginId: string;
  processTypeId: string;
  regionId: string;
  subRegionId: string;
  createdDate: string;
  subRegionName: string;
  regionName: string;
  processTypeName: string;
  clientName: string;
  databaseName: string;
  tableName: string;
  ingestionTypeId: string;
  ingestionType: string;
  updatedOn: string;
  updatedBy: string;
  tabName:string;
  multiSheet : boolean;
  rowNo:number;
}

export interface FlpProcessConfigurationResponse {
  response: FlpProcessConfiguration[],
  totalCount: number
}

export interface ProcessConfigurationRequest {
  pageNumber: number;
  pageSize: number;
  // processName: string | null;
  createdBy: string | null;
  // creationDate: string | null;
  fromDate: string | null;
  toDate: string | null;
  totalCount: number;
  searchOnColumn: string | null;
  searchValue: string | null;
  isActive : boolean;
}

export interface ProcessConfigListSearchDropdown{
  databaseColumnName: string
  databaseLabel: string
}

export interface UpdateActiveStatusByFlpConfigurationIdRequest {
  flpConfigurationIds : string;
  userName : string;
  created_by : string;
  activeStatus : boolean;
}

export interface UpdateActiveStatusByFlpConfigurationIdResponse {
  RecordId : number;
  Result : string;
  Message : string;
}

export interface ConvertToXLSXDto {
  fileData: string;
  rowCount: number
}