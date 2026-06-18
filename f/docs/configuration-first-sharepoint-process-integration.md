# Configuration First + SharePoint — verified findings (single reference)

**Last verified:** June 15, 2026  
**Database audit:** `sqlcmd -S . -d SharepointPlugin -E` (Windows auth, local instance)  
**Scope:** Configuration First → Process (`/add-process`) only.

---

## 0. Executive summary

| Half | What it means | Status |
|------|---------------|--------|
| **First half (configuration)** | User picks SharePoint app/site/library/folder in wizard → values persisted with the process definition | **Built in frontend + API DTOs + save SP params** |
| **Second half (runtime)** | Scheduler / function app loads configs and **pulls files from SharePoint** on a timer | **Not built** — no `processTypeId = 6` branch in file-loading service, no runtime list SP for SharePoint on local DB |

**You only have the first half.** Saving a type-6 process stores *where* to look. Nothing in the backend yet goes to SharePoint on schedule the way blob (type 3) and shared location (type 2) do.

---

## 1. Database — live audit (PowerShell / sqlcmd)

Re-run anytime:

```powershell
$Server = '.'
$Database = 'SharepointPlugin'

function Invoke-DiSql([string]$Query) {
  sqlcmd -S $Server -d $Database -E -Q "SET NOCOUNT ON; $Query" -W -s '|'
}

Invoke-DiSql "SELECT processTypeId, processTypeName FROM dbo.di_info_processType WHERE active = 1 ORDER BY processTypeId"
Invoke-DiSql "SELECT name FROM sys.tables WHERE name IN ('di_flpConfiguration','di_flpSharePointSource','di_application','di_applicationsite') ORDER BY name"
Invoke-DiSql "SELECT PARAMETER_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.PARAMETERS WHERE SPECIFIC_NAME = 'commit_InsertFlpConfiguration' AND PARAMETER_NAME LIKE '%sharePoint%'"
Invoke-DiSql "SELECT name FROM sys.procedures WHERE name IN ('commit_InsertFlpConfiguration','commit_InsertFlpSharePointSource','sel_processConfigurationById','sel_flpSharePointSourceByConfigId','sel_flpConfigurationList','sel_GetProcessTypes') ORDER BY name"
```

### 1.1 Verified results (SharepointPlugin on `.`)

**Process type master — table `dbo.di_info_processType`**

| processTypeId | processTypeName |
|---------------|-----------------|
| 1 | Online |
| 2 | Offline - Shared Location |
| 3 | Offline - Blob Storage |
| **6** | **SharePoint Workspace** |

**SharePoint plugin tables (pre-existing, used by workspace browse during wizard)**

| Table | Rows (verified) | Purpose |
|-------|-----------------|--------|
| `dbo.di_application` | 1 | Registered SharePoint apps |
| `dbo.di_applicationsite` | 1 | Sites per app |
| `dbo.di_applicationtype` | (seeded) | App type lookup |
| `dbo.di_sharepointlogs` | — | Plugin audit |

**Process configuration main table — `dbo.di_flpConfiguration`**

Standard columns only (`flpConfigurationId`, `processTypeId`, `sourcePath`, `locationTypeId`, …).  
**Verified: no `sharePoint*` columns on this table in SharepointPlugin.**

**SharePoint source table — `dbo.di_flpSharePointSource` (added for type 6)**

| Column | Type |
|--------|------|
| `flpConfigurationId` | `nvarchar` (PK) |
| `sharePointApplicationId` | `uniqueidentifier` |
| `sharePointApplicationSiteId` | `uniqueidentifier` |
| `sharePointLibraryName` | `nvarchar(256)` |
| `sharePointFolderPath` | `nvarchar(512)` |
| `active` | `bit` |
| `createdOn` | `datetime2` |
| `modifiedOn` | `datetime2` |

Verified row count: **0** (no type-6 saves tested yet).

### 1.2 Stored procedures — verified existence

