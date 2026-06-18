# SharePoint UI Scope Discovery (HTML/CSS/Animation Only)

## Objective

Identify ONLY the files and components responsible for rendering the SharePoint Workspace user interface.

This investigation is strictly limited to:

* HTML / JSX / TSX markup
* CSS
* SCSS
* Tailwind classes
* Styled Components
* UI layouts
* Visual styling
* Animations
* Transitions
* Icons
* Typography
* Spacing
* Responsive design

We are NOT modifying functionality.

We are NOT modifying business logic.

We are NOT modifying APIs.

We are NOT modifying state management.

We are NOT modifying authentication.

We are NOT modifying permissions.

We are NOT modifying SharePoint integration logic.

The sole purpose is to locate every UI surface belonging to the SharePoint Workspace.

---

## Search Scope

Search for:

* sharepoint
* SharePointWorkspace
* SharePointPage
* SharePointView
* SharePointLayout
* SharePointPanel
* SharePointExplorer
* SharePointFiles
* SharePointDocuments
* SharePointLibrary

Also locate:

* Routes rendering SharePoint pages
* Parent layouts rendering SharePoint UI
* Shared visual components used inside SharePoint

---

## 1. SharePoint UI Component List

| # | Component Name | Selector | File Path | Purpose | Parent Component | Child Components |
|---|---|---|---|---|---|---|
| 1 | **SharepointComponent** | `app-sharepoint` | `src/app/sharepoint/sharepoint.component.ts` | Root shell — routes between home, applications, workspace views | None (root) | SharepointHomeComponent, SharepointApplicationsComponent, SharepointWorkspaceComponent |
| 2 | **SharepointHomeComponent** | `app-sharepoint-home` | `src/app/sharepoint/sharepoint-home/sharepoint-home.component.ts` | Landing page with hero, auth, nav cards, capabilities info | SharepointComponent | SharepointIconComponent |
| 3 | **SharepointApplicationsComponent** | `app-sharepoint-applications` | `src/app/sharepoint/sharepoint-applications/sharepoint-applications.component.ts` | Application registry list + registration form | SharepointComponent | SharepointIconComponent, SharepointStatusAlertsComponent, SelectDropDownModule |
| 4 | **SharepointWorkspaceComponent** | `app-sharepoint-workspace` | `src/app/sharepoint/sharepoint-workspace/sharepoint-workspace.component.ts` | **MAIN WORKSPACE** — toolbar, explorer, file list/grid, sidebar config, viewer, modals | SharepointComponent, FileFirstSharepointAttachComponent, ConfigurationFirstSharepointTabComponent | SharepointIconComponent, SharepointLogoComponent, SharepointStatusAlertsComponent, SelectDropDownModule |
| 5 | **SharepointIconComponent** | `sp-icon` | `src/app/sharepoint/core/sharepoint.ui.ts` | Inline SVG icon component (27 icon names) | Used by all SharePoint UI components | None |
| 6 | **SharepointStatusAlertsComponent** | `sp-status-alerts` | `src/app/sharepoint/core/sharepoint.ui.ts` | Alert/toast/banner status messages | SharepointWorkspaceComponent, SharepointApplicationsComponent | SharepointIconComponent |
| 7 | **SharepointLogoComponent** | `sp-sharepoint-logo` | `src/app/sharepoint/core/sharepoint.ui.ts` | SharePoint 4-circle logo SVG | SharepointWorkspaceComponent, FileFirstSharepointAttachComponent | None |
| 8 | **FileFirstSharepointAttachComponent** | `app-file-first-sharepoint-attach` | `src/app/file-first/file-first-sharepoint-attach/file-first-sharepoint-attach.component.ts` | File First embed shell — wraps workspace in file-pick mode with selection bar | Standalone embed | SharepointWorkspaceComponent, SharepointIconComponent, SharepointLogoComponent |
| 9 | **ConfigurationFirstSharepointTabComponent** | `app-configuration-first-sharepoint-tab` | `src/app/new-process-configuration/configuration-first-sharepoint-tab/configuration-first-sharepoint-tab.component.ts` | Wizard tab embed — wraps workspace in process-config mode with selection summary | Standalone embed (wizard) | SharepointWorkspaceComponent |
| 10 | **ConfigurationFirstSharepointTabNavComponent** | `app-configuration-first-sharepoint-tab-nav` | `src/app/new-process-configuration/configuration-first-sharepoint-tab-nav/configuration-first-sharepoint-tab-nav.component.ts` | Wizard tab navigation button (cloud icon + label) | Standalone embed (wizard nav) | None |

