import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { APIResponse } from '../models/apiResponse';
import { DatatypeName, DateTimeFormats } from '../models/datatypeNames';
import { ProcessNames } from '../models/processNames';
import { DIRegions } from '../models/DIRegions';
import { DISubRegions, SecurityGroupRegion } from '../models/DISubRegions';
import { DIClientNames } from '../models/DIClientNames';
import { DIDatabaseNames } from '../models/DIDatabaseNames';
import { LocalFileDataSourceType, LocalFileDataSourceTypev4_1 } from '../models/localFileDataSourceType';
import { FlpConvertParquetRequestDto } from '../models/FlpConvertParquetRequestDto';
import { FileConfiguration, KeyValuePair } from '../models/fileConfiguration';
import {
  FileConfigurationDetails,
  FileProcessConfig,
  LogUploadedFileRequest,
  SchedulerType,
} from '../models/fileProcessConfig';
import { EnglishOnlyCharacters } from '../models/englistOnlyCharacters';
import { DataSourceType } from '../../shared/enum';
import { firstValueFrom, map, Observable, timeout, config, catchError, of, shareReplay } from 'rxjs';
import JSZip from 'jszip';
import { ConvertToXLSXDto } from '../models/processConfigurationlist';
import { OnlineConfigResponse } from '../models/OnlineConfigResponse';
import { FileNameExtension, SelectedFiles } from '../models/LandingLayer/landingLayer';
import { NavigateService } from './navigate.service';
@Injectable({
  providedIn: 'root',
})
export class ConfigurationService {
  private baseUrl = environment.apiEndpoint;
  upn: string = '';
  constructor(private _http: HttpClient, private navigateService: NavigateService) { }
  httpHeader() {
    let headers = new HttpHeaders({
      Authorization: `Bearer ${localStorage.getItem('DIApiToken')}`,
      'x-tpdi-api-version': '1.0',
      'x-tpdi-api-sg': `${sessionStorage.getItem("GUID")}`
    })

    return { headers: headers };
  }
  setHttpHeaders(): HttpHeaders {
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
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-version', '1.0');
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);
    return objHttpHeaders;
  }

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


  setHttpHeadersV2(): HttpHeaders {
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
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-version', '2.0');
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);
    return objHttpHeaders;
  }



  getAllDataTypeNames() {
    let headers: HttpHeaders = this.setHttpHeaders();
    return this._http.get<APIResponse<DatatypeName[]>>(
      this.baseUrl + 'api/ProcessConfiguration/getalldatatypenames',
      { headers: headers }
    );
  }

  getProcessNamesByLoginId(dataSourceType: number) {
    let params = new HttpParams();
    params = params.append('fileProcessingServerTypeId', dataSourceType);
    let url =
      this.baseUrl +
      'api/ProcessConfiguration/GetAllProcessNamesByLoginId';
    let headers: HttpHeaders = this.setHttpHeadersV2();

    return this._http.get<APIResponse<ProcessNames[]>>(url,
      { headers: headers, params: params }
    );
  }

  getProcessNamesByLoginIdByTerm(dataSourceType: number, term: string = '') {
    let params = new HttpParams();
    params = params.append('queryTerm', term);
    params = params.append('fileProcessingServerTypeId', dataSourceType);

    let url =
      this.baseUrl +
      'api/ProcessConfiguration/GetAllProcessNamesByLoginIdByTerm';
    let headers: HttpHeaders = this.setHttpHeadersV2();

    return this._http.get<ProcessNames[]>(url,
      { headers: headers, params: params }
    );
  }

  checkProcessNameExists(processName: string, configId: string = null) {
    let headers: HttpHeaders = this.setHttpHeaders();
    return this._http.get<APIResponse<string>>(
      `${this.baseUrl}api/ProcessConfiguration/ProcessNameExists?processName=${processName}&configId=${configId}`,
      { headers: headers }
    );
  }

  getAllDIRegions() {
    let headers: HttpHeaders = this.setHttpHeaders();
    return this._http.get<APIResponse<DIRegions[]>>(
      this.baseUrl + 'api/ProcessConfiguration/GetAllDIRegions',
      { headers: headers }
    );
  }

  getAllDISubRegions() {
    let headers: HttpHeaders = this.setHttpHeaders();
    return this._http.get<APIResponse<DISubRegions[]>>(
      this.baseUrl + 'api/ProcessConfiguration/GetAllDISubRegions',
      { headers: headers }
    );
  }

  getAllDIClientnames() {
    let headers: HttpHeaders = this.setHttpHeaders();
    return this._http.get<APIResponse<DIClientNames[]>>(
      this.baseUrl + 'api/ProcessConfiguration/GetAllDIClientnames',
      { headers: headers }
    );
  }

  getDIDatabaseNames(
    regionId: number,
    subRegionId: string,
    clientNameId: number,
    dataSourceType: number
  ) {
    let headers: HttpHeaders = this.setHttpHeaders();

    let params = new HttpParams();
    params = params.append('regionId', regionId);
    params = params.append('subRegionId', subRegionId);
    params = params.append('clientNameId', clientNameId);
    params = params.append('fileProcessingServerTypeId', dataSourceType);

    return this._http.get<APIResponse<DIDatabaseNames[]>>(
      this.baseUrl + 'api/ProcessConfiguration/GetDatabaseNames',
      { headers: headers, params: params }
    );
  }

  getDIDeltaDatabaseNames(
    regionId: number,
    subRegionId: string,
    clientNameId: number
  ) {
    let headers: HttpHeaders = this.setHttpHeadersV3();

    let params = new HttpParams();
    params = params.append('regionId', regionId);
    params = params.append('subRegionId', subRegionId);
    params = params.append('clientNameId', clientNameId);

    return this._http.get<APIResponse<DIDatabaseNames[]>>(
      this.baseUrl + 'api/ProcessConfiguration/GetDeltaDatabaseNames',
      { headers: headers, params: params }
    );
  }

  getAllDataTimeFormats(displayOnLandingLayer: boolean) {
    let headers: HttpHeaders = this.setHttpHeaders();
    return this._http.get<APIResponse<DateTimeFormats[]>>(
      this.baseUrl + `api/ProcessConfiguration/GetAllDataTimeFormats?displayOnLandingLayer=${displayOnLandingLayer}`,
      { headers: headers }
    );
  }

  getAllDataTimeFormats$(): Observable<DateTimeFormats[]> {
    var showDefault = this.navigateService.configurationProcess === DataSourceType.LandingLayer;
    return this.getAllDataTimeFormats(showDefault).pipe(
      map((resp: APIResponse<DateTimeFormats[]>) => {
        const ok = resp && resp.responseCode === 200 && resp.responseMessage?.[0] === 'Success';
        return ok ? resp.result : [];
      }),
      catchError(err => {
        console.error('Error fetching date time formats', err);
        return of([] as DateTimeFormats[]);
      }),
      shareReplay(1)
    );
  }


  getConfigurationWithId(id: number) {
    let params = new HttpParams();
    let url =
      this.baseUrl + 'api/ProcessConfiguration/GetConfigurationById?id=' + id;
    let headers: HttpHeaders = this.setHttpHeaders();
    return this._http.get<APIResponse<FileConfiguration>>(url, {
      headers: headers,
    });
  }

  getConfigurationWithIdV4_1(id: number) {
    let params = new HttpParams();
    let url =
      this.baseUrl + 'api/ProcessConfiguration/GetConfigurationById?id=' + id;
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    return this._http.get<APIResponse<OnlineConfigResponse>>(url, {
      headers: headers,
    });
  }

  insertNewConfiguration(file: File, configuration: LocalFileDataSourceType) {
    const formData: FormData = new FormData();
    let headers: HttpHeaders = new HttpHeaders();
    headers = headers.set(
      'Authorization',
      `Bearer ${localStorage.getItem('DIApiToken')}`
    );
    headers = headers.set('x-tpdi-api-version', '1.0');
    headers = headers.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);
    const data = {
      additionalSettings: configuration.additionalSettings,
      columnNameDatatypeNames: configuration.columnNameDatatypeNames,
    };

    formData.append('file', file, file.name);
    //if (environment.userId) formData.append('UserName', environment.userId);
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
    //if (environment.userId) formData.append('LoggedInUser', environment.userId);
    //formData.append('additionalSettings', configuration.additionalSettings)
    //formData.append('additionalSettings', new Blob([JSON.stringify(configuration.additionalSettings)], {type:'multipart/form-data'}));
    formData.append(
      'myJson',
      new Blob([JSON.stringify(data)], { type: 'application/json' })
    );
    //formData.append('additionalSettings.delimiter', data.additionalSettings.delimiter);
    console.log('transferring data');
    //console.log(formData);
    return this._http.post<APIResponse<FlpConvertParquetRequestDto>>(
      `${this.baseUrl}api/ProcessConfiguration/InsertConfiguration`,
      formData,
      { headers }
    );
  }


  insertNewConfigurationV4_1(file: File, configuration: LocalFileDataSourceTypev4_1) {
    //debugger;
    const formData: FormData = new FormData();
    let headers: HttpHeaders = new HttpHeaders();
    headers = headers.set(
      'Authorization',
      `Bearer ${localStorage.getItem('DIApiToken')}`
    );
    headers = headers.set('x-tpdi-api-version', '4.1');
    headers = headers.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);
    // const data = {
    //   additionalSettings: configuration.additionalSettings,
    //   columnNameDatatypeNames: configuration.columnNameDatatypeNames,
    // };


    const data = {
      processSettings: configuration.processSettings,
      fileSettings: configuration.fileSettings

    };


    formData.append('file', file, file.name);
    //if (environment.userId) formData.append('UserName', environment.userId);
    if (sessionStorage.getItem('username'))
      formData.append('UserName', sessionStorage.getItem('username'));

    if (sessionStorage.getItem('upn').split('@')[0])
      formData.append('LoggedInUser', sessionStorage.getItem('upn').split('@')[0]);
    //if (environment.userId) formData.append('LoggedInUser', environment.userId);
    //formData.append('additionalSettings', configuration.additionalSettings)
    //formData.append('additionalSettings', new Blob([JSON.stringify(configuration.additionalSettings)], {type:'multipart/form-data'}));
    formData.append(
      'myJson',
      new Blob([JSON.stringify(data)], { type: 'application/json' })
    );
    //formData.append('additionalSettings.delimiter', data.additionalSettings.delimiter);
    console.log('transferring data');
    //console.log(formData);
    return this._http.post<APIResponse<FlpConvertParquetRequestDto>>(
      `${this.baseUrl}api/ProcessConfiguration/InsertConfiguration`,
      formData,
      { headers }
    );
  }

  

  flpCSVtoParquet(
    flpConvertParquetRequestDto: FlpConvertParquetRequestDto,
    apiEndPoint: string,
    isDataBricks: boolean = false,
    apiVersion: string
  ) {
    let headers: HttpHeaders = new HttpHeaders();
    headers = headers.set(
      'Authorization',
      `Bearer ${localStorage.getItem('DIApiToken')}`
    );
    if (isDataBricks) {
      headers = headers.set('x-tpdi-api-version', apiVersion);
    }
    else {
      headers = headers.set('x-tpdi-api-version', apiVersion);
    }
    headers = headers.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);
    const data = {
      flpConfigurationId: flpConvertParquetRequestDto.flpConfigurationId,
      processName: flpConvertParquetRequestDto.processName,
      blobClients: {
        uri: flpConvertParquetRequestDto.blobClients.uri,
        accountName: flpConvertParquetRequestDto.blobClients.accountName,
        blobContainerName:
          flpConvertParquetRequestDto.blobClients.blobContainerName,
        name: flpConvertParquetRequestDto.blobClients.name,
        canGenerateSasUri: true,
        uploadedId: flpConvertParquetRequestDto.blobClients.uploadedId,
      },
    };

    return this._http.post(`${this.baseUrl}api/import/${apiEndPoint}`, data, {
      headers,
    });
  }

  
  InsertFlpConfiguration(processConfig: FileProcessConfig, apiVersion: string = "1.0") {
    let headers: HttpHeaders = this.setHttpsRequestHeaders(apiVersion);

    if (apiVersion === "1.0") {
      return this._http.post<APIResponse<FileProcessConfig>>(
        this.baseUrl + 'api/ProcessConfiguration/InsertFlpConfigurationDetails',
        processConfig,
        { headers }
      );
    } else {
      return this._http.post<APIResponse<FileProcessConfig>>(
        this.baseUrl + 'api/ProcessConfiguration/LandingLayerOfflineModuleInsertConfiguration',
        processConfig,
        { headers }
      );
    }

  }

  insertConfigLandingLayerOfflineMode(processConfig: FileProcessConfig) {
    return this._http.post<APIResponse<FileProcessConfig>>(
      this.baseUrl + 'api/ProcessConfiguration/LandingLayerOfflineModeInsertConfiguration',
      processConfig,
      this.httpHeader()
    );
  }

  logUploadedFile(request: LogUploadedFileRequest) {
    let params = new HttpParams();
    let url =
      this.baseUrl + 'api/ProcessConfiguration/LogUploadedFile';
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    return this._http.post<APIResponse<boolean>>(url,
      request,
      { headers }
    );
  }

  getConfigurationDetails(configurationId: string) {
    return this._http.get<APIResponse<FileConfigurationDetails>>(
      `${this.baseUrl}api/ProcessConfiguration/GetConfigurationDetailsById?configurationId=${configurationId}`,
      this.httpHeader()
    );
  }



  getConfigurationDetailsV2(configurationId: string, tabName: string) {
    let url = `${this.baseUrl}api/ProcessConfiguration/GetConfigurationDetailsById?configurationId=${configurationId}`;
    if (tabName) {
      url += `&tabName=${tabName}`;
    }
    return this._http.get<APIResponse<FileConfigurationDetails>>(url, this.httpHeader());
  }



  convertToEnglishOnlyCharacters(language: string, wordToBeConverted: string) {
    let headers: HttpHeaders = new HttpHeaders();
    headers = headers.set(
      'Authorization',
      `Bearer ${localStorage.getItem('DIApiToken')}`
    );
    headers = headers.set('x-tpdi-api-version', '3.0');

    headers = headers.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);

    return this._http.get<APIResponse<string>>(
      `${this.baseUrl}api/ProcessConfiguration/ConvertWordToEnglishCharactersOnly?wordToConvert=${wordToBeConverted}&language=${language}`,
      { headers }
    );
  }

  getAllEnglishCharactersOnly(language: string) {
    let headers: HttpHeaders = new HttpHeaders();
    headers = headers.set(
      'Authorization',
      `Bearer ${localStorage.getItem('DIApiToken')}`
    );
    headers = headers.set('x-tpdi-api-version', '3.0');

    headers = headers.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);

    return this._http.get<APIResponse<EnglishOnlyCharacters[]>>(
      `${this.baseUrl}api/ProcessConfiguration/GetAllEnglishCharactersOnly?language=${language}`,
      { headers }
    );

  }
  getRegionBySecurityGroupId(securityGroupId: string) {
    let headers: HttpHeaders = this.setHttpHeadersV2();
    return this._http.get<APIResponse<SecurityGroupRegion[]>>(
      `${this.baseUrl}api/ProcessConfiguration/getRegionBySecurityGroupId?securityGroupId=${securityGroupId}`,
      { headers: headers }
    );
  }

  testBackendParser(file: File) {
    const formData: FormData = new FormData();
    let headers: HttpHeaders = new HttpHeaders();
    headers = headers.set(
      'Authorization',
      `Bearer ${localStorage.getItem('DIApiToken')}`
    );
    headers = headers.set('x-tpdi-api-version', '3.0');
    headers = headers.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);
    formData.append('file', file, file.name);
    return this._http.post<APIResponse<KeyValuePair>>(
      `${this.baseUrl}api/ProcessConfiguration/ParseExcel`, formData, { headers }
    );
  }

  async uploadChunks(file: File) {
    const chunkSize = 5 * 1024 * 1024; // 5MB
    const totalChunks = Math.ceil(file.size / chunkSize);

    for (let i = 0; i < totalChunks; i++) {
      const start = i * chunkSize;
      const end = Math.min(start + chunkSize, file.size);
      const chunk = file.slice(start, end);

      const formData = new FormData();
      formData.append('chunk', chunk);
      formData.append('chunkIndex', i.toString());
      formData.append('totalChunks', totalChunks.toString());
      formData.append('fileName', file.name);

      await firstValueFrom(this._http.post(`${this.baseUrl}api/ProcessConfiguration/ConvertToXLSX`, formData));
    }
  }

  UploadToBlobTemp(file: File) {
    const formData: FormData = new FormData();
    let headers: HttpHeaders = new HttpHeaders();

    headers = headers.set(
      'Authorization',
      `Bearer ${localStorage.getItem('DIApiToken')}`
    );

    headers = headers.set('x-tpdi-api-version', '1.0');
    headers = headers.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);

    formData.append('file', file, file.name);
    formData.append('FileName', file.name);

    return this._http.post<APIResponse<string>>(
      `${this.baseUrl}api/ProcessConfiguration/UploadToBlobTemp`, formData, { headers: headers }
    );
  }



  getConvertedXLSX(fileName: string) {
    let headers: HttpHeaders = new HttpHeaders();

    headers = headers.set(
      'Authorization',
      `Bearer ${localStorage.getItem('DIApiToken')}`
    );

    headers = headers.set('x-tpdi-api-version', '1.0');
    headers = headers.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);
    headers = headers.set('fileName', fileName);
    headers = headers.set('fileExt', fileName.split('.').pop());


    return this._http.get<APIResponse<ConvertToXLSXDto>>(
      `${this.baseUrl}api/ProcessConfiguration/getConvertedXLSX`, { headers: headers }
    )

  }


  convertToXLSX(file: File, processName: string): Observable<{ blob: Blob, rowCount: number }> {


    const formData: FormData = new FormData();
    let headers: HttpHeaders = new HttpHeaders();

    headers = headers.set(
      'Authorization',
      `Bearer ${localStorage.getItem('DIApiToken')}`
    );

    headers = headers.set('x-tpdi-api-version', '3.0');
    headers = headers.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);

    formData.append('file', file, file.name);

    return this._http.post<{ fileData: string, rowCount: number }>(
      `${this.baseUrl}api/ProcessConfiguration/ConvertToXLSX`, formData, { headers: headers }
    ).pipe(
      timeout(10 * 60 * 1000), //10min
      map(response => {
        const byteCharacters = atob(response.fileData);
        const byteNumbers = Array.from(byteCharacters, c => c.charCodeAt(0));
        const byteArray = new Uint8Array(byteNumbers);
        const newBlob = new Blob([byteArray], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });

        return { blob: newBlob, rowCount: response.rowCount };
      })
    );
  }

  async getSasKey(): Promise<APIResponse<string>> {
    let headers: HttpHeaders = new HttpHeaders();
    headers = headers.set(
      'Authorization',
      `Bearer ${localStorage.getItem('DIApiToken')}`
    );
    headers = headers.set('x-tpdi-api-version', '1.0');
    headers = headers.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);

    return await firstValueFrom(this._http.get<APIResponse<string>>(
      `${this.baseUrl}api/ProcessConfiguration/GetSasKey`,
      { headers: headers }
    ));
  }

  getFileExtensionNames() {
    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    return this._http.get<APIResponse<FileNameExtension[]>>(
      this.baseUrl + 'api/ProcessConfiguration/GetValidFileExtensions',
      { headers: headers }
    );
  }

  getFileExtensionNames$(): Observable<FileNameExtension[]> {
    if (this.navigateService.configurationProcess !== DataSourceType.LandingLayer) {
      return of([] as FileNameExtension[]);
    }

    return this.getFileExtensionNames().pipe(
      map((resp: APIResponse<FileNameExtension[]>) => {
        const ok = resp && resp.responseCode === 200 && resp.responseMessage?.[0] === 'Success';
        return ok ? resp.result : [];
      }),
      catchError(err => {
        console.error('Error fetching file extensions', err);
        return of([] as FileNameExtension[]);
      }),
      shareReplay(1)
    );
  }


}