| Procedure | Exists on SharepointPlugin | Role |
|-----------|----------------------------|------|
| `dbo.sel_GetProcessTypes` | Yes | Wizard dropdown; includes type 6 |
| `dbo.commit_InsertFlpConfiguration` | Yes | Main save; has 4 `@sharePoint*` parameters |
| `dbo.commit_InsertFlpSharePointSource` | Yes | Upsert into `di_flpSharePointSource` when `processTypeId = 6` |
| `dbo.sel_flpSharePointSourceByConfigId` | Yes | Load SharePoint source by `flpConfigurationId` |
| `dbo.sel_processConfigurationById` | Yes | Edit/load wizard (multi result set) |
| `dbo.commit_InsertFlpFileConfiguration` | Yes | File rules (unchanged for SharePoint) |
| `dbo.commit_InsertConfigurationTableMapping` | Yes | DB mapping (unchanged) |
| **`dbo.sel_flpConfigurationList`** | **No (missing on local DB)** | **Runtime** — blob/shared use this; required for second half |

### 1.3 Save path — what actually happens on insert (verified from SP body)

When `commit_InsertFlpConfiguration` runs with `@processTypeId = 6`:

1. Inserts/updates `dbo.di_flpConfiguration` with `sourcePath = @sharePointFolderPath`
2. Calls `dbo.commit_InsertFlpSharePointSource` → writes `dbo.di_flpSharePointSource`

`commit_InsertFlpConfiguration` parameters (SharePoint slice):

```
@sharePointApplicationId     UNIQUEIDENTIFIER
@sharePointApplicationSiteId UNIQUEIDENTIFIER
@sharePointLibraryName       NVARCHAR(256)
@sharePointFolderPath        NVARCHAR(512)
```

### 1.4 Load path — **broken on current local DB**

`GET api/ProcessConfiguration/GetConfigurationDetailsById` → repository calls **`dbo.sel_processConfigurationById` only**.

That SP’s first result set selects `flp.sharePointApplicationId`, `flp.sharePointLibraryName`, etc. **from `di_flpConfiguration`**, but those columns **do not exist** on `di_flpSharePointSource`’s parent table in SharepointPlugin.

**`dbo.sel_flpSharePointSourceByConfigId` exists and is correct** (reads `di_flpSharePointSource` + JOINs `di_application` / `di_applicationsite`) but **the C# API never calls it.**

This is a concrete gap: **save can work; edit/load of SharePoint selection will fail or return empty until load is fixed.**

### 1.5 Production-oriented SQL in repo (for UAT/prod deploy)

These files define an **alternate** persistence shape (columns on `di_flpConfiguration` instead of side table):

| Repo file | What it defines |
|-----------|-----------------|
| `Integration_Project/Database/sharepoint_process_type_seed.sql` | Row in `di_info_processType` for id 6 |
| `Integration_Project/Database/sharepoint_process_configuration.sql` | `ALTER TABLE di_flpConfiguration ADD sharePointApplicationId, …` (four columns) |
| `Integration_Project/Database/commit_InsertFlpConfiguration.sql` | Production SP: persists SharePoint fields **on `di_flpConfiguration`** when `@processTypeId = 6` |
| `Integration_Project/Database/sel_processConfigurationById.sql` | Returns SharePoint fields from `di_flpConfiguration` + JOIN app/site |

**Local SharepointPlugin DB does not match the production SQL file yet** (side table vs columns). Pick **one** model for UAT/prod and align `sel_processConfigurationById` + API load with save.

---

## 2. How blob and shared location work (reference for second half)

### Type 2 — Shared location

