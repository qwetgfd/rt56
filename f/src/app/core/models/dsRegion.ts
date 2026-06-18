export interface DsConfig {
  consumerApplicationId: string;
  sourceDataObjId: string;
}
export interface DsApiRequest {
  consumerApplicationId: string;
  sourceDataObjId: string;
  pageNo: number;
  pageSize: number;
  filter: string;
}
export interface DsFilter {
  filterName: string;
  filterValue: string;
  operatorType: string;
}

export interface DsApiResponse {
  responseCode: number;
  responseMessage: string[];
  result: Result;
}
export interface Result {
  pagination: Pagination;
  data: RegionSubRegionClient[];
}
export interface Pagination {
  pageNo: number;
  pageSize: number;
  pageCount: number;
  recordsCount: number;
}
export interface RegionSubRegionClient {
  client_ident: number;
  client_abbr_name: string;
  client_full_name: string;
  mainregion_ident: number;
  mainregion: string;
  region_ident: number;
  region: string;
  subsubregion: string;
  subsubregion_code: string;
  subRegion_ident: number;
}

export interface DdlData {
  id: number;
  name: string;
}
export interface SubRegion {
  id: string;
  name: string;
}

export interface DsRegionResponseDto{
  region_ident : number;
  region : string;
}

export interface DsRegionSubRegionRequestDto{
  regionId : number;
  
}

export interface DsSubRegionResponseDto{
  subsubregion_code : string;
  subsubregion : string;
}

export interface DsClientResponseDto{
  client_ident : number;
  client_full_name : string;
}