---

## 2. Styling File Inventory

### Design Tokens & Shared Primitives

| File | Purpose |
|------|---------|
| `src/app/sharepoint/sharepoint-tokens.css` | CSS custom properties (colors, spacing, radii, shadows, transitions) |
| `src/app/sharepoint/sharepoint.styles.css` | Shared design system — buttons, loaders, alerts, toasts, skeletons, form fields, dropdowns, keyframes |

### Component-Specific Styles

| File | Purpose |
|------|---------|
| `src/app/sharepoint/sharepoint.component.css` | Root shell layout — full-height app container, reduced-motion support |
| `src/app/sharepoint/sharepoint-home/sharepoint-home.component.css` | Home page — hero, auth bar, nav cards, info block, responsive grid |
| `src/app/sharepoint/sharepoint-applications/sharepoint-applications.component.css` | Applications registry — page layout, cards, table, forms, filters, summary cards, registration form, verification strip, responsive |
| `src/app/sharepoint/sharepoint-workspace/sharepoint-workspace.component.css` | **Main workspace** — toolbar, explorer, file list/grid, sidebar, panels, forms, viewer, modals, cURL guide, skeleton loaders, responsive breakpoints, process-config host overrides |
| `src/app/file-first/file-first-sharepoint-attach/file-first-sharepoint-attach.component.css` | File First embed — toolbar, workspace shell, selection bar, viewer overrides, responsive |
| `src/app/new-process-configuration/configuration-first-sharepoint-tab/configuration-first-sharepoint-tab.component.css` | Wizard tab embed — panel, selection summary, workspace embed container, responsive |
| `src/app/new-process-configuration/configuration-first-sharepoint-tab-nav/configuration-first-sharepoint-tab-nav.component.css` | Wizard nav button — icon, label, chevron, active state |

### Inline Styles (in .ui.ts)

| File | Purpose |
|------|---------|
| `src/app/sharepoint/core/sharepoint.ui.ts` | SharepointIconComponent (SVG sizes), SharepointLogoComponent (logo size), SharepointStatusAlertsComponent (display: contents) |

---

## 3. Animation Inventory

### Keyframe Definitions in `sharepoint.styles.css`

| Animation | Where Used | Purpose |
|-----------|-----------|---------|
| `sp-spin` | `.sp-btn__spinner`, `.sp-icon--spin`, `.sp-curl-modal__spinner`, `.sp-generate-curl__spinner` | Continuous rotation (0.6–1.2s) |
| `sp-fade-in` | `.sp-inline-loader`, `.sp-wait`, `.sp-viewer__overlay`, `.sp-explorer-content__overlay` | Fade in (0.2–0.28s) |
| `sp-loader-pulse` | `.sp-inline-loader__logo`, `.sp-logo-loader__mark` | Logo pulse (1.8s ease-in-out) |
| `sp-loader-pulse-ring` | `.sp-inline-loader__ring` | Ring pulse |
| `sp-loader-text-in` | `.sp-inline-loader__title`, `.sp-inline-loader__hint`, `.sp-inline-loader__steps` | Text slide-up (0.35s) |
| `sp-wait-bar-slide` | `.sp-wait__bar-glow` | Progress bar slide (1.15s) |
| `sp-wait-shimmer` | `.sp-wait__sketch-line`, `.sp-wait__sketch-block` | Shimmer (1.5–1.6s) |
| `sp-wait-sketch-in` | `.sp-wait__sketch` | Sketch entrance (0.4s cubic-bezier) |
| `sp-wait-dot-bounce` | `.sp-wait__dots span` | Dot bounce (0.9s) |
| `sp-toast-in` | `.sp-toast` | Toast entrance (0.28s cubic-bezier) |
| `sp-row-in` | `.sp-filelist-row` | Row stagger entrance (0.32s) |
| `sp-tile-in` | `.sp-filegrid__item` | Grid tile stagger entrance (0.34s) |

### Keyframe Definitions in `sharepoint-workspace.component.css`

