export interface FlpConvertParquetRequestDto {
    flpConfigurationId : string;
    processName : string;
    blobClients : BlobClients
    //onPremFileLocation : OnPremFileLocation
}

export interface BlobClients {
    uploadedId : string;
    uri : string;
    accountName : string;
    blobContainerName : string;
    name : string;
    canGenerateSasUri : boolean;
    
}

export interface OnPremFileLocation {
    UploadedId : string;
    FileUrl : string;
}