| Step | Object |
|------|--------|
| Wizard fields | `serverLocationId`, `baseFolderName`, `sourceFolderLocation` → `processConfigurationForm` |
| Save SP | `dbo.commit_InsertFlpConfiguration` |
| Side table | `dbo.di_sharedLocationSourceServer` (`fileServerId`, `folderName`) |
| Main row | `di_flpConfiguration.sourcePath` = folder path, `locationTypeId = 2` |
| Edit load SP | `dbo.sel_processConfigurationById` |
| Runtime list SP | `dbo.sel_flpConfigurationList` `@processTypeId = 2` |
| Runtime service | `FileLoadingProcessConfigurationService.GetSharedFileLocationList` (SMB) |

### Type 3 — Blob storage

| Step | Object |
|------|--------|
| Wizard fields | `blobStorageAccount`, `blobContainerName`, `blobSourcePath` |
| Save SP | `dbo.commit_InsertFlpConfiguration` |
| Side table | `dbo.di_flpSourceStorageAccountInfo` |
| Main row | `di_flpConfiguration.sourcePath` = blob path, `locationTypeId = 1` |
| Edit load SP | `dbo.sel_processConfigurationById` |
| Runtime list SP | `dbo.sel_flpConfigurationList` `@processTypeId = 3` |
| Runtime service | `FileLoadingProcessConfigurationService.GetSourceLocationFilesFromBlobStorage` |

### Type 6 — SharePoint (today)

| Step | Object | Status |
|------|--------|--------|
| Wizard | `SharepointWorkspaceComponent` in config mode | Done (frontend) |
| Save SP | `commit_InsertFlpConfiguration` + `commit_InsertFlpSharePointSource` | Done (DB) |
| Side table | `di_flpSharePointSource` | Done (DB) |
| Main row | `sourcePath` = folder path | Done (DB) |
| Edit load SP | `sel_processConfigurationById` | **Misaligned / broken locally** |
| Alt load SP | `sel_flpSharePointSourceByConfigId` | Exists; **API not wired** |
| Runtime list SP | `sel_flpConfigurationList` | **Missing locally; not extended for SharePoint** |
| Runtime service | — | **Not implemented** |

---

## 3. Backend API — files changed for Configuration First

### 3.1 Process configuration (save / load) — **changed**

| File | Exact change |
|------|----------------|
| `Teleperformance.DataIngestion.Models/Enums/v1.0/ProcessTypeEnum.cs` | `SharePointWorkspace = 6` |
| `Teleperformance.DataIngestion.Models/DTOs/v1.0/FileConfiguration/InsertFlpConfigurationRequest.cs` | Properties: `SharePointApplicationId`, `SharePointApplicationSiteId`, `SharePointLibraryName`, `SharePointFolderPath` |
| `Teleperformance.DataIngestion.Models/DTOs/v1.0/FileConfiguration/ProcessConfigDetails.cs` | Same 4 + `sharePointApplicationName`, `sharePointSiteName` on load DTO |
| `Teleperformance.DataIngestion.DataAccess/Repository/v1.0/ProcessConfigurationRepository.cs` | `InsertFlpConfigurationDetails`: adds 4 `dynamicParameters` for `@sharePoint*`; `GetConfigurationDetailsById` still only calls `sel_processConfigurationById` |
| `Teleperformance.DataIngestion.DataAccess/Repository/v4.1/ProcessConfigurationRepositoryV4_1.cs` | Same 4 params on landing-layer insert path |

**Controller endpoints (unchanged routes, extended body):**

| Method | Route | SP chain |
|--------|-------|----------|
| POST | `api/ProcessConfiguration/InsertFlpConfigurationDetails` | `commit_InsertFlpConfiguration` → `commit_InsertFlpFileConfiguration` → `commit_InsertConfigurationTableMapping` |
| GET | `api/ProcessConfiguration/GetConfigurationDetailsById` | `sel_processConfigurationById` |
| GET | `api/ProcessConfiguration/GetProcessType` | `sel_GetProcessTypes` |

**Service:** `ProcessConfigurationService.InsertFlpConfigurationDetails` — **no SharePoint-specific logic**; passes request through to repository.

