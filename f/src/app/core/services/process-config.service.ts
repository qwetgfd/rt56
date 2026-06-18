import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { APIResponse } from '../../shared/models/apiResponse';
import { ProcessType } from '../../shared/models/newProcess';
import { FrequencyHour, SchedulerType, ServerDetail, StorageAccount, WeekDayName } from '../models/fileProcessConfig';
import { DsConfig } from '../models/dsRegion';
import { catchError, map, Observable, ObservedValueOf, of, shareReplay, tap } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class ProcessConfigService {
  private baseUrl = environment.apiEndpoint;
  constructor(private _http: HttpClient) { }
  httpHeader() {
    let headers = new HttpHeaders({
      'Authorization': `Bearer ${localStorage.getItem('DIApiToken')}`,
      'x-tpdi-api-version': '1.0',
      'Content-Type': 'application/json',
      'x-tpdi-api-sg': `${sessionStorage.getItem("GUID")}`,
    });
    return { headers: headers };
  }

  httpHeader2() {
    let headers = new HttpHeaders({
      'Authorization': `Bearer ${localStorage.getItem('DIApiToken')}`,
      'x-tpdi-api-version': '2.0',
      'Content-Type': 'application/json',
      'x-tpdi-api-sg': `${sessionStorage.getItem("GUID")}`,
    })

    return { headers: headers }
  };


  setHttpsRequestHeaders(apiVersion: string): HttpHeaders {
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

  getProcessType() {
    return this._http.get<APIResponse<ProcessType[]>>(
      this.baseUrl + 'api/ProcessConfiguration/GetProcessType',
      this.httpHeader()
    );
  }

  getProcessType$(): Observable<ProcessType[]> {
    return this.getProcessType() // Observable<APIResponse<ProcessType[]>>
      .pipe(
        map((resp: APIResponse<ProcessType[]>) => {
          const ok = resp && resp.responseCode === 200 && resp.responseMessage?.[0] === 'Success';
          return ok && Array.isArray(resp.result) ? resp.result : [];
        }),
        catchError(err => {
          console.error('Error fetching process types', err);
          return of([] as ProcessType[]);
        }),
        shareReplay(1)
      );
  }

  getServerDetails() {
    return this._http.get<APIResponse<ServerDetail[]>>(
      this.baseUrl + 'api/ProcessConfiguration/GetFileServerDetails',
      this.httpHeader()
    );
  }

  getServerDetails$(): Observable<ServerDetail[]> {
    return this.getServerDetails() // Observable<APIResponse<ServerDetail[]>>
      .pipe(
        map((resp: APIResponse<ServerDetail[]>) => {
          const ok = resp && resp.responseCode === 200 && resp.responseMessage?.[0] === 'Success';
          return ok && Array.isArray(resp.result) ? resp.result : [];
        }),
        catchError(err => {
          console.error('Error fetching server details', err);
          return of([] as ServerDetail[]);
        }),
        shareReplay(1)
      );
  }


  getStorageAccountDetails(fileProcessingServerTypeId: number = 1) {
    let params = new HttpParams();
    //params = params.append('fileProcessingServerTypeId', fileProcessingServerTypeId);

    let headers = new HttpHeaders({
      'Authorization': `Bearer ${localStorage.getItem('DIApiToken')}`,
      'x-tpdi-api-version': '1.0',
      'Content-Type': 'application/json',
      'x-tpdi-api-sg': `${sessionStorage.getItem("GUID")}`,
    });

    return this._http.get<APIResponse<StorageAccount[]>>(
      this.baseUrl + 'api/ProcessConfiguration/GetStorageAccountDetails',
      { headers: headers, params: params }
    );
  }

  getStorageAccountDetails$(configurationProcessType: number = 1): Observable<StorageAccount[]> {

    if (!configurationProcessType) {
      return of([]);
    }
    return this.getStorageAccountDetails(configurationProcessType) // Observable<APIResponse<StorageAccount[] | null>>
      .pipe(
        map((resp: APIResponse<StorageAccount[] | null>) => {
          const ok = resp && resp.responseCode === 200 && resp.responseMessage?.[0] === 'Success';
          return ok && Array.isArray(resp.result) ? resp.result : [];
        }),
        catchError(err => {
          console.error('Error fetching storage accounts', err);
          return of([] as StorageAccount[]);
        }),
        shareReplay(1)
      );


  }

  getSchedulerType() {
    return this._http.get<APIResponse<SchedulerType[]>>(
      `${this.baseUrl}api/ProcessConfiguration/GetSchedulerType`,
      this.httpHeader()
    );
  }

  getSchedulerTypes$(): Observable<SchedulerType[]> {
    return this.getSchedulerType()
      .pipe(
        map((resp: APIResponse<SchedulerType[]>) => {
          const ok = resp && resp.responseCode === 200 && resp.responseMessage?.[0] === 'Success';
          return ok && Array.isArray(resp.result) ? resp.result : [];
        }),
        catchError(err => {
          console.error('Error fetching scheduler types', err);
          return of([] as SchedulerType[]);
        }),
        shareReplay(1)
      );
  }

  getWeekDayName() {
    return this._http.get<APIResponse<WeekDayName[]>>(
      `${this.baseUrl}api/ProcessConfiguration/GetWeekDayName`,
      this.httpHeader()
    );
  }
  getWeekDayName$(): Observable<WeekDayName[]> {
    return this.getWeekDayName() // Observable<APIResponse<WeekDayName[]>>
      .pipe(
        map((resp: APIResponse<WeekDayName[]>) => {
          const ok = resp && resp.responseCode === 200 && resp.responseMessage?.[0] === 'Success';
          return ok && Array.isArray(resp.result) ? resp.result : [];
        }),
        catchError(err => {
          console.error('Error fetching week day names', err);
          return of([] as WeekDayName[]);
        }),
        shareReplay(1)
      );
  }

  getFrequencyHour() {
    return this._http.get<APIResponse<FrequencyHour[]>>(
      `${this.baseUrl}api/ProcessConfiguration/GetFrequencyHour`,
      this.httpHeader()
    );
  }
  getDataSliceConfiguration(id: number) {
    return this._http.get<APIResponse<DsConfig>>(
      `${this.baseUrl}api/ProcessConfiguration/GetDSConfiguration?id=${id}`,
      this.httpHeader2()
    );
  }

  getDataSliceConfiguration$(id: number): Observable<void> {
    if (!id) {
      return of(void 0);
    }
    return this.getDataSliceConfiguration(id)
      .pipe(
        tap((resp: APIResponse<DsConfig>) => {
          if (resp && resp.result) {
            sessionStorage.setItem('dsAppId', resp.result.consumerApplicationId);
            sessionStorage.setItem('dsSrcId', resp.result.sourceDataObjId);
          }

        }),
        map(() => void 0), //convert to Observable<void>
        catchError(err => {
          console.error('Error fetching data slice configuration', err);
          return of(void 0);
        }),
        shareReplay(1)
      );
  }

  // Fetch security groups from Azure Graph API
  fetchSecurityGroups(searchTerm: string, accessToken: string) {
    //debugger;
    const apiUrl = `https://graph.microsoft.com/v1.0/groups?$filter=startswith(displayName,'${searchTerm}')`;
    const headers = {
      Authorization: `Bearer ${accessToken}`, // Use the token from sessionStorage
    };

    return this._http.get<any>(apiUrl, { headers });
  }


}
