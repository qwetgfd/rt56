# TP Data Ingestion — SharePoint Workspace

**Client overview · functionality at a glance**

---

## What this solution is

- A web-based tool that connects your organization to **Microsoft SharePoint** document libraries.
- Part of **TP Data Ingestion**: it helps teams **register connections**, **browse files**, **preview documents**, and **share access** with other systems that need to read SharePoint content securely.
- Sensitive sign-in details are handled on the **server**, not stored in the user’s browser.

---

## Who it is for

- Teams that work with files stored in SharePoint and need a simple way to explore them.
- Administrators who manage multiple SharePoint sites or tenants.
- Integrators who need a **stable API** to pull files into pipelines, reports, or downstream applications.

---

## Main areas of the application

### 1. Home

- Starting screen with two paths:
  - **Applications** — manage saved SharePoint connection profiles.
  - **Workspace** — open a connected library and work with files like a file explorer.

### 2. Applications (connection registry)

- **Register** new SharePoint connections with a friendly name, site, and document library.
- **View** all registered connections in **card** or **list** layout.
- **Search and filter** by name, host, tenant, or type (Internal, External, Custom).
- **Edit** connection details when something changes (new secret, different site, etc.).
- **Delete** connections that are no longer needed.
- **Use** a saved connection — jumps straight into the Workspace with that profile selected.
- See summary counts: total applications, and how many are Internal, External, or Custom.
- After saving a connection, the system provides **integration credentials** for automated access (see “For other systems” below).
- Option to **download an API guide** (text file) for the registered connection.

### 3. Workspace (file browser)

- Connect using one of three **connection profiles**:
  - **Internal** — pick a connection you already registered in Applications.
  - **External** — enter tenant and app details for an external SharePoint environment.
  - **Custom** — use your own Microsoft Entra (Azure AD) app registration.
- Once connected:
  - Browse **folders and files** in the chosen document library.
  - Navigate with **back**, **forward**, **up**, and **breadcrumb** paths.
  - Switch between **list** and **grid** views.
  - **Refresh** the current folder.
  - **Reset** or **disconnect** when you want to start over.
- For Internal connections, **choose or refresh** the list of document libraries on the site.
- **Open files** in a built-in viewer when supported.
- **Download** files from the viewer.
- **Generate integration steps** (copy or download) for connecting other tools to the same SharePoint location.

---

## Application types (in plain terms)

| Type | Purpose |
|------|---------|
| **Internal** | Standard in-house SharePoint profiles saved in the system and reused from the Workspace. |
| **External** | Connections to SharePoint outside your main tenant; credentials are entered per session or saved as a profile. |
| **Custom** | Your organization’s own Microsoft app registration, for full control over access. |

---

## File preview support

When you open a file in the Workspace, the system can show a preview for many common formats, including:

- **Documents:** PDF, Word, PowerPoint  
- **Spreadsheets:** Excel, CSV  
- **Media:** images, video, audio  
- **Text** files  

If preview is not available for a file type, you can still **download** it.

---

## For other systems (integrations)

- Each registered application receives a **registered app ID** and **API secret** for secure machine-to-machine access.
- External tools authenticate once, then request files using a **token** and a **file path** — without embedding SharePoint passwords in scripts.
- The application can produce a **step-by-step integration guide** (including copy-ready commands) from the Workspace or from the application details screen.
- Typical flow for integrators:
  1. Obtain credentials from the registered application.
  2. Request an access token.
  3. Browse folders or download/stream a specific file by path.

*SharePoint site and library settings come from the registered application, so integrators do not need to repeat that configuration in every call.*

---

## Security and data handling (client-friendly)

- Microsoft sign-in secrets for SharePoint are **stored in the database** on the server side when you register an application.
- Day-to-day file operations go through the **API**; the web app does not keep long-lived passwords in the browser.
- Integration access uses **issued API credentials** separate from your Microsoft Entra app secret.
- Connection summary in the Workspace lets authorized users review **what is configured** (with secrets masked by default).

---

## What users can accomplish end-to-end

1. **Set up** one or more SharePoint connection profiles (tenant, site, library).  
2. **Browse** document libraries as if using a desktop file manager.  
3. **Preview and download** files without leaving the application.  
4. **Hand off** the same access to automation teams via the streaming/API guide.  
5. **Maintain** connections over time — update, filter, and remove profiles as environments change.

---

## What this solution does not replace

- It is not a full replacement for the SharePoint web interface for editing sites, permissions, or site structure.
- It focuses on **data access**, **browsing**, **preview**, and **controlled API access** for ingestion and integration scenarios.

---

## Summary

**TP Data Ingestion — SharePoint Workspace** gives your teams a single place to **manage SharePoint connections**, **explore document libraries**, **preview files**, and **enable secure automated access** for other systems — with credentials kept on the server and clear paths for both everyday users and integrators.

---

*Document generated from the current application scope. For deployment URLs, support contacts, or SLA details, add your organization’s standard appendix to this file.*