### 3.2 SharePoint plugin (browse during wizard) — **not changed for Configuration First**

Already existed; wizard workspace calls these:

| Controller | Endpoints |
|------------|-----------|
| `Controllers/v4.1/SharePointController.cs` | `GET api/applications/types`, `GET/POST api/applications`, `POST api/workspace/browse`, `POST api/workspace/fetchfile` |
| `Controllers/v4.1/SharePointUserController.cs` | User-context connect/browse |

| Service / repo | Path |
|----------------|------|
| `Teleperformance.DataIngestion.Sharepoint/Services/V1_0/SharePointPluginService.cs` | Graph browse, fetch file |
| `Teleperformance.DataIngestion.Sharepoint/Repositories/V1_0/SharePointPluginRepository.cs` | Plugin SPs: `sel_application`, `commit_application`, `sel_applicationsites`, etc. |

### 3.3 Runtime file loading — **not changed (second half)**

| File | Today |
|------|-------|
| `DataAccess/Services/v1.0/FileLoadingProcessConfigurationService.cs` | `GetProcessList(processTypeId)` → only Azure blob or on-prem branches; **no `processTypeId == 6`** |
| `DataAccess/Repository/v1.0/FileLoadingProcessConfigurationRepository.cs` | `FlpConfigurationDetails` → **`sel_flpConfigurationList`** |
| `Models/Entities/.../FlpConfiguration.cs` | **No SharePoint properties** |

---

## 4. Frontend — files responsible for Configuration First

### 4.1 New components

| Path | Responsibility |
|------|----------------|
| `src/app/new-process-configuration/configuration-first-sharepoint-tab/configuration-first-sharepoint-tab.component.ts` | Tab pane; `#sharepointWorkspace`; `selection()`, `validateSelection()` |
| `configuration-first-sharepoint-tab.component.html` | Embeds `<app-sharepoint-workspace [processConfigMode]="true" [filePickMode]="true">` |
| `configuration-first-sharepoint-tab.component.css` | Tab layout |
| `src/app/new-process-configuration/configuration-first-sharepoint-tab-nav/configuration-first-sharepoint-tab-nav.component.ts` | Left nav button when `processType == 6` |
| `configuration-first-sharepoint-tab-nav.component.html` | Nav markup |
| `configuration-first-sharepoint-tab-nav.component.css` | Nav styles |

### 4.2 Wizard glue (modified)

| File | What changed |
|------|----------------|
| `new-process-configuration/new-process-configuration.component.ts` | `@ViewChild('sharePointTab')`; `sharePointInitial*` from API on edit; `onProcessTypeChange` clears blob/shared for type 6; `onTabChanged` routes to SharePoint tab; `onSubmit` spreads `sharePointFileProcessFields(sharePointTab?.selection())`; `isSharePointSettingsTabValid()` |
| `new-process-configuration/new-process-configuration.component.html` | `<app-configuration-first-sharepoint-tab-nav>`; `@if` SharePoint tab; scheduler tab visible for types 2, 3, 6 |
| `app.module.ts` | Declares `ConfigurationFirstSharepointTabComponent`, `ConfigurationFirstSharepointTabNavComponent` |
| `app-routing.module.ts` | `/add-process` → `resolve: { lookups: OfflineModuleResolver }` |

### 4.3 Models and list (modified)

| File | What changed |
|------|----------------|
| `core/models/fileProcessConfig.ts` | `FileProcessConfig` + `FileConfigurationDetails` SharePoint optional fields |
| `process-config-list/process-config-list.component.html` | Detail rows when `processTypeId == 6` |
| `process-config-list/process-config-list.component.ts` | `SHAREPOINT_PROCESS_TYPE_ID`, `SP_INTEGRATION.listDetail` |

### 4.4 SharePoint module (shared; Configuration First uses these)