| Animation | Where Used | Purpose |
|-----------|-----------|---------|
| `sp-pulse-dot` | `.sp-explorer-context__status-dot` | Status dot pulse (1.2s) |
| `sp-fade-in` | `.sp-explorer-inline-loader`, `.sp-explorer-content__overlay`, `.sp-viewer__overlay` | Fade in |
| `sp-skeleton-shimmer` | `.sp-skeleton` | Skeleton shimmer (1.4s) |
| `sp-loading-track` | `.sp-loading-bar__track` | Loading bar track (1.1s cubic-bezier) |
| `sp-loading-glow` | `.sp-loading-bar__glow` | Loading bar glow (1.1s) |
| `sp-spin` | `.sp-generate-curl__spinner`, `.sp-curl-modal__spinner` | Rotation |
| `spCfgEmptyIn` | `:host(.sp-workspace-host--process-config) .sp-explorer-empty` | Config mode empty state entrance (0.55s) |
| `spCfgEmptyFloat` | `:host(.sp-workspace-host--process-config) .sp-explorer-empty__icon` | Config mode icon float (3.2s) |

### Keyframe Definitions in `sharepoint-applications.component.css`

| Animation | Where Used | Purpose |
|-----------|-----------|---------|
| `sp-fieldset-reveal` | `.sp-fieldset--step` | Fieldset entrance (0.22s) |
| `sp-url-spin` | `.sp-url-inline-loader__ring` | URL parse spinner (0.85s) |
| `sp-url-pulse` | `.sp-url-inline-loader__core` | URL parse core pulse (1.1s) |
| `sp-url-beam` | `.sp-url-inline-loader__beam::after` | URL parse beam sweep (1.35s) |
| `sp-url-label` | `.sp-url-inline-loader__label` | URL parse label pulse (1.1s) |
| `sp-url-ok-pop` | `.sp-url-inline-ok` | URL parse success pop (0.35s) |
| `sp-verify-mark-pulse` | `.sp-verify-mark__pulse` | Verify mark pulse (2s) |
| `sp-verify-dot` | `.sp-verify-dots i` | Verify dot bounce (1.2s) |
| `sp-verify-track-slide` | `.sp-verify-track__fill` | Verify track slide (1.4s) |
| `sp-verify-phase-in` | `.sp-verify-strip__phase` | Verify phase entrance (0.35s) |
| `sp-verify-glimpse-flash` | `.sp-verify-glimpse` | Glimpse flash (1.15s) |
| `sp-verify-glimpse-wait-pulse` | `.sp-verify-glimpse-wait__pulse` | Glimpse wait pulse (1.1s) |
| `sp-reg-page-in` | `.sp-page--form` | Registration page entrance (0.4s) |
| `sp-reg-sheen` | `@keyframes sp-reg-sheen` | Registration sheen (unused keyframe) |
| `sp-reg-rise` | `.sp-page--form .sp-page__head--reg`, `.sp-page__lead` | Registration header rise (0.5s) |
| `sp-reg-panel-in` | `.sp-page--form .sp-reg-sheet`, `.sp-reg-actions`, `.sp-reg-download` | Registration panel entrance (0.35–0.5s) |
| `sp-reg-pulse` | `.sp-required` | Required asterisk pulse (2.4s) |
| `sp-advanced-in` | `.sp-advanced__body` | Advanced settings entrance (0.2s) |
| `sp-vrf-*` (30+ keyframes) | `.sp-vrf__*` | Verification radar animations — border flow, mesh drift, radar ping, core pulse, scan sweep, track glow, badge pulse, probe glow, cursor blink, live dot, preflight float, wire flow, stat pop, mark pop/shake, text in, title shimmer, phase in, dot wave/done/active, shake, pulse ring/burst, wait fade, ellipsis, sub-slide |
| `sp-cta-ready` | `.sp-btn--cta-ready` | CTA ready pulse (2.6s) |

### Keyframe Definitions in `file-first-sharepoint-attach.component.css`

| Animation | Where Used | Purpose |
|-----------|-----------|---------|
| `ff-spin` | `.ff-btn-spinner` | File First spinner (0.6s) |

### Keyframe Definitions in `sharepoint-home.component.css`

