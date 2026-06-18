# DataIngestion Project Architecture

---

Last updated: 2026-06-04

## 1. Project Overview

---

DataIngestion is an Angular single-page application used for data ingestion workflows, process configuration, status tracking, analytics views, EIB flows, and profiling.

It is built as a classic NgModule-based Angular app (not standalone components) with:

- Root module + root routing
- Shared and Core cross-cutting modules
- Feature areas under `src/app/*`
- Lazy-loaded feature modules for EIB and Profiling
- Authentication and API protection with Microsoft MSAL

## 2. Technology Stack

---

From package and Angular configuration:

- Framework: Angular 21
- Language: TypeScript 5.9
- Build: Angular CLI / `@angular/build:application`
- UI: Bootstrap, Angular Material, PrimeNG, NG Bootstrap, ng-select
- Charts: ApexCharts / ng-apexcharts
- Notifications/UX helpers: ngx-toastr, ngx-spinner, ngx-pagination
- Auth: `@azure/msal-angular`, `@azure/msal-browser`
- Data/file helpers: PapaParse, xlsx, jszip, xmldom, xpath
- Realtime: `@microsoft/signalr`

## 3. Source Layout

---

Top-level runtime source areas:

- `src/main.ts`: Angular bootstrap entry
- `src/index.html`: host page
- `src/styles.css`: global styles
- `src/app/`: application modules/components/services
- `public/`: static assets and fonts

Important app subfolders:

- `src/app/core/`: services, guards, interceptors, resolvers, models, utilities
- `src/app/shared/`: reusable components, directives, pipes, shared models
- `src/app/environments/`: environment-specific config
- Feature folders such as `dashboard`, `process-configuration`, `eib`, `profiling`, etc.

## 4. High-Level Architecture

---

```
Browser
  |
  v
Angular AppModule
  |------------------------|------------------------|
  v                        v                        v
AppRoutingModule         CoreModule              SharedModule
  |                        |                        |
  +-- Feature Components   +-- Auth + API Services  +-- Reusable UI + Directives + Pipes
  +-- Lazy Module:         |       |
      EibModule             |       +-- Backend APIs
  +-- Lazy Module:         |       +-- Microsoft identity Platform
      ProfilingModule       |
                            +-- Guards/Resolvers/Interceptors
```

## 5. Modules Inventory

---

**5.1 Angular module files (7 total)**

1. `src/app/app.module.ts` (root module)
2. `src/app/app-routing.module.ts` (root routing module)
3. `src/app/core/core.module.ts`
4. `src/app/shared/shared.module.ts`
5. `src/app/eib/eib.module.ts` (lazy loaded)
6. `src/app/profiling/profiling.module.ts` (lazy loaded)
7. `src/app/main-layout/main-layout.module.ts`

**5.2 Notes on module responsibilities**

- `app.module.ts`: root declarations, global providers, MSAL setup, interceptors, PrimeNG theme, bootstrap targets
- `app-routing.module.ts`: primary route map and lazy-loading boundaries
- `core.module.ts`: cross-cutting runtime capabilities (spinner, toastr setup)
- `shared.module.ts`: reusable UI, directives, pipes, forms imports/exports
- `eib.module.ts` and `profiling.module.ts`: feature isolation + route-level lazy loading
- `main-layout.module.ts`: present but currently minimal

## 6. Routing Architecture

---

Configured in `src/app/app-routing.module.ts`.

**6.1 Root and feature routes**

- `''`, `login` -> Login
- `mainlayout` -> Main layout shell
- `file-upload`
- `file-processing-status`
- `file-processing-status/:id`
- `dashboard`
- `add` (datasource add flow)
- `user-group`
- `process-configuration`
- `add-process` (with resolver)
- `process-config-list`
- `process-configuration/:id` (with resolver)
- `process-configuration/:id/:tabName` (with resolver)
- `processed-file-list`
- `ruleset-list`
- `create-eib` (guarded)
- `unauthorized`

**6.2 Lazy-loaded route segments**

- `eib` -> `EibModule`
- `profiling` -> `ProfilingModule`

**6.3 Route protection and prefetch**

- Guard used: `addEIBAccessGuard`
- Resolver used: `OfflineModuleResolver`
- Additional guards exist in Core for broader authorization concerns

## 7. Component Inventory

---

Total component classes found: 33

**7.1 Root and shell**

1. AppComponent (src/app/app.component.ts)
2. LoginComponent (src/app/login/login.component.ts)
3. MainLayoutComponent (src/app/main-layout/main-layout.component.ts)
4. UnauthorizedComponent (src/app/unauthorized/unauthorized.component.ts)

**7.2 Data ingestion and process configuration**

5. FileUploadComponent (`src/app/file-upload/file-upload.component.ts`)
6. FileProcessingStatusComponent (`src/app/file-processing-status/file-processing-status.component.ts`)
7. FileStatusChartComponent (`src/app/file-status-chart/file-status-chart.component.ts`)
8. ProcessStatusTemplateComponent (`src/app/process-status-template/process-status-template.component.ts`)
9. ProcessConfigurationComponent (`src/app/process-configuration/process-configuration.component.ts`)
10. AddProcessComponent (`src/app/add-process/add-process.component.ts`)
11. ProcessConfigListComponent (`src/app/process-config-list/process-config-list.component.ts`)
12. NewProcessConfigurationComponent (`src/app/new-process-configuration/new-process-configuration.component.ts`)
13. FilePreviewComponent (`src/app/new-process-configuration/file-preview/file-preview.component.ts`)
14. ProcessedFileListComponent (`src/app/processed-file-list/processed-file-list.component.ts`)
15. DatasourcesComponent (`src/app/datasources/datasources.component.ts`)

