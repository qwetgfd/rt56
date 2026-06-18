export const environment = {
  production: false,

  // Production: set apiBaseUrl / apiEndpoint / sharepointApiBaseUrl from deployment config.
  apiBaseUrl: '',
  apiEndpoint: '',
  // #region Sharepoint Workspace - AY
  sharepointApiBaseUrl: '',
  // #endregion

  graphApiEndpoint: 'https://graph.microsoft.com/v1.0',
  appId: 'YOUR_APP_ID',
  clientKey: 'YOUR_CLIENT_KEY',
  isAdmin: 'false',
  refreshInterval: 10000,
  userFullName: '',
  userId: '',

  // #region Sharepoint Workspace - AY
  apiVersion: '1.0',
  ownerKey: 'system',
  defaultLibraryName: 'Documents',
  tenantName: '',
  clientId: '',
  authority: '',
  hostName: '',
  siteName: '',
  libraryName: 'Documents',
  userGraphScopes: [
    'https://graph.microsoft.com/User.Read',
    'https://graph.microsoft.com/Files.Read',
    'https://graph.microsoft.com/Sites.Read.All',
  ],
  // #endregion
  // ============================================================
};