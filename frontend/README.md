# SharePoint File Browser — Angular Frontend

A standalone Angular 17 frontend for browsing SharePoint sites, folders, and files via your .NET backend API.

## Prerequisites

- Node.js 18+ and npm
- Angular CLI (`npm install -g @angular/cli`)
- Your .NET backend running (default: `https://localhost:7071`)

## Quick Start

### 1. Install dependencies

```bash
cd frontend
npm install
```

### 2. Start the Angular dev server

```bash
npm start
```

This runs `ng serve` with the proxy config so all `/api` calls are forwarded to your .NET backend at `https://localhost:7071`. The app opens at `http://localhost:4200`.

### 3. Configure your Azure credentials

On first load, switch to the **Configuration** tab and enter:

| Field | Description |
|-------|-------------|
| **Tenant ID** | Azure AD tenant ID (GUID) |
| **Client ID** | App registration client ID (GUID) |
| **Client Secret** | App registration client secret |
| **SharePoint Host Name** | e.g. `yourcompany.sharepoint.com` |
| **Site Path** | e.g. `sites/YourSiteName` |
| **Drive Name** | Default: `Documents` |

Click **Save Configuration**. Values are stored in your browser's localStorage.

### 4. Browse!

Switch to the **Browser** tab. The app will:

1. Resolve the site from the host name + site path
2. List available drives (e.g. Documents, Site Assets)
3. Show root-level folders and files
4. Let you click into any folder to drill down
5. Show breadcrumb navigation for easy back-tracking
6. Preview images, PDFs, text, audio, and video files
7. Download any file
8. Create new folders

## How It Works

```
Angular (localhost:4200)
  │
  │  proxy.conf.json  ──►  /api/*  ──►  .NET Backend (localhost:7071)
  │
  ├── Config tab     →  Saves credentials to localStorage
  ├── Browser tab    →  Calls GetSite → ListDrives → ListChildren (recursive)
  ├── File preview   →  StreamFile (images, PDF, text, media)
  └── File download  →  DownloadFile
```

### API Endpoints Used

| Angular Call | Backend Endpoint | Purpose |
|---|---|---|
| `getSite()` | `POST /api/SharePoint/GetSite` | Resolve site from host + path |
| `listDrives()` | `POST /api/SharePoint/ListDrives` | List document libraries |
| `listChildren()` | `POST /api/SharePoint/ListChildren` | List files + folders in a path |
| `streamFile()` | `POST /api/SharePoint/StreamFile` | Preview file content |
| `downloadFile()` | `POST /api/SharePoint/DownloadFile` | Download file as blob |
| `createFolder()` | `POST /api/SharePoint/CreateFolder` | Create a new folder |

All calls pass `tenantId`, `clientId`, `clientSecret`, `hostName`, `sitePath`, and `driveName` as HTTP headers.

## Project Structure

```
frontend/
├── angular.json                    # Angular CLI config
├── package.json                    # Dependencies
├── proxy.conf.json                 # Dev proxy → .NET backend
├── tsconfig.json                   # TypeScript config
└── src/
    ├── index.html                  # Entry HTML
    ├── main.ts                     # Bootstrap
    ├── styles.css                  # Global styles
    └── app/
        ├── app.component.ts        # Root component (tabs + header)
        ├── app.component.html
        ├── app.component.css
        ├── components/
        │   ├── config-form/        # Azure credentials form
        │   │   ├── config-form.component.ts
        │   │   ├── config-form.component.html
        │   │   └── config-form.component.css
        │   └── file-browser/       # Folder browser + file preview
        │       ├── file-browser.component.ts
        │       ├── file-browser.component.html
        │       └── file-browser.component.css
        ├── models/
        │   └── sharepoint.models.ts    # TypeScript interfaces
        └── services/
            ├── config.service.ts       # localStorage credential management
            └── sharepoint.service.ts   # HTTP calls to .NET backend
```

## Build for Production

```bash
ng build --configuration production
```

Output goes to `dist/sharepoint-plugin-frontend/`.

## Troubleshooting

- **"Azure configuration is required"** → Go to the Configuration tab and fill in your credentials
- **CORS errors** → Make sure your .NET backend is running and the CORS policy in `Program.cs` includes `http://localhost:4200`
- **401 Unauthorized** → Check your tenantId, clientId, and clientSecret
- **403 Forbidden** → Ensure your Azure app has `Sites.ReadWrite.All` permission with admin consent
- **404 Not Found** → Verify hostName, sitePath, and driveName values
- **File locked build error** → Stop the running .NET process before rebuilding