| Animation | Where Used | Purpose |
|-----------|-----------|---------|
| *(none — uses shared styles)* | — | — |

### Keyframe Definitions in `configuration-first-sharepoint-tab.component.css`

| Animation | Where Used | Purpose |
|-----------|-----------|---------|
| *(none — uses shared styles)* | — | — |

### Keyframe Definitions in `configuration-first-sharepoint-tab-nav.component.css`

| Animation | Where Used | Purpose |
|-----------|-----------|---------|
| *(none — uses shared styles)* | — | — |

### CSS Transitions (all files)

Every interactive element uses CSS transitions. Key transition properties:

| Property | Value | Where |
|----------|-------|-------|
| `--sp-transition-fast` | `0.12s ease` | Hover states, small UI changes |
| `--sp-transition` | `0.16s ease` | Sidebar collapse, panel transitions |
| `background var(--sp-transition-fast)` | Buttons, nav items, list rows |
| `border-color var(--sp-transition-fast)` | Buttons, inputs, cards |
| `color var(--sp-transition-fast)` | Links, labels, icons |
| `transform var(--sp-transition-fast)` | Grid tiles, cards, file icons |
| `opacity var(--sp-transition)` | Sidebar panels, modals |
| `width var(--sp-transition)` | Sidebar collapse |
| `transform 0.25s ease` | Mobile sidebar drawer |
| `transform 0.3s cubic-bezier(0.34, 1.3, 0.5, 1)` | Segmented control thumb |

### Reduced Motion Support

All files include `@media (prefers-reduced-motion: reduce)` overrides that disable animations and transitions.

---

## 4. SAFE TO MODIFY

These files contain ONLY markup, styling, visual layout, and animations. They are the only files allowed for future UI enhancement work.

### Tier 1 — Core Workspace UI (Primary Target)

| File | Lines | What You Can Change |
|------|-------|-------------------|
| `src/app/sharepoint/sharepoint-workspace/sharepoint-workspace.component.html` | ~839 | Toolbar layout, explorer structure, file list/grid markup, sidebar panels, viewer markup, modal markup |
| `src/app/sharepoint/sharepoint-workspace/sharepoint-workspace.component.css` | ~2580 | All workspace styles — toolbar, explorer, file views, sidebar, panels, viewer, modals, skeletons, responsive breakpoints, process-config host overrides, keyframes |
| `src/app/sharepoint/sharepoint-workspace/sharepoint-workspace.component.ts` | ~1671 | **Only** `@Input()`/`@Output()` definitions if adding new visual props. Do NOT change business logic, service calls, or state management. |

### Tier 2 — Shared Design System

| File | Lines | What You Can Change |
|------|-------|-------------------|
| `src/app/sharepoint/sharepoint-tokens.css` | ~40 | CSS custom properties — colors, spacing, radii, shadows, transitions |
| `src/app/sharepoint/sharepoint.styles.css` | ~1015 | Shared primitives — buttons, loaders, alerts, toasts, skeletons, form fields, dropdowns, scrollbar, keyframes |
| `src/app/sharepoint/core/sharepoint.ui.ts` | ~346 | Icon SVG paths, icon sizes, alert/toast/banner markup, logo SVG |

### Tier 3 — Standalone Module Pages

| File | Lines | What You Can Change |
|------|-------|-------------------|
| `src/app/sharepoint/sharepoint.component.ts` | ~48 | View routing logic only — do NOT change |
| `src/app/sharepoint/sharepoint.component.html` | ~25 | Shell layout — do NOT change |
| `src/app/sharepoint/sharepoint.component.css` | ~51 | Root shell styles — full-height layout, reduced-motion |
| `src/app/sharepoint/sharepoint-home/sharepoint-home.component.ts` | ~59 | Auth logic only — do NOT change |
| `src/app/sharepoint/sharepoint-home/sharepoint-home.component.html` | ~63 | Hero, auth bar, nav cards, info block markup |
| `src/app/sharepoint/sharepoint-home/sharepoint-home.component.css` | ~226 | Home page styles — hero, auth, nav cards, info block, responsive |
| `src/app/sharepoint/sharepoint-applications/sharepoint-applications.component.ts` | ~886 | Application registry logic — do NOT change |
| `src/app/sharepoint/sharepoint-applications/sharepoint-applications.component.html` | ~561 | List/table/form markup |
| `src/app/sharepoint/sharepoint-applications/sharepoint-applications.component.css` | ~4161 | Application registry styles — cards, table, forms, filters, summary, registration, verification, responsive, keyframes |

