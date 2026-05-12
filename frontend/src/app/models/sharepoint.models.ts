export interface AzureConfig {
  tenantId: string;
  clientId: string;
  clientSecret: string;
  hostName: string;
  sitePath: string;
  driveName: string;
}

export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
}

export interface SharePointSite {
  id: string;
  name: string;
  displayName?: string;
  webUrl?: string;
  hostName?: string;
}

export interface SharePointDrive {
  id: string;
  name: string;
  driveType?: string;
  webUrl?: string;
  description?: string;
}

export interface SharePointDriveItem {
  id: string;
  name: string;
  isFolder: boolean;
  size: number;
  mimeType?: string;
  lastModifiedDateTime?: string;
  webUrl?: string;
  path?: string;
  childCount?: number;
}

export interface SharePointFileMetadata {
  id: string;
  name: string;
  size: number;
  mimeType?: string;
  lastModifiedDateTime?: string;
  webUrl?: string;
  downloadUrl?: string;
  siteId?: string;
  driveId?: string;
  path?: string;
}

export interface BreadcrumbItem {
  name: string;
  path: string;
}
