export interface ProcessListRequest {
  regionId: number;
  subRegionId: string;
  clientId: number;
  fromDate: Date;
  toDate: Date;
}

export interface ProcessListResponse {
  processRowCount: number;
  newProcessCount: number;
  currentMonth: number;
  currentMonthName: string;
}

export interface FileListResponse {
  totalUploadedFiles: number;
  successCount: number;
  failureCount: number;
  newFileProcessCount: number;
  currentMonth: number;
  currentMonthName: string;
}

export interface ClientListResponse {
  totalClients: number;
  activeClients: number;
  currentMonth: number;
  currentMonthName: string;
}

export interface DiUtilization {
  totalFileCount: number;
  month: number;
  monthName: string;
  clientId: number;
  clientName: string;
}
export interface DiUploads {
  totalUploadedFiles: number;
  processTypeId: number;
  processType: string;
}

export interface RealTimeProcessing {
  configurationId: string;
  processName: string;
  uploadFileId: string;
  fileName: string;
  creationDateTime:Date;
  processStatusId: number;
  processStatusName: string;
}

export interface UtilizationRegion{
  regionId: number;
  regionName: string;
  totalFileCount: number;
}