### Tier 4 — Embed Wrappers (Shell UI Only)

| File | Lines | What You Can Change |
|------|-------|-------------------|
| `src/app/file-first/file-first-sharepoint-attach/file-first-sharepoint-attach.component.ts` | ~99 | File pick logic — do NOT change |
| `src/app/file-first/file-first-sharepoint-attach/file-first-sharepoint-attach.component.html` | ~60 | Toolbar, workspace embed, selection bar markup |
| `src/app/file-first/file-first-sharepoint-attach/file-first-sharepoint-attach.component.css` | ~334 | Embed shell styles — toolbar, workspace shell, selection bar, viewer overrides, responsive |
| `src/app/new-process-configuration/configuration-first-sharepoint-tab/configuration-first-sharepoint-tab.component.ts` | ~115 | Wizard tab logic — do NOT change |
| `src/app/new-process-configuration/configuration-first-sharepoint-tab/configuration-first-sharepoint-tab.component.html` | ~47 | Panel, selection summary, workspace embed markup |
| `src/app/new-process-configuration/configuration-first-sharepoint-tab/configuration-first-sharepoint-tab.component.css` | ~177 | Wizard tab styles — panel, selection summary, embed container, responsive |
| `src/app/new-process-configuration/configuration-first-sharepoint-tab-nav/configuration-first-sharepoint-tab-nav.component.ts` | ~36 | Tab nav logic — do NOT change |
| `src/app/new-process-configuration/configuration-first-sharepoint-tab-nav/configuration-first-sharepoint-tab-nav.component.html` | ~19 | Nav button markup |
| `src/app/new-process-configuration/configuration-first-sharepoint-tab-nav/configuration-first-sharepoint-tab-nav.component.css` | ~80 | Nav button styles — icon, label, chevron, active state |

### Tier 5 — User-Facing Copy (Safe to Edit)

| File | Lines | What You Can Change |
|------|-------|-------------------|
| `src/app/sharepoint/core/sharepoint.messages.ts` | ~535 | All user-facing strings — labels, hints, headings, error messages, toast messages |

---

## 5. DO NOT TOUCH

These files contain services, APIs, hooks, stores, context providers, authentication logic, authorization logic, data fetching logic, and SharePoint integration logic. These files must never be modified.

### Services (API Calls, Auth, Data Fetching)

| File | Purpose |
|------|---------|
| `src/app/sharepoint/services/sharepoint-api.service.ts` | SharePoint API calls — browse, list libraries, fetch files, application catalog |
| `src/app/sharepoint/services/sharepoint-user.service.ts` | MSAL authentication — sign-in, sign-out, token management, user context |
| `src/app/sharepoint/services/sharepoint-preview.service.ts` | File preview rendering — PDF, DOCX, PPTX, Excel, CSV |

### Types, Config, Utils (Data Contracts, Business Logic)

| File | Purpose |
|------|---------|
| `src/app/sharepoint/core/sharepoint.types.ts` | TypeScript interfaces — DTOs, enums, API envelopes |
| `src/app/sharepoint/core/sharepoint.config.ts` | DI token `SHAREPOINT_ENV` — environment configuration |
| `src/app/sharepoint/core/sharepoint.utils.ts` | Business logic helpers — dropdown mappers, file type detection, URL parsing, curl builders, icon sources |
| `src/app/sharepoint/integration/configuration-first.sharepoint.ts` | Process type constants, SharePoint file process fields |

### Wiring (App Module, Routing, Environment)

| File | Purpose |
|------|---------|
| `src/app/app.module.ts` | Declares SharePoint components, provides `SHAREPOINT_ENV` |
| `src/app/app-routing.module.ts` | Route `{ path: 'ingestion-console', component: SharepointComponent }` |
| `src/app/environments/environment.ts` | Environment config — API URLs, MSAL credentials, SharePoint settings |

### Backend (Completely Out of Scope)

| Path | Purpose |
|------|---------|
| `src/app/../Backend/**` | .NET backend API |
| `src/app/../Database/**` | SQL stored procedures and table scripts |

