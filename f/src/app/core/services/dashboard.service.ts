import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { APIResponse } from '../../shared/models/apiResponse';
import {
  ClientListResponse,
  DiUploads,
  DiUtilization,
  FileListResponse,
  ProcessListRequest,
  ProcessListResponse,
  RealTimeProcessing,
  UtilizationRegion,
} from '../../shared/models/dashboard';
import { AzureUserGroup, CampaignUserAccess, SecurityGroup } from '../models/userDetails';
import { Observable, expand, EMPTY, map, reduce } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class DashboardService {
  private baseUrl = environment.apiEndpoint;
  constructor(private _http: HttpClient) { }
  httpHeader() {
    let headers = new HttpHeaders({
      Authorization: `Bearer ${localStorage.getItem('DIApiToken')}`,
      'x-tpdi-api-version': '1.0',
      'Content-Type': 'application/json',
      'x-tpdi-api-sg': `${sessionStorage.getItem('GUID')}`,
    })

    return { headers: headers };
  }
  httpHeader2() {
    let headers = new HttpHeaders({
      Authorization: `Bearer ${localStorage.getItem('DIApiToken')}`,
      'x-tpdi-api-version': '2.0',
      'Content-Type': 'application/json',
      'x-tpdi-api-sg': `${sessionStorage.getItem('GUID')}`,
    })

    return { headers: headers };
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

  getFromDate(): Date {
    let today = new Date();
    today.setDate(1);
    today.setMonth(today.getMonth() - 2);
    return today;
  }
  getProcessesCreated() {
    let data = {
      regionId: 0,
      subRegionId: null,
      clientId: 0,
      fromDate: this.getFromDate(),
      toDate: new Date(),
    };

    return this._http.post<APIResponse<ProcessListResponse>>(
      this.baseUrl + 'api/Dashboard/GetProcessList',
      data,
      this.httpHeader()
    );
  }
  getClientList() {
    let data = {
      regionId: 0,
      subRegionId: null,
      clientId: 0,
      fromDate: this.getFromDate(),
      toDate: new Date(),
    };
    return this._http.post<APIResponse<ClientListResponse>>(
      this.baseUrl + 'api/Dashboard/GetClientList',
      data,
      this.httpHeader()
    );
  }
  getFilesProcessed() {
    let data = {
      regionId: 0,
      subRegionId: null,
      clientId: 0,
      fromDate: this.getFromDate(),
      toDate: new Date(),
    };
    return this._http.post<APIResponse<FileListResponse>>(
      this.baseUrl + 'api/Dashboard/GetFileList',
      data,
      this.httpHeader()
    );
  }
  getDiUploads() {
    let data = {
      regionId: 0,
      subRegionId: null,
      clientId: 0,
      fromDate: this.getFromDate(),
      toDate: new Date(),
    };
    return this._http.post<APIResponse<DiUploads[]>>(
      this.baseUrl + 'api/Dashboard/CountFileUploadsByProcessType',
      data,
      this.httpHeader()
    );
  }
  getRealTimeProcessingStatus() {
    let data = {
      regionId: 0,
      subRegionId: null,
      clientId: 0,
      fromDate: this.getFromDate(),
      toDate: new Date(),
      noOfRecords: 3,
    };
    return this._http.post<APIResponse<RealTimeProcessing[]>>(
      this.baseUrl + 'api/Dashboard/DashboardRealTimeProcessingStatusList',
      data,
      this.httpHeader()
    );
  }
  getDIFrameworkUtilization() {
    return this._http.get<APIResponse<DiUtilization[]>>(
      this.baseUrl + 'api/Dashboard/DIFrameworkUtilization',
      this.httpHeader()
    );
  }
  getUtilizationByRegions() {
    return this._http.get<APIResponse<UtilizationRegion[]>>(
      this.baseUrl + 'api/Dashboard/UtilizationByRegions',
      this.httpHeader()
    );
  }
  getAllGroup() {
    const encodedUpn = encodeURIComponent(sessionStorage.getItem('upn') || '');
    var data = this._http.get<APIResponse<SecurityGroup[]>>(
      `${this.baseUrl
      }api/ProcessConfiguration/SecurityGroups?loginId=${encodedUpn}`,
      this.httpHeader2()
    );
    //debugger;
    //console.log(data);
    return data;
  }


  saveUserGroup(groupId: string) {
    let data = {
      SecurityGroupId: groupId,
      LoginId: sessionStorage.getItem('upn'),
      UserName: sessionStorage.getItem('username'),
    };

    return this._http.post<any>(
      this.baseUrl + 'api/ProcessConfiguration/SaveSecurityGroup',
      data,
      this.httpHeader2()
    );
  }
  
  getUserGroup() {
    return this._http.get<AzureUserGroup>(
      `${environment.graphApiEndpoint}memberOf?$select=displayName,id,groupTypes`
    );
  }

  getAllUserGroups(): Observable<any[]> {
    const url = `${environment.graphApiEndpoint}memberOf?$select=displayName,id,groupTypes`;  
    

    return this._http.get<any>(url).pipe(
      expand(response =>
        response['@odata.nextLink']
          ? this._http.get<any>(response['@odata.nextLink'])
          : EMPTY
      ),
      map(response => response.value),
      reduce((acc, val) => acc.concat(val), [])
    );
  }

  calculateSuccessPercentages(files: FileListResponse) {
    if (files.totalUploadedFiles <= 0) {
      return {
        successPercentage: 0,
        failurePercentage: 0,
      };
    }
    const successPercentage =
      (files.successCount / files.totalUploadedFiles) * 100;
    const failurePercentage =
      (files.failureCount / files.totalUploadedFiles) * 100;
    return {
      successPercentage,
      failurePercentage,
    };
  }
  getFileExtension(fileName: string): string {
    if (!fileName) {
      return 'doc';
    }
    const index = fileName.lastIndexOf('.');
    return index !== -1 ? fileName.substring(index + 1) : 'doc';
  }
  getMonthName(month: number): string {
    const monthNames: string[] = [
      'January',
      'February',
      'March',
      'April',
      'May',
      'June',
      'July',
      'August',
      'September',
      'October',
      'November',
      'December',
    ];

    if (month >= 1 && month <= 12) return monthNames[month - 1];
    else return '';
  }
  getFileName(filename: string): string {
    const startName = filename.substring(0, 8);
    const endName = filename.substring(filename.length - 6);
    return `${startName}..${endName}`;
  }
  getProfilePic() {
    return this._http.get(environment.graphApiEndpoint + 'photo/$value', {
      responseType: 'blob',
    });
  }
  getUserProfile() {
    return this._http.get<any>(environment.graphApiEndpoint);
  }
  sumArray(numbers: number[]): number {
    return numbers.reduce((a, b) => a + b, 0);
  }
}