**7.3 Governance and admin**

16. UserGroupComponent (`src/app/user-group/user-group.component.ts`)
17. AdGroupComponent (`src/app/ad-group/ad-group.component.ts`)
18. RulesetListComponent (`src/app/ruleset-list/ruleset-list.component.ts`)
19. CreateRuleComponent (`src/app/create-rule/create-rule.component.ts`)
20. RegexBuilderComponent (`src/app/regex-builder/regex-builder.component.ts`)
21. ConfirmDialogComponent (`src/app/confirm-dialog/confirm-dialog.component.ts`)

**7.4 Dashboard and utilization**

22. DashboardComponent (`src/app/dashboard/dashboard.component.ts`)
23. DiRegionComponent (`src/app/di-region/di-region.component.ts`)
24. DiUploadsComponent (`src/app/di-uploads/di-uploads.component.ts`)
25. DiUtilizationComponent (`src/app/di-utilization/di-utilization.component.ts`)

**7.5 EIB feature area**

26. EibComponent (`src/app/eib/eib.component.ts`)
27. CreateEibComponent (`src/app/eib/create-eib/create-eib.component.ts`)
28. CustomEIBViewComponent (`src/app/eib/custom-eib-view/custom-eib-view.component.ts`)
29. EibDownloadComponent (`src/app/eib/eib-download/eib-download.component.ts`)

**7.6 Profiling feature area**

30. ProfilingComponent (`src/app/profiling/profiling.component.ts`)
31. ProfilingDetailsComponent (`src/app/profiling/profiling-details/profiling-details.component.ts`)

**7.7 Shared reusable components**

32. NotificationComponent (`src/app/shared/components/notification/notification.component.ts`)
33. ErrorBoxComponent (`src/app/shared/components/error-box/error-box.component.ts`)

## 8. Core Layer Inventory

---

**8.1 Services (20 total)**

1. `auth.service.ts`
2. `busy.service.ts`
3. `configuration.service.ts`
4. `confirm-modal.service.ts`
5. `dashboard.service.ts`
6. `data-insider.service.ts`
7. `dataslice.service.ts`
8. `di-parser.service.ts`
9. `eib/eib.service.ts`
10. `FAB/fab-service.service.ts`
11. `file-status.service.ts`
12. `flp-configuration-list.service.ts`
13. `graph-api-token.service.ts`
14. `LandingLayer/landingLayer.service.ts`
15. `login.service.ts`
16. `modal-service.service.ts`
17. `navigate.service.ts`
18. `process-config.service.ts`
19. `status.service.ts`
20. `token.service.ts`

**8.2 Guards (3 total)**

1. `auth.guard.ts`
2. `add-eib-access.guard.ts`
3. `global-rule-access.guard.ts`

**8.3 Interceptors (2 total)**

1. `loading.interceptor.ts`
2. `error.interceptor.ts`

**8.4 Resolvers (1 total)**

1. `offline-module.resolver.ts`

**8.5 Core models (26 total)**

- Includes contracts for API responses, user/group access, process config, data sources, EIB entities, region mappings, and additional ingestion settings

## 9. Shared Layer Inventory

---

**9.1 Shared module assets**

- Shared components: Notification, ErrorBox
- Shared directives in `nospaces.directive.ts`:
  - NospacesDirective
  - NumberOnlyDirective
  - DecimalFourPlacesDirective
- Shared pipes in `pipes.ts`:
  - KeysPipe
  - IsFabUserPipe
- Shared models: 5 files under `src/app/shared/models/`

## 10. Authentication and Security Flow

---

- MSAL is configured in AppModule via factory providers:
  - MSAL instance factory
  - Guard config factory
  - Interceptor config factory
- HTTP pipeline includes three interceptors:
  - MSAL token interceptor
  - Loading interceptor
  - Error interceptor
- Root component initializes login/account state, stores API token and selected group metadata in storage, and coordinates auth bootstrap.

## 11. Build and Runtime Configuration

---

From angular and npm config:

- Build output: `dist/data-ingestion`
- Environments:
  - `src/app/environments/environment.ts`
  - `src/app/environments/environment.prod.ts`
- Build targets:
  - `npm run build` (default app build)
  - `npm run build:uat`
  - `npm run build:prod`
- Serve target:
  - `npm start / ng serve`

## 12. Test Coverage Snapshot

---

- Component spec files found: 17
- This indicates partial test coverage (not every component has a matching spec currently)

## 13. Observations

---

- Architecture follows layered Angular conventions:
  - Presentation in components
  - Business/domain orchestration mostly in services
  - Cross-cutting concerns in interceptors/guards/resolvers
- EIB and Profiling are separated through lazy loading, which is good for bundle segmentation.
- Shared module centralizes reusable directives/pipes/UI components.

## 14. Quick Counts Summary

---

- Components: 33
- Angular module files (including routing): 7
- Core services: 20
- Guards: 3
- Interceptors: 2
- Resolvers: 1
- Core model files: 26
- Shared model files: 5
- Component spec files: 17
