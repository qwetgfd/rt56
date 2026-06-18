# SharePoint Configuration Flow — Database & API Deliverables

**Date:** 2026-06-15  
**Local database:** `SharepointPlugin` (set via environment variable or KeyVault)  
**Scope:** Configuration First → Process (`/add-process`, process type **6**)

---

## Executive summary

| Area | Status on `SharepointPlugin` |
|------|------------------------------|
| Authoritative plugin scripts (`Tables.sql`, `StoredProcedures.sql`, `SeedData.sql`) | **Validated OK** — all 5 tables + 8 SPs deploy and execute |
| Lookup scripts in `Database/` folder | **Validated OK** after `local_lookup_tables.sql` + `local_lookup_seed.sql` |
| SharePoint side-table approach (`di_flpSharePointSource` + new SPs) | **Deployed OK** |
| `sharepoint_process_configuration.sql` (ALTER `di_flpConfiguration`) | **Fails locally** — `di_flpConfiguration` does not exist |
| Modified `commit_InsertFlpConfiguration.sql` / `sel_processConfigurationById.sql` | **Deploy as CREATE only** — **EXEC fails** without full `DataIngestionV2` schema |
| Full wizard save/load | **Blocked locally** until process-configuration schema exists |

**Policy alignment:** Existing stored procedures must not be rewritten. SharePoint-specific persistence should use **additive** objects (`di_flpSharePointSource`, `commit_InsertFlpSharePointSource`, `sel_flpSharePointSourceByConfigId`) and API-layer orchestration. The current modifications to `commit_InsertFlpConfiguration.sql` and `sel_processConfigurationById.sql` conflict with this policy and should be reverted when implementing the side-table approach.

---

## 1. Stored procedures involved in the SharePoint configuration flow

### 1A. SharePoint plugin (application registration & workspace browse)

| Stored procedure | Script source | Purpose |
|------------------|---------------|---------|
| `sel_applicationtypes` | `StoredProcedures.sql` | Application type dropdown |
| `sel_application` | `StoredProcedures.sql` | List registered applications |
| `sel_applicationbyid` | `StoredProcedures.sql` | Load single application |
| `sel_applicationsites` | `StoredProcedures.sql` | Sites for an application |
| `commit_application` | `StoredProcedures.sql` | Register/update application |
| `commit_applicationsites` | `StoredProcedures.sql` | Register/update sites |
| `commit_applicationdelete` | `StoredProcedures.sql` | Soft-delete application |
| `commit_sharepointlogs` | `StoredProcedures.sql` | Audit logging |

### 1B. Process configuration wizard — route resolver prefetch

| Stored procedure | Script in `Database/` | API endpoint |
|------------------|----------------------|--------------|
| `sel_GetProcessTypes` | `sel_GetProcessTypes.sql` | `GET api/ProcessConfiguration/GetProcessType` |
| `sel_storageAccountDetails` | `sel_storageAccountDetails.sql` | `GET api/ProcessConfiguration/GetStorageAccountDetails` |
| `sel_fileServerDetails` | `sel_fileServerDetails.sql` | `GET api/ProcessConfiguration/GetFileServerDetails` |
| `sel_weekDays` | `sel_weekDays.sql` | `GET api/ProcessConfiguration/GetWeekDayName` |
| `sel_dataSliceAPIConfig` | `sel_dataSliceAPIConfig.sql` | `GET api/ProcessConfiguration/GetDSConfiguration?id=1` (v2.0) |
| `sel_info_schedulerType` | *(not in `Database/` folder)* | `GET api/ProcessConfiguration/GetSchedulerType` |
| `sel_datetimeFormat` | *(not in `Database/` folder)* | `GET api/ProcessConfiguration/GetAllDataTimeFormats` |
| `sel_FileExtensionNames` or similar | *(not in `Database/` folder)* | `GET api/ProcessConfiguration/GetValidFileExtensions` |

### 1C. Process configuration wizard — runtime (beyond resolver)

| Stored procedure | API endpoint | Notes |
|------------------|--------------|-------|
| `sel_Regions` | `GET api/ProcessConfiguration/GetAllDIRegions` | Not in `Database/` |
| `sel_subregions` | `GET api/ProcessConfiguration/GetAllDISubRegions` | Not in `Database/` |
| `sel_ClientNames` | `GET api/ProcessConfiguration/GetAllDIClientnames` | Not in `Database/` |
| `sel_DatatypeNames` | `GET api/ProcessConfiguration/getalldatatypenames` | Not in `Database/` |
| `sel_DatabaseName` | `GET api/ProcessConfiguration/GetDatabaseNames` | Not in `Database/` |
| `sel_flpProcessNameIsUnique` | `GET api/ProcessConfiguration/ProcessNameExists` | Not in `Database/` |
| `commit_SecurityGroup` (v4.1) | Called before save | Not in `Database/` |
| `sel_frequencyHours` | `GET api/ProcessConfiguration/GetFrequencyHour` | Not in `Database/` |

