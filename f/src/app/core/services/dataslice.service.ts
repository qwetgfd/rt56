import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import {
  DsApiRequest,
  DsApiResponse,
  DsClientResponseDto,
  DsConfig,
  DsRegionResponseDto,
  DsRegionSubRegionRequestDto,
  DsSubRegionResponseDto,
  RegionSubRegionClient,
  Result,
} from '../models/dsRegion';
import { APIResponse } from '../models/apiResponse';
import { retry } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class DataSliceService {
  // private baseUrl = environment.dsApiEndPoint;
  private baseUrl2 = environment.apiEndpoint;
  constructor(private _http: HttpClient) {}

  setHttpHeadersV3(): HttpHeaders {
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
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-version', '3.0');
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);
    return objHttpHeaders;
  }

  // getToken(): Promise<string> {
  //   const dsHeaders = {
  //     headers: new HttpHeaders({
  //       'x-tpds-api-version': '1.0',
  //       ClientId: environment.dsAppId,
  //       clientKey: environment.dsclientKey,
  //       'Content-Type': 'application/json',
  //     }),
  //   };
  //   return new Promise((res, rej) => {
  //     this._http
  //       .post<any>(this.baseUrl + 'api/Account/authenticate', null, dsHeaders)
  //       .pipe(retry(3))
  //       .subscribe({
  //         next: (response) => {
  //           if (
  //             response &&
  //             response.responseCode == 200 &&
  //             response.responseMessage[0] == 'Success'
  //           ) {
  //             //console.log(response.result.token);
  //             res(response.result.token);
  //           } else {
  //             res('');
  //           }
  //         },
  //         error: (error) => {
  //           res('');
  //           console.log(error);
  //         },
  //       });
  //   });
  // }
  // getRegion(token: string, data: DsApiRequest): Promise<Result> {
  //   const httpHeader = {
  //     headers: new HttpHeaders({
  //       Authorization: `Bearer ${token}`,
  //       'x-tpds-api-version': '1.0',
  //       'Content-Type': 'application/json',
  //     }),
  //   };
  //   return new Promise((res, rej) => {
  //     this._http
  //       .post<APIResponse<Result>>(
  //         `${environment.dsApiEndPoint}api/DataSlice/GetData`,
  //         data,
  //         httpHeader
  //       )
  //       .pipe(retry(3))
  //       .subscribe({
  //         next: (response) => {
  //           if (
  //             response &&
  //             response.responseCode == 200 &&
  //             response.responseMessage[0] == 'Success'
  //           ) {
  //             res(response.result);
  //           } else {
  //             console.log('failed');
  //             res(null);
  //           }
  //         },
  //         error: (error) => {
  //           res(null);
  //           console.log('error in getting region');
  //           console.error(error);
  //         },
  //       });
  //   });
  // }

  fillCache(){
    
    const dsHeaders = {
      headers: new HttpHeaders({
        'x-tpds-api-version': '3.0',
        'Content-Type': 'application/json', 

      }),
    };

    
    let headers: HttpHeaders = this.setHttpHeadersV3();
    return new Promise((res, rej) =>{
      this._http.post<APIResponse<any>>(`${this.baseUrl2}api/DataSliceAPI/FillCache`,null, {headers})
      .pipe(retry(3))
      .subscribe();
    });
     
  }

  getRegion2(){
    let headers: HttpHeaders = this.setHttpHeadersV3();
    return this._http.get<APIResponse<DsRegionResponseDto[]>>(
      `${this.baseUrl2}api/DataSliceAPI/GetRegionsBySecurityGroup`,
      { headers: headers }
    );
  }

  getSubRegions(regionId : string){
    let headers: HttpHeaders = this.setHttpHeadersV3();
    headers = headers.set('regionId', String(regionId));      
    return this._http.get<APIResponse<DsSubRegionResponseDto[]>>(
      `${this.baseUrl2}api/DataSliceAPI/GetSubRegions`,      
      { headers: headers }      
    );
  }

  getClients(regionId: string, subregionCode : string){
    let headers: HttpHeaders = this.setHttpHeadersV3();    
    headers = headers.set('regionId', String(regionId));
    headers = headers.set('subregionCode', String(subregionCode));
    return this._http.get<APIResponse<DsClientResponseDto[]>>(
      `${this.baseUrl2}api/DataSliceAPI/GetClients`,      
      { headers: headers }
      
    );
  }
}
