import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import {
  ApiResponse,
  SharePointSite,
  SharePointDrive,
  SharePointDriveItem,
  SharePointFileMetadata
} from '../models/sharepoint.models';
import { ConfigService } from './config.service';

@Injectable({
  providedIn: 'root'
})
export class SharePointService {
  private readonly baseUrl = 'https://localhost:54724/api/SharePoint';

  constructor(
    private http: HttpClient,
    private configService: ConfigService
  ) {}

  private getHeaders(): HttpHeaders {
    const headers = this.configService.getHeaders();
    let httpHeaders = new HttpHeaders();
    for (const [key, value] of Object.entries(headers)) {
      httpHeaders = httpHeaders.set(key, value);
    }
    return httpHeaders;
  }

  private post<T>(action: string, body: object = {}): Observable<ApiResponse<T>> {
    if (!this.configService.isConfigured()) {
      return throwError(() => new Error('Azure configuration is required. Please configure your credentials first.'));
    }
    return this.http.post<ApiResponse<T>>(
      `${this.baseUrl}/${action}`,
      body,
      { headers: this.getHeaders() }
    ).pipe(
      catchError(error => {
        let message = 'An error occurred';
        if (error.error?.message) {
          message = error.error.message;
        } else if (error.message) {
          message = error.message;
        }
        return throwError(() => new Error(message));
      })
    );
  }

  getSite(): Observable<ApiResponse<SharePointSite>> {
    return this.post<SharePointSite>('GetSite');
  }

  listDrives(): Observable<ApiResponse<SharePointDrive[]>> {
    return this.post<SharePointDrive[]>('ListDrives');
  }

  listChildren(folderPath?: string): Observable<ApiResponse<SharePointDriveItem[]>> {
    return this.post<SharePointDriveItem[]>('ListChildren', { folderPath: folderPath ?? '' });
  }

  getFileInfo(filePath: string): Observable<ApiResponse<SharePointFileMetadata>> {
    return this.post<SharePointFileMetadata>('GetFileInfo', { filePath });
  }

  downloadFile(filePath: string): Observable<Blob> {
    if (!this.configService.isConfigured()) {
      return throwError(() => new Error('Azure configuration is required.'));
    }
    return this.http.post(
      `${this.baseUrl}/DownloadFile`,
      { filePath },
      { headers: this.getHeaders(), responseType: 'blob' }
    );
  }

  streamFile(filePath: string): Observable<Blob> {
    if (!this.configService.isConfigured()) {
      return throwError(() => new Error('Azure configuration is required.'));
    }
    return this.http.post(
      `${this.baseUrl}/StreamFile`,
      { filePath },
      { headers: this.getHeaders(), responseType: 'blob' }
    );
  }

  getInlineStreamUrl(filePath: string): string {
    const cfg = this.configService.getConfig();
    if (!cfg) {
      throw new Error('Azure configuration is required.');
    }

    const params = new URLSearchParams({
      filePath,
      tenantId: cfg.tenantId,
      clientId: cfg.clientId,
      clientSecret: cfg.clientSecret,
      hostName: cfg.hostName,
      sitePath: cfg.sitePath,
      driveName: cfg.driveName || 'Documents'
    });

    return `${this.baseUrl}/StreamFileInline?${params.toString()}`;
  }

  createFolder(folderName: string, parentFolderPath?: string): Observable<ApiResponse<any>> {
    return this.post('CreateFolder', {
      folderName,
      parentFolderPath: parentFolderPath ?? '',
      conflictBehavior: 'rename'
    });
  }
}
