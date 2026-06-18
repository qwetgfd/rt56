import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { APIResponse } from '../../models/apiResponse';
import { DBView, EIB, EIBConfigurationDetails, EIBCountry, EIBGenerationStatus, EIBListRequest, EIBListResponse, ProfilingSPNames } from '../../models/EIB/dbView';
import { DataAssistsRequest } from '../../models/DataInsider';
import { catchError, throwError } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class EibService {
  private baseUrl = environment.apiEndpoint;
  constructor(private _http: HttpClient) { }

  private setHttpsRequestHeaders(apiVersion: string): HttpHeaders {
    let objHttpHeaders = new HttpHeaders();
    objHttpHeaders = objHttpHeaders.append(
      'Authorization',
      `Bearer ${localStorage.getItem('DIApiToken')}`
    );
    objHttpHeaders = objHttpHeaders.set(
      'Content-Type',
      'application/json; charset=utf-8'
    );
    objHttpHeaders = objHttpHeaders.set('Accept', 'application/json');
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-version', apiVersion);
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);
    return objHttpHeaders;
  }

  GetAllViews() {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    return this._http.get<APIResponse<DBView[]>>(
      this.baseUrl + 'api/EIB/GetAllViews',
      { headers: headers }
    );
  }

  GetAllProfilingSPNames() {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    return this._http.get<APIResponse<ProfilingSPNames[]>>(
      this.baseUrl + 'api/EIB/GetAllProfilingSP',
      { headers: headers }
    );
  }

  GetCurrentStatusOfPDMSP(procedureNameId : number){
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    return this._http.get<APIResponse<ProfilingSPNames>>(
      this.baseUrl + `api/EIB/GetCurrentStatusOfPDMSP?procedureNameId=${procedureNameId}`,
      { headers: headers }
    );
  }

  RegisterPDMRProfilingSPRun(procedureNameId : number, processedBy : string){
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    //todo: edit the return type

    return this._http.post<APIResponse<string>>(
          this.baseUrl + 'api/EIB/RegisterPDMRProfilingSPRun',
          {procedureNameId : procedureNameId, processedBy : processedBy},
          {headers : headers}
        );
  }


  GetAllCountries() {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    return this._http.get<APIResponse<EIBCountry[]>>(
      this.baseUrl + 'api/EIB/GetEIBCountries',
      { headers: headers }
    );
  }

  GetAllEIBs(data: EIBListRequest) {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    const params = new HttpParams({ fromObject: data as any });
    return this._http.get<APIResponse<EIBListResponse>>(
      this.baseUrl + 'api/EIB/GetAllEIB',
      { headers, params }
    );
  }  

  GetEIBRequiredBPKeyword() {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    //const params = new HttpParams({fromObject : data as any});
    return this._http.get<APIResponse<string>>(
      this.baseUrl + 'api/EIB/GetEIBRequiredBPKeyword',
      { headers }
    );
  }

   downloadFile(sasUrl: string) {
     let theaders= new HttpHeaders({
         'Authorization': `Bearer ${localStorage.getItem('DIApiToken')}`,
         'x-tpdi-api-version': '4.1',
         'Content-Type': 'application/json',
         'x-tpdi-api-sg': `${sessionStorage.getItem("GUID")}`,
       });
     return this._http
         .get(
          `${this.baseUrl}api/Blob/downloadFile2?sasUrl=${encodeURIComponent(sasUrl)}`, {
           headers: theaders,
           responseType: 'blob',
         })
         .pipe(
           catchError((error) => {
             console.error('API Error:', error);
             return throwError(
               () => new Error('Error occurred while updating signature')
             );
           })
         );
     // return this._http.get(`${this.baseUrl}api/Blob/DownloadFile`, { responseType: 'blob' });
   }

  GetEIBByEIBId(eibId: string) {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    //const params = new HttpParams({fromObject : data as any});
    return this._http.get<APIResponse<EIBConfigurationDetails>>(
      this.baseUrl + `api/EIB/GetEIBByEIBId?eibId=${eibId}`,
      { headers: headers }
    );
  }

  insertEIBConfigurationDetails(request: EIBConfigurationDetails, eibFile: File) {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1').delete('Content-Type');
    const formData = new FormData();
    var isUpdate = (request.configurationId === '' || request.configurationId === null);
    if (isUpdate) {
      formData.append('EIBFile', eibFile, eibFile.name);
    }
    formData.append('RequestData', new Blob([JSON.stringify(request)], { type: 'application/json' }));
    if (sessionStorage.getItem('username'))
      formData.append(
        'UserName',
        sessionStorage.getItem('username')
      );

    if (sessionStorage.getItem('upn').split('@')[0])
      formData.append(
        'LoggedInUser',
        sessionStorage.getItem('upn').split('@')[0]
      );
    return this._http.post<APIResponse<boolean>>(
      this.baseUrl + `api/EIB/InsertEIBDetails?e=${isUpdate}`,
      formData,
      { headers }
    );
  }

  CheckActiveEIBConfiguration(EIBName: string) {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    return this._http.get<APIResponse<boolean>>(
      this.baseUrl + `api/EIB/CheckActiveEIBConfiguration?EIBName=${EIBName}`,
      { headers: headers }
    );
  }

  //queue the eibId for Queued
  generateEIB(EIBId: string) {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    return this._http.post<APIResponse<boolean>>(
      this.baseUrl + `api/EIB/GenerateEIB?EIBId=${EIBId}`,
      null,
      { headers: headers }
    );
  }

  getEIBGenerationStatus(monday: string, sunday: string) {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    return this._http.get<APIResponse<EIBGenerationStatus[]>>(
      this.baseUrl + `api/EIB/getEIBGenerationStatus?generationStartDateTime=${monday}&generationEndDateTime=${sunday}`,
      { headers: headers }
    );
  }

  SendPDMProflingSatusBySPId(procedureId : number, runId : string,connectionId:string, lastSeendId : number = 0){
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    return this._http.get<APIResponse<ProfilingSPNames>>(
      this.baseUrl + `api/EIB/SendPDMProflingSatusBySPId?procedureNameId=${procedureId}&runId=${runId}&connectionId=${connectionId}&lastSeendId=${lastSeendId}`,
      { headers: headers }
    );
  }





}
