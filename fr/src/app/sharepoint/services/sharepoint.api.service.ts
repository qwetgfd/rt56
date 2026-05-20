import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, map, of } from 'rxjs';
import { SHAREPOINT_ENV } from '../core/sharepoint.config';
import {
  ApiEnvelope,
  ApplicationDto,
  ApplicationTypeDto,
  SharePointItemDto,
  SharePointLibraryDto,
  WorkspaceConnection,
} from '../core/sharepoint.types';

@Injectable({ providedIn: 'root' })
export class SharePointApiService {
  private readonly env = inject(SHAREPOINT_ENV);
  private readonly baseUrl = this.env.apiBaseUrl.replace(/\/$/, '');

  constructor(private readonly http: HttpClient) {}

  static encodePath64(filePath: string): string {
    const utf8 = unescape(encodeURIComponent(filePath));
    let bin = '';
    for (let i = 0; i < utf8.length; i++) {
      bin += String.fromCharCode(utf8.charCodeAt(i) & 0xff);
    }
    return btoa(bin).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  }

  private get headers(): HttpHeaders {
    return new HttpHeaders({ 'Content-Type': 'application/json' });
  }

  private unwrap<T>(): (res: ApiEnvelope<T>) => T {
    return (res) => {
      if (!res?.success) throw new Error(res?.message ?? 'Request failed.');
      return res.data;
    };
  }

  listApplicationTypes(): Observable<ApplicationTypeDto[]> {
    return this.http
      .get<ApiEnvelope<ApplicationTypeDto[]>>(`${this.baseUrl}/applications/types`)
      .pipe(map(this.unwrap<ApplicationTypeDto[]>()));
  }

  listApplications(typeCode?: string): Observable<ApplicationDto[]> {
    const qs = typeCode ? `?ownerKey=${encodeURIComponent(this.env.ownerKey)}&typeCode=${encodeURIComponent(typeCode)}` : '';
    return this.http
      .get<ApiEnvelope<ApplicationDto[]>>(`${this.baseUrl}/applications${qs}`)
      .pipe(map(this.unwrap<ApplicationDto[]>()));
  }

  saveApplication(payload: Partial<ApplicationDto>): Observable<ApplicationDto> {
    return this.http
      .post<ApiEnvelope<ApplicationDto>>(`${this.baseUrl}/applications`, payload, { headers: this.headers })
      .pipe(map(this.unwrap<ApplicationDto>()));
  }

  deleteApplication(applicationId: string): Observable<void> {
    return this.http
      .delete<ApiEnvelope<unknown>>(`${this.baseUrl}/applications/${applicationId}`)
      .pipe(map(() => undefined));
  }

  listLibraries(connection: WorkspaceConnection): Observable<SharePointLibraryDto[]> {
    return this.http
      .post<ApiEnvelope<SharePointLibraryDto[]>>(`${this.baseUrl}/applications/libraries`, connection, { headers: this.headers })
      .pipe(map(this.unwrap<SharePointLibraryDto[]>()));
  }

  browseFolder(connection: WorkspaceConnection, folderPath?: string): Observable<SharePointItemDto[]> {
    return this.http
      .post<ApiEnvelope<SharePointItemDto[]>>(`${this.baseUrl}/workspace/browse`, { credentials: connection, folderPath }, { headers: this.headers })
      .pipe(map(this.unwrap<SharePointItemDto[]>()));
  }

