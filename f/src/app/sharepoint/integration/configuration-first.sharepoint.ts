import type { ProcessConfigSharePointSelection } from '../core/sharepoint.types';
import { PROCESS_TYPE_SHAREPOINT_WORKSPACE } from '../core/sharepoint.types';

export const SHAREPOINT_PROCESS_TYPE_ID = PROCESS_TYPE_SHAREPOINT_WORKSPACE;
export const SHAREPOINT_SETTINGS_TAB_ID = 'sharepoint-settings-tab';
export const SHAREPOINT_SETTINGS_TAB_PANE_ID = 'sharepoint-settings-tab-pane';

/** Mirrors backend SourceLocationTypeEnum; SharePoint is the proposed extension value. */
export const SOURCE_LOCATION_AZURE = 1;
export const SOURCE_LOCATION_ON_PREM = 2;
export const SOURCE_LOCATION_SFTP = 3;
export const SOURCE_LOCATION_SHAREPOINT = 4;

export const PROCESS_TYPE_SHARED_LOCATION = 2;
export const PROCESS_TYPE_BLOB_STORAGE = 3;

export interface SharePointFileProcessFields {
  sharePointApplicationId: string;
  sharePointApplicationSiteId: string;
  sharePointLibraryName: string;
  sharePointFolderPath: string;
}

export function isSharePointProcessType(processType: unknown): boolean {
  return processType === SHAREPOINT_PROCESS_TYPE_ID || processType === String(SHAREPOINT_PROCESS_TYPE_ID);
}

/** Map wizard process type to persisted locationTypeId (runtime provider key). */
export function resolveLocationTypeId(processType: unknown): number {
  const type = Number(processType);
  if (type === PROCESS_TYPE_SHARED_LOCATION) {
    return SOURCE_LOCATION_ON_PREM;
  }
  if (isSharePointProcessType(processType)) {
    return SOURCE_LOCATION_SHAREPOINT;
  }
  return SOURCE_LOCATION_AZURE;
}

export function activateSharePointSettingsTab(): void {
  document.getElementById(SHAREPOINT_SETTINGS_TAB_ID)?.click();
}

export function sharePointFileProcessFields(
  selection: ProcessConfigSharePointSelection | null | undefined,
): SharePointFileProcessFields {
  return {
    sharePointApplicationId: selection?.sharePointApplicationId ?? '',
    sharePointApplicationSiteId: selection?.sharePointApplicationSiteId ?? '',
    sharePointLibraryName: selection?.sharePointLibraryName ?? '',
    sharePointFolderPath: selection?.sharePointFolderPath ?? '',
  };
}
