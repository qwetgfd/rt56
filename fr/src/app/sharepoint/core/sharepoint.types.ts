export type ApplicationTypeCode = 'tp_internal' | 'tp_external' | 'custom';

export interface ApiEnvelope<T> {
  success: boolean;
  message: string;
  data: T;
}

export interface ApplicationTypeDto {
  applicationTypeId: number;
  code: string;
  displayName: string;
  description?: string;
}

export interface ApplicationDto {
  applicationId: string;
  applicationTypeCode: string;
  applicationTypeName: string;
  ownerKey: string;
  displayName: string;
  tenantId: string;
  clientId: string;
  clientSecret: string;
  consumerClientId?: string;
  consumerSecret?: string;
  hostName: string;
  siteName?: string;
  libraryName?: string;
  notes?: string;
  createdOn: string;
  modifiedOn?: string;
}

export interface SharePointLibraryDto {
  id: string;
  name: string;
  description?: string;
  webUrl?: string;
}

export interface SharePointItemDto {
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

export interface WorkspaceConnection {
  applicationId?: string | null;
  tenantId?: string;
  clientId?: string;
  clientSecret?: string;
  consumerClientId?: string;
  consumerSecret?: string;
  hostName?: string;
  siteName?: string;
  libraryName?: string;
}

export type FileKind =
  | 'video' | 'audio' | 'pdf' | 'word' | 'excel' | 'csv'
  | 'powerpoint' | 'text' | 'image' | 'folder' | 'unknown';
