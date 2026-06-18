import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { APIResponse } from '../../shared/models/apiResponse';
import {
  FileUploadDetailedStatus,
  FlpConfigurationResponse,
  FlpUploadedFileStatusResponse,
} from '../../shared/models/fileUploadStatus';
import { Observable } from 'rxjs';
import { FlpProcessConfiguration, FlpProcessConfigurationResponse, ProcessConfigurationRequest, UpdateActiveStatusByFlpConfigurationIdRequest, UpdateActiveStatusByFlpConfigurationIdResponse } from '../models/processConfigurationlist';

@Injectable({
  providedIn: 'root',
})
export class FlpProcessConfigurationService {
  private baseUrl = environment.apiEndpoint;
  constructor(private _http: HttpClient) { }
  httpHeader() {
    let headers = new HttpHeaders({
      'Authorization': `Bearer ${localStorage.getItem('DIApiToken')}`,
      'x-tpdi-api-version': '1.0',
      'Content-Type': 'application/json',
      'x-tpdi-api-sg': `${sessionStorage.getItem("GUID")}`
    });
    return { headers: headers };
  }

  httpHeader3 = {
    headers: new HttpHeaders({
      'Authorization': `Bearer ${localStorage.getItem('DIApiToken')}`,
      'x-tpdi-api-version': '3.0',
      'Content-Type': 'application/json',
      'x-tpdi-api-sg': `${sessionStorage.getItem("GUID")}`
    }),
  };
  // getProcessConfigList() : Observable<APIResponse<FlpProcessConfiguration[]>> {
  //   return this._http.get<APIResponse<FlpProcessConfiguration[]>>(
  //     this.baseUrl + 'api/ProcessConfiguration/GetFileProcessConfigurationList',
  //     this.httpHeader
  //   );
  // }
  getProcessConfigList(request: ProcessConfigurationRequest): Observable<APIResponse<FlpProcessConfigurationResponse>> {
    return this._http.post<APIResponse<FlpProcessConfigurationResponse>>(
      this.baseUrl + 'api/ProcessConfiguration/GetFileProcessConfigurationList', request,
      this.httpHeader()
    );
  }

  UpdateActiveStatusByFlpConfigurationId(request: UpdateActiveStatusByFlpConfigurationIdRequest) {
    return this._http.post<APIResponse<UpdateActiveStatusByFlpConfigurationIdResponse>>(
      this.baseUrl + 'api/ProcessConfiguration/UpdateActiveStatusByFlpConfigurationId',
      request,
      this.httpHeader3
    )
  }
}
