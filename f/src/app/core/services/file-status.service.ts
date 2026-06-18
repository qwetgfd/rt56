import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { APIResponse } from '../../shared/models/apiResponse';
import {
  FileUploadDetailedStatus,
  FlpUploadedFileStatusResponse,
} from '../../shared/models/fileUploadStatus';
import { FlpProcessedFileListResponse, ProcessedFileRequest } from '../models/processedFileList';
import { catchError, Observable, throwError } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class FileUploadStatusService {
  private baseUrl = environment.apiEndpoint;
  constructor(private _http: HttpClient) { }
  httpHeader = {
    headers: new HttpHeaders({
      'Authorization': `Bearer ${localStorage.getItem('DIApiToken')}`,
      'x-tpdi-api-version': '1.0',
      'Content-Type': 'application/json',
      'x-tpdi-api-sg': `${sessionStorage.getItem("GUID")}`,
    }),
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
    // objHttpHeaders = objHttpHeaders.set('Accept', 'application/json');
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-version', apiVersion);
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);
    return objHttpHeaders;
  }


  setHttpsRequestHeadersV2(apiVersion: string): HttpHeaders {
    let objHttpHeaders = new HttpHeaders();
    objHttpHeaders = objHttpHeaders.append(
      'Authorization',
      `Bearer ${localStorage.getItem('DIApiToken')}`
    );
    objHttpHeaders = objHttpHeaders.set(
      'Content-Type',
      'application/json; charset=utf-8'
    );
    // objHttpHeaders = objHttpHeaders.set('Accept', 'application/json');
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-version', apiVersion);
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);
     const isInternalRequest: boolean = true;
    objHttpHeaders = objHttpHeaders.set('internalDIFRequest', String(isInternalRequest)); // Convert boolean to string

    return objHttpHeaders;
  }


  getFileUploadStatus() {

    let headers = new HttpHeaders({
      'Authorization': `Bearer ${localStorage.getItem("DIApiToken")}`,
      'x-tpdi-api-version': '1.0',
      'Content-Type': 'application/json',
      'x-tpdi-api-sg': `${sessionStorage.getItem("GUID")}`,
    });
    return this._http.get<APIResponse<FlpUploadedFileStatusResponse[]>>(
      this.baseUrl + 'api/Status/FileUploadStatus',
      { headers: this.setHttpsRequestHeaders('1.0') }
    );
  }
  getFileUploadDetailedStatus(
    flpConfigurationID: string,
    uploadFileId: string,
    tabName: string
  ) {
    let data = {
      flpConfigurationID: flpConfigurationID,
      uploadFileId: uploadFileId,
      tabName: tabName
    };
    return this._http.post<APIResponse<FileUploadDetailedStatus>>(
      this.baseUrl + 'api/Status/FileUploadDetailedStatus',
      data,
      { headers: this.setHttpsRequestHeaders('1.0') }
      //this.httpHeader
    );
  }

  downloadFile(sasUrl: string) {
    let theaders = new HttpHeaders({
      'Authorization': `Bearer ${localStorage.getItem("DIApiToken")}`,
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

  getAllProcessedFile(request: ProcessedFileRequest): Observable<APIResponse<FlpProcessedFileListResponse>> {
    return this._http.post<APIResponse<FlpProcessedFileListResponse>>(
      this.baseUrl + 'api/Status/GetProcessedFileList',
      request,
      //this.httpHeader
      { headers: this.setHttpsRequestHeadersV2('1.0') }
    );
  }
}