  fetchFileBlob(connection: WorkspaceConnection, filePath: string): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/workspace/fetchfile`, { credentials: connection, filePath }, {
      headers: this.headers,
      responseType: 'blob',
    });
  }

  /** Query params for GET /workspace/file (path64 avoids slash issues in query strings). */
  private buildWorkspaceFileQueryParams(
    filePath: string,
    accessToken: string,
    connection?: WorkspaceConnection,
  ): URLSearchParams {
    const params = new URLSearchParams({
      path64: SharePointApiService.encodePath64(filePath),
      access_token: accessToken,
      apiVersion: this.env.apiVersion,
    });
    if (connection?.libraryName?.trim()) {
      params.set('libraryName', connection.libraryName.trim());
    }
    if (connection?.siteName?.trim()) {
      params.set('siteName', connection.siteName.trim());
    }
    return params;
  }

  resolveStreamingUrl(connection: WorkspaceConnection, filePath: string): Observable<string> {
    const appId = connection.applicationId?.trim();
    const apiSecret = connection.consumerSecret?.trim();
    if (appId && apiSecret) {
      return this.generateToken(appId, apiSecret).pipe(
        map((t) => {
          const params = this.buildWorkspaceFileQueryParams(filePath, t.accessToken, connection);
          return `${this.baseUrl}/workspace/file?${params.toString()}`;
        }),
      );
    }
    return of(this.buildStreamingUrl(connection, filePath));
  }

  buildStreamingUrl(connection: WorkspaceConnection, filePath: string): string {
    const params = new URLSearchParams();
    params.set('path64', SharePointApiService.encodePath64(filePath));
    if (connection.applicationId) {
      params.set('applicationId', connection.applicationId);
      if (connection.libraryName) params.set('libraryName', connection.libraryName);
      if (connection.siteName) params.set('siteName', connection.siteName);
    } else {
      if (connection.tenantId) params.set('tenantId', connection.tenantId);
      if (connection.clientId) params.set('clientId', connection.clientId);
      if (connection.clientSecret) params.set('clientSecret', connection.clientSecret);
      if (connection.hostName) params.set('hostName', connection.hostName);
      if (connection.siteName) params.set('siteName', connection.siteName);
      if (connection.libraryName) params.set('libraryName', connection.libraryName);
    }
    return `${this.baseUrl}/workspace/fetchfile?${params.toString()}`;
  }

  generateToken(applicationId: string, apiSecret: string): Observable<TokenResponse> {
    const headers = new HttpHeaders({
      'x-tpdi-api-version': this.env.apiVersion,
      'x-application-id': applicationId,
      'x-client-secret': apiSecret,
    });
    return this.http
      .post<ApiEnvelope<TokenResponse>>(`${this.baseUrl}/auth/token`, null, { headers })
      .pipe(map(this.unwrap<TokenResponse>()));
  }

  browseFolderByToken(token: string, folderPath = ''): Observable<SharePointItemDto[]> {
    const headers = new HttpHeaders({
      Authorization: `Bearer ${token}`,
      'x-tpdi-api-version': this.env.apiVersion,
    });
    const qs = folderPath ? `?path=${encodeURIComponent(folderPath)}` : '';
    return this.http
      .get<ApiEnvelope<SharePointItemDto[]>>(`${this.baseUrl}/workspace/browse${qs}`, { headers })
      .pipe(map(this.unwrap<SharePointItemDto[]>()));
  }

  fetchFileByToken(token: string, filePath: string, connection?: WorkspaceConnection): Observable<Blob> {
    const headers = new HttpHeaders({
      Authorization: `Bearer ${token}`,
      'x-tpdi-api-version': this.env.apiVersion,
      'Content-Type': 'application/json',
      ...(connection?.libraryName?.trim() ? { 'x-library-name': connection.libraryName.trim() } : {}),
      ...(connection?.siteName?.trim() ? { 'x-site-name': connection.siteName.trim() } : {}),
    });
    return this.http.post(`${this.baseUrl}/workspace/file`, { filePath }, {
      headers,
      responseType: 'blob',
    });
  }

  buildStreamingUrlByToken(token: string, filePath: string, connection?: WorkspaceConnection): string {
    const params = this.buildWorkspaceFileQueryParams(filePath, token, connection);
    return `${this.baseUrl}/workspace/file?${params.toString()}`;
  }
}

export interface TokenResponse {
  accessToken: string;
  tokenType: string;
  expiresInSeconds: number;
}
