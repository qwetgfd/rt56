# SharePoint Configuration First — Analysis & Implementation Status

**Date:** June 15, 2026  
**Scope:** Configuration First → Process (`/add-process`, `NewProcessConfigurationComponent`)  
**Spec:** [configuration-first-sharepoint-process-integration.md](./configuration-first-sharepoint-process-integration.md)

---

## Executive summary

| Area | Status before this work |
|------|-------------------------|
| SharePoint plugin API (`/api/applications/*`, `/api/workspace/*`) | **Done** — controllers, service, DB tables/SPs in `Integration_Project/Database/` |
| SharePoint workspace UI (`SharepointWorkspaceComponent`) | **Done** — used by File First and standalone SharePoint page |
| Process type 6 in master data | **Not done** |
| `di_flpConfiguration` SharePoint columns | **Not done** |
| `commit_InsertFlpConfiguration` / `sel_processConfigurationById` SharePoint fields | **Not done** |
| API DTOs + repository mapping | **Not done** |
| Configuration First wizard SharePoint tab | **Not done** (CSS existed; no HTML/tab wiring) |
| Resolver key mismatch (`offline` vs `lookups`) | **Bug** — wizard prefetched data never applied from resolver |
| Runtime file load for SharePoint process type | **Out of scope** (later phase) |

**Local dev connection string:** Key Vault bypassed in `Program.cs` and `DapperService.cs`; uses `connectionstring` env var → `SharepointPlugin` database.

**Important:** Local `SharepointPlugin` DB currently has only the 5 SharePoint plugin tables. Full wizard save/lookups require `DataIngestionV2` schema (or a combined database).

---

## 1. Connection string references

| Location | Secret / source | Role | Local dev status |
|----------|-----------------|------|------------------|
| `Program.cs` | `TPDataIngestionV2ConnectionString` | NLog `DecryptedConnectionString` | Bypassed → env var |
| `DapperService.cs` | `TPDataIngestionV2ConnectionString` | Default SQL for all DI repos | Bypassed → env var |
| `SharePointPluginRepository.cs` | `connectionstring` env var | SharePoint plugin SPs | Already local |
| `DapperService` overload with `keyVaultName` | Per-mapping secret | Customer DB at runtime | Unchanged |
| `FileLoadingProcessService` (v1) | `DatabaseConnectionSecret` | Runtime load | Unchanged |
| Blob temp upload services | `TPTempAzureStorageAccount*` | Azure Blob, not SQL | Unchanged |

---

## 2. Tables

### SharePoint plugin (`Integration_Project/Database/Tables.sql`)

| Table |
|-------|
| `di_applicationtype` |
| `di_application` |
| `di_applicationsite` |
| `di_application_usage` |
| `di_sharepointlogs` |

### Process Configuration workflow (production `DataIngestionV2`)

Core: `di_flpConfiguration`, `di_flpFileConfiguration`, `di_configurationTableMapping`, `di_databaseConfiguration`  
Scheduler: `di_flpSchedulerConfiguration`, `di_customSchedulerDetails`, `di_processScheduler`  
Source/dest: `di_flpSourceStorageAccountInfo`, `di_flpDestinationStorageAccountInfo`, `di_sharedLocationSourceServer`, `di_databricksDestinationStorageAccountInfo`  
Lookups: `di_info_processType`, `di_info_storageAccountDetails`, `di_info_fileServerDetails`, `di_info_schedulerType`, `di_weekDays`, `di_dataSliceAPIConfig`  
Security: `di_flpConfigurationSecurityGroupMapping`, `di_info_securityGroup`

**New (SharePoint only):** nullable columns on `di_flpConfiguration` — `sharePointApplicationId`, `sharePointApplicationSiteId`, `sharePointLibraryName`, `sharePointFolderPath`

---

## 3. Stored procedures

### SharePoint plugin (versioned in `Database/StoredProcedures.sql`)

`sel_applicationtypes`, `sel_application`, `sel_applicationbyid`, `commit_application`, `sel_applicationsites`, `commit_applicationsites`, `commit_applicationdelete`, `commit_sharepointlogs`

### Process Configuration (partially in `Database/`)