| File | Configuration First usage |
|------|---------------------------|
| `sharepoint/integration/configuration-first.sharepoint.ts` | `SHAREPOINT_PROCESS_TYPE_ID = 6`, tab IDs, `isSharePointProcessType()`, `sharePointFileProcessFields()`, `activateSharePointSettingsTab()` |
| `sharepoint/core/sharepoint.types.ts` | `ProcessConfigSharePointSelection` type |
| `sharepoint/core/sharepoint.messages.ts` | `SP_INTEGRATION.configurationFirst`, `SP_INTEGRATION.listDetail` |
| `sharepoint/sharepoint-workspace/sharepoint-workspace.component.ts` | `processConfigMode`; `processConfigurationSelection()`; `processConfigurationSelectionValid()`; `restoreProcessConfigSelection()` |
| `sharepoint/sharepoint-workspace/sharepoint-workspace.component.html` | Hides file-pick footer when `processConfigMode` |
| `sharepoint/sharepoint-workspace/sharepoint-workspace.component.css` | Wizard embed styles |
| `sharepoint/services/sharepoint-api.service.ts` | Browse/connect HTTP calls |

**Not Configuration First** (same repo, different flows): `file-upload/*`, `file-first/file-first-sharepoint-attach/*`, standalone `sharepoint/sharepoint.component.*`.

### 4.5 Resolver

`core/resolvers/offline-module.resolver.ts` — prefetches process types (includes 6 once DB seeded).  
`new-process-configuration.component.ts` reads `route.snapshot.data['lookups'] ?? route.snapshot.data['offline']`.

---

## 5. End-to-end flows

### 5.1 First half — create process (implemented)

```
User: /add-process → process type 6
  → SharePoint tab → workspace connects via plugin APIs
  → user browses folder
  → scheduler + DB tabs → Save
Frontend: sharePointFileProcessFields(selection) on POST body
API: InsertFlpConfigurationDetails → commit_InsertFlpConfiguration
DB: di_flpConfiguration (processTypeId=6, sourcePath=folder)
    + di_flpSharePointSource (app, site, library, folder)
```

### 5.2 First half — edit process (partially broken locally)

```
GET GetConfigurationDetailsById → sel_processConfigurationById
  → expects sharePoint* on di_flpConfiguration (missing locally)
  → should use sel_flpSharePointSourceByConfigId OR fix SP JOIN to di_flpSharePointSource
Frontend: sharePointInitial* → workspace restoreProcessConfigSelection()
```

### 5.3 Second half — scheduled ingest (not implemented)

```
Scheduler triggers function app
  → FileLoadingProcessConfigurationService.GetProcessList(6)
  → sel_flpConfigurationList (must return SharePoint fields)
  → NEW: resolve app credentials from di_application
  → NEW: SharePointPluginService.BrowseFolderAsync / FetchFileAsync
  → filter by search_string_in_file_name
  → UploadedFileDetails → existing ingest pipeline
```

Blob equivalent for comparison:

```
GetProcessList(3) → sel_flpConfigurationList @processTypeId=3
  → GetSourceLocationFilesFromBlobStorage(...)
```

---

## 6. What is left and how to build it

### Phase A — Fix load/save consistency (still first half)

**Problem:** Save writes `di_flpSharePointSource`; load reads non-existent columns on `di_flpConfiguration`.

**Option 1 (side table — matches local DB):**

1. Change `dbo.sel_processConfigurationById` result set 1 to `LEFT JOIN dbo.di_flpSharePointSource s ON s.flpConfigurationId = flp.flpConfigurationId` and select `s.sharePointApplicationId`, …
2. Or in `ProcessConfigurationRepository.GetConfigurationDetailsById`, after main read, if `processTypeId == 6`, call `sel_flpSharePointSourceByConfigId` and map onto `ProcessConfigDetail`.

**Option 2 (columns on main table — matches `commit_InsertFlpConfiguration.sql` in repo):**

