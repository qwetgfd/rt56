export interface FlpProcessedFile {
  configurationId: string;
  fileName: string;
  processName: string;
  createdDate: string;
  statusName: string;
  regionName : string;
  subRegionName : string;
  clientName : string;
  databaseName : string;
  tableName: string;
  createdBy: string;
  loginId: string;
  totalRecords : number;
  processedRecords : number;
  duplicateRecords : number;
  startTime : string;
  endTime : string;
  fileId: string
  description : string;
  tabName : string;
}

export interface FlpProcessedFileListResponse {
  response: FlpProcessedFile[],
  totalCount: number
}

export interface ProcessedFileRequest {
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
}