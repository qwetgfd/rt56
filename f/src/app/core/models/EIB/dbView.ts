import { ProcessConfigurationRequest } from "../processConfigurationlist";
import { ProcessedFileRequest } from "../processedFileList";

export interface DBView {
    dbViewId: string;
    businessProcessNameId : string;
    viewId: number;
    viewName: string;
    columnCount: number;
    fromColumn: string;
    toColumn: string;
}

export interface ProfilingSPNames {
    id: number;
    runId : string;
    sPName : string;
    description: string;  
    createdBy : string;
    insertedAt : string;  
    latestStatus : string;
    profilingRunHistoryLog : ProfilingRunLogHistory[];
}

export interface ProfilingRunLog {
    id : number;
    runId : string;
    rule : string;
    reason : string;
    status : string;
    insertedAt : string;
    endedAt : string;
    runningStatus : string;
    color : string;
    isNew : boolean;
    
    
}   

export interface ProfilingRunLogHistory {
    runId : string;
    processedBy : string;
    startedAt : string;
    endedAt : string;
    status : string;
}

export interface EIBListResponse {
    response: EIB[],
    totalCount: number
}

export interface EIB {
    eibId: string;
    eibName: string;
    description: string;
    createdBy: string;
    creationDateTime: string;
    modifiedDateTime: string;
    generationStartDateTime: string;
    generationEndDateTime: string;
    updatedBy: string;
    mappedcount: number;
    status: number;
    fileUrl: string;
    hasActiveFileURL : boolean;
    errorMessage : string;
}

export interface BusinessProcessDetails {
    row: number;
    processName: string;
    processNameId: string;
    fieldCount: number;
    isRequired: boolean;
    disabled: boolean; //businessprocess has no sheetname
    isMapped: boolean;
    category : string;
}

export interface MappingDetails {
    id : number;
    processName: string;
    dbView: DBView[];
    isDeleted : boolean;
}

export interface EIBConfigurationDetails {
    configurationId: string;
    EIBId: string;
    eibName: string;
    description: string;
    noOfBusinessProcess: number;
    updatedDateTime: string;
    createdBy: string;
    updatedBy: string;
    isActive: boolean;
    businessProcessNames: BusinessProcessNames[];
    businessProcessDBViewMapping: BusinessProcessDBViewMapping[];    
    countryId : number;
    countryName : string;
}

export interface EIBConfigurationDetailsWithFileUrl {
    configurationId: string;
    EIBId: string;
    eibName: string;
    description: string;
    noOfBusinessProcess: number;
    updatedDateTime: string;
    createdBy: string;
    updatedBy: string;
    isActive: boolean;
    businessProcessNames: BusinessProcessNames[];
    businessProcessDBViewMapping: BusinessProcessDBViewMapping[];
    businessProcessFileUrlMapping : BusinessProcessFileUrlMapping[];
    countryId : number;
    countryName : string;
}

interface BusinessProcessNames {
    businessProcessNameId: string;
    EIBId: string;
    businessProcessName: string;
    fieldCount: number;
    isRequired: boolean;
    isDisabled: boolean;
    creationDateTime: string;
    isActive: boolean;
    createdBy: string;
    updatedBy: string;
}

export interface BusinessProcessFileUrlMapping {
    EIBId : string;
    businessProcessNameId: string;
    fileUrl : string;
    filename : string;
}
interface BusinessProcessDBViewMapping {
    bpnViewId: string; //pk
    businessProcessName: string;
    businessProcessNameId: string;
    viewNameId: number;
    viewName: string;
    columnCount: number;
    fromColumn: string;
    toColumn: string;
    isActive: boolean;
    updatedBy: string;
}

export interface EIBGenerationStatus {
    eibId: string;
    status: string;
    fileURL: string;  
    hasActiveFileURL : boolean;  
    errorMessage : string;
    generationStartDateTime : string;
}

export interface EIBListRequest {
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

export interface EIBCountry {
    id : number;
    countryName : string;
    description : string; 
}