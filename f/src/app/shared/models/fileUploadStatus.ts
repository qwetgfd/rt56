export interface FlpUploadedFileStatusResponse {
  clientId: number;
  clientName: string;
  fileConfigurationStatusList: FlpConfigurationResponse[];
}
export interface FlpConfigurationResponse {
  flpConfigurationID: string;
  flpConfigurationName: string;
  uploadedFiles: UploadedFileStatus[];
}

export interface UploadedFileStatus {
  uploadFileId: string;
  uploadFileName: string;
  fileCreationDate: Date;
  fileProcessStatusId: number;
  fileProcessstatusName: string;
  databaseName: string;
  tableName: string;
  totalRecords: number;
  processedRecords: number;
  duplicateRecords: number;
  completionTime: string;
  durationInSeconds: number;
  tabName: string;
  fileProcessingServerTypeId : number;
}

export interface FileUploadDetailedStatus {
  flpConfigurationID: string;
  uploadFileID: string;
  uploadFile: string;
  databaseName: string;
  tableName: string;
  totalRecords: number;
  processedRecords: number;
  duplicateRecords: number;
  description: string;
  tabName: string;
  blobName: string;
  statusCode: string;
  fileStatus: FileStatus[];
}
export interface FileStatus {
  statusName: string;
  status: string;
  processStatusId: number;
  statusStartTime: string;
  statusCompletionTime: string;
  durationInSeconds: number;
  errorMessage: string;
  statusMessage : string;
}

export interface FileConfigAndStatus {
  id: number;
  flpConfigurationID: string;
  flpConfigurationName: string;
  uploadFileId: string;
  uploadedFiles: UploadedFileStatus[];
  fileUploadDetailedStatus: FileUploadDetailedStatus;
}

export interface Duration {
  startTime: Date;
  endTime: Date;
  totalDuration: number;
  uploadFileId: string;
  databaseName: string;
  tableName: string;
  totalRecords: number;
  processedRecords: number;
  duplicateRecords: number;
  description: string;
  tabName: string;
}