### 1D. Save and load (SharePoint process type included)

| Stored procedure | Script in `Database/` | API endpoint |
|------------------|----------------------|--------------|
| `commit_InsertFlpConfiguration` | `commit_InsertFlpConfiguration.sql` | `POST api/ProcessConfiguration/InsertFlpConfigurationDetails` (step 1 of 3) |
| `commit_InsertFlpFileConfiguration` | *(not in `Database/`)* | Same POST (step 2) |
| `commit_InsertConfigurationTableMapping` | *(not in `Database/`)* | Same POST (step 3) |
| `sel_processConfigurationById` | `sel_processConfigurationById.sql` | `GET api/ProcessConfiguration/GetConfigurationDetailsById` |

**Nested SPs called by `commit_InsertFlpConfiguration`:**

- `commit_flpSecurityGroupMapping`
- `commit_SecurityGroupIdForUser`
- `commit_customSchedulerDetailsV2`
- `funSchdulerCalculateNextRun` *(function, referenced in SP body)*

### 1E. SharePoint-specific — additive (recommended, no changes to existing SPs)

| Stored procedure | Script | API (proposed) |
|------------------|--------|----------------|
| `commit_InsertFlpSharePointSource` | `local_sharepoint_source_sps.sql` | Call **after** `commit_InsertFlpConfiguration` when `processTypeId = 6` |
| `sel_flpSharePointSourceByConfigId` | `local_sharepoint_source_sps.sql` | Merge into `GetConfigurationDetailsById` response in API layer |

---

## 2. Dependent tables

### 2A. Plugin tables (authoritative — `Tables.sql`)

| Table | Used by |
|-------|---------|
| `di_applicationtype` | `sel_applicationtypes` |
| `di_application` | `sel_application`, `commit_application`, SharePoint name resolution |
| `di_applicationsite` | `sel_applicationsites`, `commit_applicationsites`, SharePoint site name resolution |
| `di_application_usage` | `commit_application` (usage tracking) |
| `di_sharepointlogs` | `commit_sharepointlogs` |

### 2B. Lookup tables (local additive — `local_lookup_tables.sql`)

| Table | Used by |
|-------|---------|
| `di_info_processType` | `sel_GetProcessTypes` |
| `di_weekDays` | `sel_weekDays` |
| `di_info_storageAccountDetails` | `sel_storageAccountDetails` |
| `di_info_StorageContainerName` | `sel_storageAccountDetails` |
| `di_info_fileServerDetails` | `sel_fileServerDetails`, `sel_processConfigurationById` |
| `di_dataSliceAPIConfig` | `sel_dataSliceAPIConfig` |
| `di_flpSharePointSource` | `commit_InsertFlpSharePointSource`, `sel_flpSharePointSourceByConfigId` |

### 2C. Process configuration core (production `DataIngestionV2` — **missing locally**)

**Read/write via `commit_InsertFlpConfiguration`:**

| Table |
|-------|
| `di_flpConfiguration` |
| `di_flpConfigurationCampaignMapping` |
| `di_flpSchedulerConfiguration` |
| `di_processScheduler` |
| `di_customSchedulerDetails` |
| `di_databricksDestinationStorageAccountInfo` |
| `di_flpSourceStorageAccountInfo` |
| `di_sharedLocationSourceServer` |
| `di_flpDestinationStorageAccountInfo` |
| `NLog` |

**Read via `sel_processConfigurationById` (additional):**

| Table |
|-------|
| `di_configurationTableMapping` |
| `di_databaseConfiguration` |
| `di_flpFileConfiguration` |
| `di_flpFileConfigExtension` |
| `di_flpFileConfigRegex` |
| `di_info_schedulerType` |
| *(plus security group / rule set tables in later result sets)* |

---

## 3. Missing tables (must be created for full local functionality)

### Already created on `SharepointPlugin` (additive local scripts)

- All plugin tables (`Tables.sql`)
- All lookup tables (`local_lookup_tables.sql`)
- `di_flpSharePointSource`

### Still missing for resolver-only screens to fully load

