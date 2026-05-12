# SharePoint Azure Configuration Guide

This plugin exposes a small public API for SharePoint file work. The consumer should not call site lookup, drive lookup, and stream APIs separately. The plugin handles those steps internally.

## Public APIs

```http
POST /api/SharePoint/GenerateAccessToken
POST /api/SharePoint/StreamFile
POST /api/SharePoint/DownloadFile
POST /api/SharePoint/GetFileInfo
POST /api/SharePoint/CreateFolder
```

## Common Values Set In Code

`Program.cs` sets common values directly:

```csharp
Environment.SetEnvironmentVariable("sharepointGraphEndpoint", "https://graph.microsoft.com");
Environment.SetEnvironmentVariable("sharepointGraphApiVersion", "v1.0");
Environment.SetEnvironmentVariable("sharepointTokenEndpointTemplate", "https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token");
Environment.SetEnvironmentVariable("sharepointGraphScope", "https://graph.microsoft.com/.default");
Environment.SetEnvironmentVariable("sharepointGrantType", "client_credentials");
Environment.SetEnvironmentVariable("sharepointDefaultDriveName", "Documents");
Environment.SetEnvironmentVariable("sharepointTimeoutSeconds", "300");
Environment.SetEnvironmentVariable("sharepointFileContentType", "application/octet-stream");
Environment.SetEnvironmentVariable("hostName", "nocompany102.sharepoint.com");
```

## Required Headers

Every consuming application should pass its own Azure and SharePoint values in headers:

```http
tenantId: {tenantId}
clientId: {clientId}
clientSecret: {clientSecret}
sitePath: sites/Sharepoint_PluginTest
```

Optional headers:

```http
hostName: nocompany102.sharepoint.com
driveId: {driveId}
driveName: Documents
graphScope: https://graph.microsoft.com/.default
grantType: client_credentials
tokenEndpoint: https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token
```

## Azure Setup

1. Open `https://portal.azure.com`.
2. Open `Microsoft Entra ID`.
3. Open `App registrations`.
4. Select `New registration`.
5. Name the app, for example `SharePoint Plugin App`.
6. Select `Accounts in this organizational directory only`.
7. Leave redirect URI empty.
8. Select `Register`.
9. Copy `Application (client) ID` as `clientId`.
10. Copy `Directory (tenant) ID` as `tenantId`.
11. Open `Certificates & secrets`.
12. Create a new client secret.
13. Copy the secret `Value` as `clientSecret`.

## Permissions

For quick testing:

1. Open the app registration.
2. Select `API permissions`.
3. Select `Add a permission`.
4. Select `Microsoft Graph`.
5. Select `Application permissions`.
6. Add `Sites.ReadWrite.All`.
7. Select `Grant admin consent`.

For production least privilege, use `Sites.Selected` and grant the app access to the specific SharePoint site.

## SharePoint Values

Example SharePoint site:

```text
https://nocompany102.sharepoint.com/sites/Sharepoint_PluginTest
```

From this URL:

```text
hostName = nocompany102.sharepoint.com
sitePath = sites/Sharepoint_PluginTest
```

If the file is in:

```text
Documents/OpportunityTestFolder/demo.pdf
```

Pass only the library-relative path in the body:

```text
OpportunityTestFolder/demo.pdf
```

## Generate Access Token

```http
POST /api/SharePoint/GenerateAccessToken
tenantId: {tenantId}
clientId: {clientId}
clientSecret: {clientSecret}
```

The plugin posts internally to:

```http
https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token
```

with:

```text
client_id={clientId}
client_secret={clientSecret}
scope=https://graph.microsoft.com/.default
grant_type=client_credentials
```

## Stream Any File

```http
POST /api/SharePoint/StreamFile
Content-Type: application/json
api-version: 1.0
tenantId: {tenantId}
clientId: {clientId}
clientSecret: {clientSecret}
sitePath: sites/Sharepoint_PluginTest
```

Body:

```json
{
  "filePath": "OpportunityTestFolder/demo.pdf"
}
```

The plugin internally resolves the site, resolves the `Documents` drive by default, and streams the Graph `/content` response. The caller does not need to calculate byte ranges. Use the same API for PDF, text, Word, Excel, image, audio, and video files.

## Download File

```http
POST /api/SharePoint/DownloadFile
```

Body:

```json
{
  "filePath": "OpportunityTestFolder/demo.pdf"
}
```

Use `StreamFile` for large files. Use `DownloadFile` only when the caller needs the whole file loaded as bytes.

## Get File Info

```http
POST /api/SharePoint/GetFileInfo
```

Body:

```json
{
  "filePath": "OpportunityTestFolder/demo.pdf"
}
```

This returns metadata like file id, name, size, MIME type, modified date, and web URL.

## Create Folder

```http
POST /api/SharePoint/CreateFolder
```

Body:

```json
{
  "folderName": "OpportunityTestFolder",
  "conflictBehavior": "rename"
}
```

Create inside another folder:

```json
{
  "folderName": "ChildFolder",
  "parentFolderPath": "OpportunityTestFolder",
  "conflictBehavior": "rename"
}
```

## Troubleshooting

`401 Unauthorized` usually means tenant id, client id, client secret, or token permissions are wrong.

`403 Forbidden` usually means Graph application permissions or admin consent are missing.

`404 Not Found` usually means host name, site path, drive name, or file path is wrong.

For default document libraries, do not include `Documents/` in `filePath`.
