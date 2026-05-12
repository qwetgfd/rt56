# SharePoint Graph Streaming Steps

Use `SHAREPOINT_AZURE_CONFIGURATION_GUIDE.md` for full Azure and SharePoint setup.

## Main Flow

Consumers should call one endpoint for streaming:

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

The plugin internally:

1. Generates the Microsoft Graph access token.
2. Resolves the SharePoint site.
3. Resolves the document library drive.
4. Calls Graph `/content`.
5. Streams the file to the caller.

## Other Public APIs

```http
POST /api/SharePoint/GenerateAccessToken
POST /api/SharePoint/DownloadFile
POST /api/SharePoint/GetFileInfo
POST /api/SharePoint/CreateFolder
```

These are the only public API endpoints intended for consumers.