| SP | File |
|----|------|
| `sel_GetProcessTypes` | `sel_GetProcessTypes.sql` |
| `sel_storageAccountDetails` | `sel_storageAccountDetails.sql` |
| `sel_fileServerDetails` | `sel_fileServerDetails.sql` |
| `sel_weekDays` | `sel_weekDays.sql` |
| `sel_dataSliceAPIConfig` | `sel_dataSliceAPIConfig.sql` |
| `commit_InsertFlpConfiguration` | `commit_InsertFlpConfiguration.sql` |
| `sel_processConfigurationById` | `sel_processConfigurationById.sql` |

Save chain also calls (not in `Database/` folder): `commit_InsertFlpFileConfiguration`, `commit_InsertConfigurationTableMapping`, `commit_flpSecurityGroupMapping`, `commit_SecurityGroupIdForUser`, `commit_customSchedulerDetailsV2`, `commit_SecurityGroup`, `sel_flpRuleSetsByConfigId`, `funSchdulerCalculateNextRun`

---

## 4. SP → table dependencies (Process Configuration path)

| SP | Tables |
|----|--------|
| `sel_GetProcessTypes` | `di_info_processType` |
| `commit_InsertFlpConfiguration` | `di_flpConfiguration` (+ scheduler, source/dest, security mapping SPs) |
| `sel_processConfigurationById` | 15+ tables; see `sel_processConfigurationById.sql` |

---

## 5. APIs used by Configuration First → Process

### Resolver prefetch (`OfflineModuleResolver`)

- `GET api/ProcessConfiguration/GetStorageAccountDetails`
- `GET api/ProcessConfiguration/GetAllDataTimeFormats`
- `GET api/ProcessConfiguration/GetValidFileExtensions`
- `GET api/ProcessConfiguration/GetDSConfiguration?id=1`
- `GET api/ProcessConfiguration/GetProcessType`
- `GET api/ProcessConfiguration/GetFileServerDetails`
- `GET api/ProcessConfiguration/GetWeekDayName`
- `GET api/ProcessConfiguration/GetSchedulerType`

### Wizard runtime

- Regions, sub-regions, clients, database names, process name check, security groups, frequency hours
- `GET api/ProcessConfiguration/GetConfigurationDetailsById` (edit)
- `POST api/ProcessConfiguration/InsertFlpConfigurationDetails` (save)

### SharePoint workspace (unchanged plugin)

- `GET api/applications/types`, `GET/POST api/applications`
- `POST api/workspace/browse`, `POST api/workspace/fetchfile`

---

## 6. Minimum changes for SharePoint (implemented in this branch)

1. `INSERT` process type 6 → `di_info_processType`
2. Four nullable columns on `di_flpConfiguration`
3. Extend `commit_InsertFlpConfiguration` + `sel_processConfigurationById`
4. `ProcessTypeEnum.SharePointWorkspace = 6`
5. `InsertFlpConfigurationRequest` + `ProcessConfigDetail` + repository params
6. Fix resolver key; wizard SharePoint tab; save/load mapping
7. `SharepointWorkspaceComponent.processConfigMode` for folder selection (no file pick)

---

## 7. Implementation checklist

| # | Task | Status |
|---|------|--------|
| 1 | Process type seed + enum | Done |
| 2 | FLP table columns | Done (`sharepoint_process_configuration.sql`) |
| 3 | SP extensions | Done |
| 4 | API DTOs + repository | Done |
| 5 | Resolver fix | Done (`lookups` key in routing) |
| 6 | Wizard SharePoint tab | Done |
| 7 | `onProcessTypeChange` / `onTabChanged` / `onSubmit` | Done |
| 8 | `FileProcessConfig` + list detail | Done |
| 9 | Runtime file fetch | Deferred |

---

## 8. Files touched by implementation

| Layer | Files |
|-------|-------|
| Database | `sharepoint_process_configuration.sql`, `commit_InsertFlpConfiguration.sql`, `sel_processConfigurationById.sql` |
| Backend | `ProcessTypeEnum.cs`, `InsertFlpConfigurationRequest.cs`, `ProcessConfigDetails.cs`, `ProcessConfigurationRepository.cs`, `ProcessConfigurationRepositoryV4_1.cs` |
| Frontend | `offline-module.resolver.ts`, `fileProcessConfig.ts`, `new-process-configuration.*`, `sharepoint-workspace.component.*`, `process-config-list.component.html`, `app.module.ts` |
