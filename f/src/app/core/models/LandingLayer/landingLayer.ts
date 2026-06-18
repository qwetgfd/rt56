export interface FileNameExtension {
    id : number;
    fileExtension : string;
}

export interface SelectedFiles {
    File : File;
    status : string;
}

export interface Prefixes {
    id : number;
    prefixName : string;
}

export interface LandingLayerConfiguration {
    noOfAllowedFilesToUpload : number;
    totalFileSize : number;
}

export interface LandingLayerInsertConfigurationRequest{
    processName : string;
    flpConfigurationId : string;
    uploadFileId : string;
    loggedInUser : string; //full name
    userName : string; //username
    files : File[];
}