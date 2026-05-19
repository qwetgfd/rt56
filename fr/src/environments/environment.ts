/** Frontend shell config only — SharePoint defaults come from GET /api/SharePoint/config. */
export const environment = {
  production: false,
  autoConnect: true,
  apiBaseUrl: 'https://localhost:54724/api/SharePoint',
  apiVersion: '1.0',
  registeredApplicationsStorageKey: 'sp-registered-applications',
  curlPlaceholderFilePath: 'path/to/file.ext',
  curlOutputStreamFileName: './streamed-file',
  curlOutputDownloadFileName: './download.bin',
};