1. Run `sharepoint_process_configuration.sql` on target DB.
2. Deploy production `commit_InsertFlpConfiguration.sql` / `sel_processConfigurationById.sql`.
3. Stop using `di_flpSharePointSource` for new saves.

**Verify:** Save type-6 process → `SELECT * FROM di_flpSharePointSource` (or columns on `di_flpConfiguration`) → open edit in wizard → workspace restores app/site/library/folder.

### Phase B — Runtime (second half)

| # | Work | Where |
|---|------|-------|
| B1 | Create or extend **`dbo.sel_flpConfigurationList`** to return SharePoint fields for `processTypeId = 6` (JOIN `di_flpSharePointSource` or main-table columns) | SQL Server |
| B2 | Add to **`FlpConfiguration`** entity: `sharePointApplicationId`, `sharePointApplicationSiteId`, `sharePointLibraryName`, `sharePointFolderPath` | `Models/Entities/.../FlpConfiguration.cs` |
| B3 | In **`FileLoadingProcessConfigurationService.GetProcessList`**, when `flp.processTypeId == 6`, call new method e.g. `GetSharePointSourceFiles(...)` | Service |
| B4 | Implement **`GetSharePointSourceFiles`**: load app from `di_application` by id; build `WorkspaceCredentials`; `BrowseFolderAsync` for folder; match files to `search_string_in_file_name`; `FetchFileAsync` per file; `UploadedFileDetails` | Service + `SharePointPluginService` |
| B5 | Mirror in **`GetProcessListToLandingLayer`** if landing-layer offline must support SharePoint | v4.1 service |
| B6 | Optional: skip `di_flpSourceStorageAccountInfo` insert in `commit_InsertFlpConfiguration` when `@processTypeId = 6` (today type 6 still gets empty blob side row because `locationTypeId = 1`) | SP |

**Copy pattern from blob** (`FileLoadingProcessConfigurationService.cs` lines ~98–103): same loop, different source resolver.

### Phase C — Test plan

1. `sqlcmd`: type 6 in `di_info_processType`.
2. Wizard save → row in `di_flpConfiguration` + `di_flpSharePointSource`.
3. Wizard edit → SharePoint fields round-trip.
4. Manual or scheduled call to `GetProcessList(6)` → lists files from configured folder.
5. File reaches same ingest path as blob upload.

---

## 7. API payload (type 6 save)

```json
{
  "processTypeId": 6,
  "locationTypeId": 1,
  "sharePointApplicationId": "<guid>",
  "sharePointApplicationSiteId": "<guid>",
  "sharePointLibraryName": "Documents",
  "sharePointFolderPath": "/Reports/2026",
  "serverLocationId": 1,
  "baseFolderName": "",
  "sourceFolderLocation": "",
  "blobStorageAccount": 1,
  "blobContainerName": "",
  "blobSourcePath": "",
  "scheduledId": 6,
  "fileConfigurations": [ "..." ],
  "configurationTableMappings": [ "..." ]
}
```

---

## 8. Quick answers

| Question | Answer |
|----------|--------|
| Is only the first half done? | **Yes.** Configure + save path exists; runtime ingest does not. |
| New table for SharePoint? | **On local DB:** `dbo.di_flpSharePointSource`. **Production SQL in repo:** four columns on `dbo.di_flpConfiguration` (not deployed locally). |
| New SPs? | **`commit_InsertFlpSharePointSource`**, **`sel_flpSharePointSourceByConfigId`** on local DB. Extended **`commit_InsertFlpConfiguration`**, **`sel_processConfigurationById`**. |
| Does API call SharePoint on save? | No — save is SQL only. Plugin APIs are for **wizard browse** only. |
| What breaks today on local? | **Edit load** (`sel_processConfigurationById` vs side table). **Runtime** (`sel_flpConfigurationList` missing + no service branch). |
| One file for all touch points? | **This file** — §1 DB audit, §3 backend, §4 frontend, §6 remaining work. |