| Table | Required for |
|-------|--------------|
| `di_info_schedulerType` | `GetSchedulerType` |
| `di_datetimeFormat` (or equivalent) | `GetAllDataTimeFormats` |
| File extension lookup table | `GetValidFileExtensions` |

### Still missing for complete wizard save/load

Entire `DataIngestionV2` process-configuration schema (~30+ tables) including at minimum:

`di_flpConfiguration`, `di_flpFileConfiguration`, `di_configurationTableMapping`, `di_databaseConfiguration`, `di_flpSchedulerConfiguration`, `di_info_schedulerType`, `di_flpDestinationStorageAccountInfo`, `di_flpSourceStorageAccountInfo`, `di_sharedLocationSourceServer`, `di_databricksDestinationStorageAccountInfo`, `di_processScheduler`, `di_customSchedulerDetails`, security group mapping tables, and all nested SPs referenced by `commit_InsertFlpConfiguration`.

**Local validation note:** `commit_InsertFlpConfiguration` and `sel_processConfigurationById` can be **created** on `SharepointPlugin`, but **EXEC** fails until `di_flpConfiguration` and join targets exist.

---

## 4. Required seed and lookup data

### Authoritative (`SeedData.sql`) — deployed

| Table | Seed |
|-------|------|
| `di_applicationtype` | `tp_external`, `tp_internal`, `tp_user_delegated` |

### Local additive (`local_lookup_seed.sql`) — deployed

| Table | Seed |
|-------|------|
| `di_info_processType` | 1 Online, 2 Offline-Shared, 3 Offline-Blob, **6 SharePoint Workspace** |
| `di_weekDays` | Sunday–Saturday |
| `di_info_storageAccountDetails` | `local-dev-storage` (id 1) |
| `di_info_StorageContainerName` | `local-container` |
| `di_info_fileServerDetails` | `LOCAL-DEV-FS` (id 1) |
| `di_dataSliceAPIConfig` | id 1 with sample GUIDs |

### SharePoint workspace UI (plugin)

For browse/connect in process-config mode, seed at least one row in:

- `di_application` — registered Entra app (no secrets in process config; credentials stay in plugin tables)
- `di_applicationsite` — site linked to that application

### Process type seed (production path)

`sharepoint_process_configuration.sql` seeds process type 6 and adds columns to `di_flpConfiguration`. **Locally:** use `local_lookup_seed.sql` for type 6 instead; **do not** run column ALTERs until `di_flpConfiguration` exists.

---

## 5. API endpoints mapped to stored procedures

### SharePoint plugin APIs (`SharePointController` — unchanged)

| Endpoint | Stored procedure(s) | Tables |
|----------|---------------------|--------|
| `GET /api/applications/types` | `sel_applicationtypes` | `di_applicationtype` |
| `GET /api/applications` | `sel_application` | `di_application` |
| `GET /api/applications/{id}` | `sel_applicationbyid` | `di_application` |
| `POST /api/applications` | `commit_application` | `di_application` |
| `POST /api/applications/libraries` | *(Graph — no DB)* | — |
| `POST /api/workspace/browse` | *(Graph)* | `di_application` for credentials |
| `POST /api/workspace/fetchfile` | *(Graph)* | `di_application` |

### Process configuration APIs (wizard)

| Endpoint | Stored procedure(s) |
|----------|---------------------|
| `GET GetProcessType` | `sel_GetProcessTypes` |
| `GET GetStorageAccountDetails` | `sel_storageAccountDetails` |
| `GET GetFileServerDetails` | `sel_fileServerDetails` |
| `GET GetWeekDayName` | `sel_weekDays` |
| `GET GetSchedulerType` | `sel_info_schedulerType` |
| `GET GetDSConfiguration` | `sel_dataSliceAPIConfig` |
| `GET GetAllDataTimeFormats` | `sel_datetimeFormat` |
| `GET GetValidFileExtensions` | `sel_FileExtensionNames` *(name per repo)* |
| `GET GetAllDIRegions` | `sel_Regions` |
| `GET GetAllDISubRegions` | `sel_subregions` |
| `GET GetAllDIClientnames` | `sel_ClientNames` |
| `GET getalldatatypenames` | `sel_DatatypeNames` |
| `GET GetDatabaseNames` | `sel_DatabaseName` |
| `GET ProcessNameExists` | `sel_flpProcessNameIsUnique` |
| `POST InsertFlpConfigurationDetails` | `commit_SecurityGroup` → `commit_InsertFlpConfiguration` → `commit_InsertFlpFileConfiguration` → `commit_InsertConfigurationTableMapping` |
| `GET GetConfigurationDetailsById` | `sel_processConfigurationById` (+ `sel_flpRuleSetsByConfigId` in service layer) |