---

## Architecture Summary

```
Route: /ingestion-console
  └── SharepointComponent (shell router)
        ├── SharepointHomeComponent (home page)
        │     └── SharepointIconComponent
        ├── SharepointApplicationsComponent (registry)
        │     ├── SharepointIconComponent
        │     ├── SharepointStatusAlertsComponent
        │     └── SelectDropDownModule
        └── SharepointWorkspaceComponent (★ MAIN UI)
              ├── SharepointIconComponent
              ├── SharepointLogoComponent
              ├── SharepointStatusAlertsComponent
              └── SelectDropDownModule

Embed Wrappers (reuse SharepointWorkspaceComponent):
  ├── FileFirstSharepointAttachComponent [filePickMode=true]
  └── ConfigurationFirstSharepointTabComponent [processConfigMode=true, filePickMode=true]
        └── ConfigurationFirstSharepointTabNavComponent (tab button)

Shared Design System:
  ├── sharepoint-tokens.css (CSS variables)
  ├── sharepoint.styles.css (shared primitives)
  └── sharepoint.ui.ts (icon, alerts, logo components)
```

---

## CSS Class Prefix Convention

All SharePoint UI uses `sp-` prefix. Key class groups:

| Prefix | Domain |
|--------|--------|
| `sp-btn-*` | Buttons (primary, secondary, ghost, danger, icon, sm, lg, block) |
| `sp-icon-*` | Icons (sm, lg, spin) |
| `sp-toolbar-*` | Workspace toolbar (left, right, breadcrumb, buttons, nav) |
| `sp-explorer-*` | File explorer (empty, context, content, inline-loader, view-toggle) |
| `sp-filelist-*` | File list view (header, body, row, btn, columns) |
| `sp-filegrid-*` | File grid view (item, btn, icon, name, meta) |
| `sp-skeleton-*` | Skeleton loaders (row, grid-item, shimmer) |
| `sp-workspace-*` | Workspace layout (page, shell, main, sidebar, toggle) |
| `sp-panel-*` | Sidebar panels (head, actions, summary) |
| `sp-segment-*` | Segmented control (opt, title, hint, active) |
| `sp-field-*` | Form fields (label, input, hint, required, dropdown) |
| `sp-viewer-*` | File viewer (backdrop, head, body, stage, media, pick) |
| `sp-curl-*` | cURL modal (backdrop, modal, step, code, meta) |
| `sp-chip-*` | Status badges (internal, external) |
| `sp-alert-*` | Alert banners (error, success, dismiss) |
| `sp-toast-*` | Toast notifications (host, error, success, dismiss) |
| `sp-ngx-dropdown-*` | Dropdown styling (trigger, option, chevron) |
| `sp-verify-*` | Verification strip (strip, mark, timeline, glimpse) |
| `sp-reg-*` | Registration form (panel, segment, banner, actions) |
| `sp-vrf-*` | Verification radar (strip, icon, console, probe) |
| `sp-page--form` | Registration page scope |

---

## Responsive Breakpoints

| Breakpoint | Target | Key Changes |
|------------|--------|-------------|
| `≤1024px` | Tablet | Sidebar becomes overlay drawer, file list hides modified column, toolbar compact |
| `≤768px` | Mobile | Toolbar wraps, breadcrumb full-width, sidebar full-screen overlay, viewer padding reduced |
| `≤480px` | Small mobile | File list hides size column, segment options stack, panel padding reduced |
| `≤900px` | Wizard embed | Selection summary grid becomes single column |
| `≤640px` | Registration | Segment buttons full-width, toolbar hint wraps |
| `≤720px` | Registration type | Type picker stacks vertically |

---

## File Count Summary

| Category | Files | Total Lines (approx) |
|----------|-------|---------------------|
| Component TS (HTML/CSS logic) | 10 | ~4,500 |
| Component HTML templates | 7 | ~1,700 |
| Component CSS files | 7 | ~9,500 |
| Shared design system | 3 | ~1,400 |
| **Total UI surface** | **27** | **~17,100** |

---

*Generated: 2026-06-17*
*Repo: `C:\Users\user\Desktop\s\Integration_Project\FRONTEND\src\app\sharepoint\`*
*Route: `/ingestion-console`*
