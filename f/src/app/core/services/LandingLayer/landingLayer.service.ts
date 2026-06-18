import { HttpClient, HttpEvent, HttpHeaders, HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { NavigateService } from '../navigate.service';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { APIResponse } from '../../models/apiResponse';
import { LandingLayerConfiguration, LandingLayerInsertConfigurationRequest, SelectedFiles } from '../../models/LandingLayer/landingLayer';
import { FlpConvertParquetRequestDto } from '../../models/FlpConvertParquetRequestDto';
import { LocalFileDataSourceTypev4_1 } from '../../models/localFileDataSourceType';

@Injectable({
  providedIn: 'root'
})
export class LandingLayerService {

  constructor(private http: HttpClient, private navigateService: NavigateService) { }

  private baseUrl = environment.apiEndpoint;

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


  getLandingLayerUploadConfiguration() {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    return this.http.get<APIResponse<LandingLayerConfiguration>>(
      this.baseUrl + 'api/ProcessConfiguration/GetLandingLayerUploadConfiguration',
      { headers: headers }
    );
  }

  uploadFile(file: File): Observable<HttpEvent<any>> {
    const formData: FormData = new FormData();
    formData.append('file', file);


    const req = new HttpRequest(
      'POST',
      `${this.baseUrl}api/ProcessConfiguration/upload`,   // Replace with your API endpoint
      formData,
      {
        reportProgress: true,
        headers: this.setHttpsRequestHeaders('4.1')
      }
    );

    return this.http.request(req);

  }

  insertConfigLandingLayer(selectedFiles: SelectedFiles[], configuration: LocalFileDataSourceTypev4_1) {
      const formData: FormData = new FormData();
      let headers: HttpHeaders = new HttpHeaders();
      headers = headers.set(
        'Authorization',
        `Bearer ${localStorage.getItem('DIApiToken')}`
      );
      headers = headers.set('x-tpdi-api-version', '4.1');
      headers = headers.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);
  
      const data = {
        processSettings: configuration.processSettings,
        fileSettings: configuration.fileSettings
      };
  
      if (sessionStorage.getItem('username'))
        formData.append('userName', sessionStorage.getItem('username'));
  
      if (sessionStorage.getItem('upn').split('@')[0])
        formData.append('loggedInUser', sessionStorage.getItem('upn').split('@')[0]);
  
      // formData.append(
      //   'myJson',
      //   new Blob([JSON.stringify(data)], { type: 'application/json' })
      // );
  
      formData.append('MyJson', JSON.stringify(data));
  
      // --- FILES: append each file under the same field name ---    
      for (const sel of selectedFiles) {
        const file = sel?.File;
        if (file) {
          // third param sets filename explicitly (good practice)
          formData.append('files', file, file.name);
        }
      }
  
      return this.http.post<APIResponse<FlpConvertParquetRequestDto>>(
        `${this.baseUrl}api/ProcessConfiguration/LandingLayerInsertConfiguration`,
        formData,
        { headers }
      );
    }

  moveFileToLandingLayer(processConfig: LandingLayerInsertConfigurationRequest) {
    let headers: HttpHeaders = new HttpHeaders();
      headers = headers.set(
        'Authorization',
        `Bearer ${localStorage.getItem('DIApiToken')}`
      );
      headers = headers.set('x-tpdi-api-version', '4.1');
      headers = headers.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);

    const formData = new FormData();
    formData.append('processName', processConfig.processName);
    formData.append('flpConfigurationId', processConfig.flpConfigurationId);
    formData.append('uploadFileId', processConfig.uploadFileId);
    formData.append('loggedInUser', processConfig.loggedInUser);
    formData.append('userName', processConfig.userName); 

    // Append files to FormData
    processConfig.files.forEach((file, index) => {
      formData.append(`files`, file, file.name);
    });
    
    return this.http.post(`${this.baseUrl}api/processConfiguration/MovedFileToLandingLayer`, formData,
      { headers: headers }
    );

  }
}