**SharePoint-specific (recommended addition):**

| Endpoint change | Stored procedure |
|-----------------|------------------|
| After save when `processTypeId = 6` | `commit_InsertFlpSharePointSource` |
| On load when `processTypeId = 6` | `sel_flpSharePointSourceByConfigId` (merge in repository) |

---

## 6. Unavoidable stored procedure changes

**Per project constraints: none are unavoidable.**

SharePoint configuration can be supported without modifying existing stored procedures by:

1. Seeding process type **6** in `di_info_processType` (data only).
2. Persisting SharePoint references in **`di_flpSharePointSource`** (new table).
3. Using **`commit_InsertFlpSharePointSource`** / **`sel_flpSharePointSourceByConfigId`** (new SPs).
4. Updating the **API repository** to call the new SPs after/before existing calls.

### Changes that exist today but conflict with policy

| File | Change | Recommendation |
|------|--------|----------------|
| `commit_InsertFlpConfiguration.sql` | Added 4 `@sharePoint*` parameters; INSERT/UPDATE columns on `di_flpConfiguration` | **Revert** to production-original; use side-table SP instead |
| `sel_processConfigurationById.sql` | Returns SharePoint columns from `di_flpConfiguration`; JOINs `di_application` / `di_applicationsite` | **Revert**; enrich response in API via `sel_flpSharePointSourceByConfigId` |
| `sharepoint_process_configuration.sql` | ALTER `di_flpConfiguration` + seed type 6 | **Replace** with seed-only script for type 6 when `di_flpConfiguration` is unavailable; column ALTERs only when that table exists in target env |

**Justification for avoiding SP changes:** Production `commit_InsertFlpConfiguration` is a large transactional procedure with nested security/scheduler/storage logic. Adding SharePoint parameters changes the contract for all consumers. A side table preserves the original procedure behavior and isolates SharePoint data.

---

## 7. Local deployment order (validated)

```text
1. Tables.sql
2. StoredProcedures.sql
3. SeedData.sql
4. local_lookup_tables.sql
5. local_lookup_seed.sql
6. local_sharepoint_source_sps.sql
7. Lookup SPs (strip USE [DataIngestionV2]; use CREATE OR ALTER):
   - sel_GetProcessTypes.sql
   - sel_storageAccountDetails.sql
   - sel_fileServerDetails.sql
   - sel_weekDays.sql
   - sel_dataSliceAPIConfig.sql
```

**Do not run on `SharepointPlugin` until `di_flpConfiguration` exists:**

- `sharepoint_process_configuration.sql` (column ALTERs)
- `commit_InsertFlpConfiguration.sql` / `sel_processConfigurationById.sql` (EXEC only)

---

## 8. Next implementation steps (API alignment)

1. **Revert** modified versions of `commit_InsertFlpConfiguration.sql` and `sel_processConfigurationById.sql` in `Database/` to production originals.
2. **Wire repository:** after `InsertFlpConfigurationDetails` succeeds with `processTypeId = 6`, call `commit_InsertFlpSharePointSource`.
3. **Wire load:** in `GetConfigurationDetailsById`, if `processTypeId = 6`, call `sel_flpSharePointSourceByConfigId` and map to `ProcessConfigDetail` SharePoint properties.
4. **Optional local stubs:** minimal `di_info_schedulerType`, datetime format, and file extension tables + SPs so resolver prefetch returns 200 for all keys.

---

## Appendix: Local validation results (2026-06-15)

| Script / SP | Deploy | EXEC test |
|-------------|--------|-----------|
| `Tables.sql` | OK | — |
| `StoredProcedures.sql` | OK | `sel_applicationtypes` OK |
| `SeedData.sql` | OK | — |
| `local_lookup_*` | OK | — |
| `local_sharepoint_source_sps.sql` | OK | — |
| `sel_GetProcessTypes` | OK | Returns types 1,2,3,6 |
| `sel_weekDays` | OK | 7 rows |
| `sel_storageAccountDetails` | OK | — |
| `sel_fileServerDetails` | OK | — |
| `sel_dataSliceAPIConfig` | OK | — |
| `sharepoint_process_configuration.sql` | **FAIL** | `di_flpConfiguration` missing |
| `commit_InsertFlpConfiguration` | CREATE OK | **Not tested** — requires full schema |
