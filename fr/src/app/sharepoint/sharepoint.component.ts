import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { environment } from '../../environments/environment';

interface SitePathBodyFields {
  hostName?: string;
  sitePath?: string;
  driveName?: string;
}

interface ListChildrenBodyRequest extends SitePathBodyFields {
  folderPath?: string;
}

interface StreamFileBodyRequest extends SitePathBodyFields {
  filePath: string;
}

interface FileByPathRequest extends SitePathBodyFields {
  filePath: string;
}

interface SharePointDriveItem {
  id: string;
  name: string;
  isFolder: boolean;
  size: number;
  mimeType?: string;
  lastModifiedDateTime?: string;
  webUrl?: string;
  path?: string;
  childCount?: number;
}

interface SharePointListInfo {
  id: string;
  name: string;
  displayName?: string;
  description?: string;
  webUrl?: string;
  createdDateTime?: string;
  lastModifiedDateTime?: string;
}

interface SharePointListItem {
  id: string;
  fields: Record<string, unknown>;
  createdDateTime?: string;
  lastModifiedDateTime?: string;
}

interface ListItemUpdateBody extends SitePathBodyFields {
  listId: string;
  itemId: string;
  fields: Record<string, unknown>;
}

interface ListItemCreateBody extends SitePathBodyFields {
  listId: string;
  fields: Record<string, unknown>;
}

interface ListItemDeleteBody extends SitePathBodyFields {
  listId: string;
  itemId: string;
}

interface ApiResponse<T> { success: boolean; message: string; data: T }

interface SharePointServerDefaults {
  tenantId: string;
  clientId: string;
  clientSecret: string;
  sharepointHostName: string;
  sitePath: string;
  driveName: string;
  filePath: string;
  defaultDriveName: string;
}

interface RegisteredApplication {
  id: string;
  displayName: string;
  tenantId: string;
  clientId: string;
  clientSecret: string;
  sharepointHostName: string;
  driveName?: string;
  notes?: string;
}

type SpView = 'home' | 'register' | 'apps-list' | 'app-edit' | 'workspace' | 'sample-code';
type WorkspaceTab = 'files' | 'lists';
type ConfigProfile = 'tp-internal' | 'tp-external';
type FileType = 'video' | 'audio' | 'pdf' | 'word' | 'excel' | 'powerpoint' | 'text' | 'image' | 'folder' | 'unknown';

function fileLabel(item: SharePointDriveItem): string {
  return (item.path ?? item.name).toLowerCase();
}

function getFileType(item: SharePointDriveItem): FileType {
  if (item.isFolder) return 'folder';
  const mime = (item.mimeType ?? '').toLowerCase();
  const name = fileLabel(item);
  if (mime.startsWith('video/') || mime.includes('msvideo') || /\.(mp4|avi|mov|wmv|mkv|webm|flv|m4v|3gp|mpg|mpeg|ogv)$/.test(name)) return 'video';
  if (mime.startsWith('audio/') || /\.(mp3|wav|ogg|flac|aac|wma|m4a|opus)$/.test(name)) return 'audio';
  if (mime === 'application/pdf' || name.endsWith('.pdf')) return 'pdf';
  if (mime.includes('word') || /\.(doc|docx|odt|rtf)$/.test(name)) return 'word';
  if (mime.includes('excel') || mime.includes('spreadsheet') || /\.(xls|xlsx|ods|csv)$/.test(name)) return 'excel';
  if (mime.includes('powerpoint') || mime.includes('presentation') || /\.(ppt|pptx|odp)$/.test(name)) return 'powerpoint';
  if (mime.startsWith('text/') || /\.(txt|md|json|xml|css|js|ts|html|htm|log|cfg|ini|bat|sh|py|cs|java|cpp|c|h|sql|yaml|yml)$/.test(name)) return 'text';
  if (mime.startsWith('image/') || /\.(jpg|jpeg|png|gif|bmp|svg|webp|ico|tiff)$/.test(name)) return 'image';
  return 'unknown';
}

function getFileIcon(type: FileType): string {
  const icons: Record<FileType, string> = {
    video: '🎬', audio: '🎵', pdf: '📄', word: '📝', excel: '📊', powerpoint: '📽️',
    text: '📃', image: '🖼️', folder: '📁', unknown: '📎',
  };
  return icons[type];
}

function formatSize(item: SharePointDriveItem): string {
  if (item.isFolder) return `${item.childCount ?? 0} items`;
  if (item.size < 1024) return `${item.size} B`;
  if (item.size < 1048576) return `${(item.size / 1024).toFixed(1)} KB`;
  if (item.size < 1073741824) return `${(item.size / 1048576).toFixed(1)} MB`;
  return `${(item.size / 1073741824).toFixed(1)} GB`;
}

const MIME_BY_EXT: Record<string, string> = {
  pdf: 'application/pdf', mp4: 'video/mp4', m4v: 'video/mp4', webm: 'video/webm',
  mov: 'video/quicktime', avi: 'video/x-msvideo', wmv: 'video/x-ms-wmv', mkv: 'video/x-matroska',
  mp3: 'audio/mpeg', m4a: 'audio/mp4', wav: 'audio/wav', ogg: 'audio/ogg',
  png: 'image/png', jpg: 'image/jpeg', jpeg: 'image/jpeg', gif: 'image/gif', webp: 'image/webp',
};

function guessMimeFromName(fileName: string): string {
  const ext = fileName.split('.').pop()?.toLowerCase() ?? '';
  return MIME_BY_EXT[ext] ?? 'application/octet-stream';
}

function isBrowserLimitedVideo(fileName: string): boolean {
  return /\.(avi|wmv|mkv|flv)$/.test(fileName.toLowerCase());
}

function maskValue(value: string): string {
  if (!value?.trim()) return '—';
  if (value.length <= 10) return '••••••••';
  return `${value.slice(0, 4)}…${value.slice(-4)}`;
}

@Component({
  selector: 'app-sharepoint',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './sharepoint.component.html',
  styleUrl: './sharepoint.component.css',
})
export class SharepointComponent implements OnInit {
  readonly apiBaseUrl = environment.apiBaseUrl;

  tpInternalDefaults: SharePointServerDefaults | null = null;
  configLoading = false;

  view: SpView = 'home';
  workspaceTab: WorkspaceTab = 'files';
  configProfile: ConfigProfile = 'tp-internal';
  externalConnected = false;
  showInternalConfig = false;
  configPanelCollapsed = false;
  showDeveloperTools = false;
  driveName = '';
  registerSuccess = false;
  editingAppId: string | null = null;

  registeredApps: RegisteredApplication[] = [];
  selectedRegisteredAppId = '';

  registerForm: RegisteredApplication = this.emptyRegisterForm();

  sitePath = '';
  filePath = '';
  currentPath = '';
  items: SharePointDriveItem[] = [];
  loading = false;
  error = '';
  successMessage = '';
  breadcrumbs: string[] = [];

  activeCredentials = {
    tenantId: '',
    clientId: '',
    clientSecret: '',
    sharepointHostName: '',
  };

  selectedItem: SharePointDriveItem | null = null;
  showViewer = false;
  fileType: FileType = 'unknown';
  fileContent = '';
  blobUrl = '';
  safeBlobUrl: SafeResourceUrl | null = null;
  mediaMimeType = '';
  mediaPlaybackUnsupported = false;
  viewerLoading = false;

  // SharePoint lists
  spLists: SharePointListInfo[] = [];
  selectedList: SharePointListInfo | null = null;
  listItems: SharePointListItem[] = [];
  listColumns: string[] = [];
  listLoading = false;
  showListItemForm = false;
  editingListItem: SharePointListItem | null = null;
  listItemForm: Record<string, string> = {};

  constructor(
    private http: HttpClient,
    private sanitizer: DomSanitizer,
  ) {}

  ngOnInit(): void {
    this.loadRegisteredApps();
    this.loadTpInternalConfigFromApi();
  }

  get defaultDriveName(): string {
    return this.tpInternalDefaults?.defaultDriveName?.trim() || 'Documents';
  }

  /** Returns the site path with "sites/" prefix for API calls. */
  get apiSitePath(): string {
    const raw = this.sitePath.trim();
    if (!raw) return '';
    if (raw.startsWith('sites/')) return raw;
    return `sites/${raw}`;
  }

  private hasInternalDefaultsConfigured(): boolean {
    const d = this.tpInternalDefaults;
    return !!(d?.tenantId?.trim() && d?.clientId?.trim() && d?.clientSecret?.trim() && d?.sharepointHostName?.trim());
  }

  private loadTpInternalConfigFromApi(onLoaded?: () => void): void {
    this.configLoading = true;
    this.http.get<ApiResponse<SharePointServerDefaults>>(`${this.apiBaseUrl}/config`).subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.tpInternalDefaults = res.data;
          if (environment.autoConnect && this.hasInternalDefaultsConfigured() && this.view === 'home') {
            this.openWorkspace();
          }
          onLoaded?.();
        } else {
          this.error = res.message || 'Failed to load SharePoint configuration from API.';
        }
        this.configLoading = false;
      },
      error: (err) => {
        this.error = err.error?.message ?? 'Could not reach SharePoint API for configuration. Is the API running?';
        this.configLoading = false;
      },
    });
  }

  private hasActiveCredentials(): boolean {
    const c = this.activeCredentials;
    return !!(c.tenantId?.trim() && c.clientId?.trim() && c.clientSecret?.trim() && c.sharepointHostName?.trim());
  }

  get isTpInternal(): boolean {
    return this.configProfile === 'tp-internal';
  }

  get isTpExternal(): boolean {
    return this.configProfile === 'tp-external';
  }

  get canUseWorkspace(): boolean {
    if (this.isTpInternal) {
      return this.hasActiveCredentials();
    }
    return this.isTpExternal && this.externalConnected && this.hasActiveCredentials();
  }

  get configProfileLabel(): string {
    return this.isTpInternal ? 'TP-Internal' : 'External';
  }

  get effectiveDriveName(): string {
    return this.normalizeDriveName(this.driveName);
  }

  get internalConfigSummary(): { tenantId: string; clientId: string; secret: string; host: string } {
    const d = this.tpInternalDefaults;
    return {
      tenantId: maskValue(d?.tenantId ?? ''),
      clientId: maskValue(d?.clientId ?? ''),
      secret: '••••••••',
      host: d?.sharepointHostName ?? '—',
    };
  }

  // ── Sample code for integration ──
  get sampleCSharpCode(): string {
    const c = this.activeCredentials;
    return `// C# — Stream a file from SharePoint
using var client = new HttpClient();
client.DefaultRequestHeaders.Add("tenantId", "${c.tenantId || 'YOUR_TENANT_ID'}");
client.DefaultRequestHeaders.Add("clientId", "${c.clientId || 'YOUR_CLIENT_ID'}");
client.DefaultRequestHeaders.Add("clientSecret", "${c.clientSecret ? '••••••••' : 'YOUR_CLIENT_SECRET'}");
client.DefaultRequestHeaders.Add("sharepointHostName", "${c.sharepointHostName || 'contoso.sharepoint.com'}");
client.DefaultRequestHeaders.Add("sitePath", "${this.apiSitePath || 'sites/MySite'}");
client.DefaultRequestHeaders.Add("driveName", "${this.effectiveDriveName || 'Documents'}");
client.DefaultRequestHeaders.Add("api-version", "${environment.apiVersion}");

// List folder children
var listResponse = await client.PostAsJsonAsync(
    "${this.apiBaseUrl.replace(/\/$/, '')}/folder/list",
    new { folderPath = "" }
);

// Stream a file
var streamResponse = await client.PostAsJsonAsync(
    "${this.apiBaseUrl.replace(/\/$/, '')}/StreamFile",
    new { filePath = "folder/file.pdf" }
);
await using var fileStream = File.Create("downloaded-file.pdf");
await streamResponse.Content.CopyToAsync(fileStream);`;
  }

  get sampleCurlCode(): string {
    const base = this.apiBaseUrl.replace(/\/$/, '');
    return `# List folder children
curl -X POST "${base}/folder/list" \\
  -H "tenantId: ${this.activeCredentials.tenantId || 'YOUR_TENANT_ID'}" \\
  -H "clientId: ${this.activeCredentials.clientId || 'YOUR_CLIENT_ID'}" \\
  -H "clientSecret: ${this.activeCredentials.clientSecret ? '••••••••' : 'YOUR_CLIENT_SECRET'}" \\
  -H "sharepointHostName: ${this.activeCredentials.sharepointHostName || 'contoso.sharepoint.com'}" \\
  -H "sitePath: ${this.apiSitePath || 'sites/MySite'}" \\
  -H "driveName: ${this.effectiveDriveName || 'Documents'}" \\
  -H "api-version: ${environment.apiVersion}" \\
  -H "Content-Type: application/json" \\
  -d '{}'

# Stream a file
curl -X POST "${base}/StreamFile" \\
  -H "tenantId: ${this.activeCredentials.tenantId || 'YOUR_TENANT_ID'}" \\
  -H "clientId: ${this.activeCredentials.clientId || 'YOUR_CLIENT_ID'}" \\
  -H "clientSecret: ${this.activeCredentials.clientSecret ? '••••••••' : 'YOUR_CLIENT_SECRET'}" \\
  -H "sharepointHostName: ${this.activeCredentials.sharepointHostName || 'contoso.sharepoint.com'}" \\
  -H "sitePath: ${this.apiSitePath || 'sites/MySite'}" \\
  -H "driveName: ${this.effectiveDriveName || 'Documents'}" \\
  -H "api-version: ${environment.apiVersion}" \\
  -H "Content-Type: application/json" \\
  -d '{"filePath":"folder/file.pdf"}' \\
  --output ./downloaded-file.pdf`;
  }

  private emptyRegisterForm(): RegisteredApplication {
    return {
      id: '',
      displayName: '',
      tenantId: '',
      clientId: '',
      clientSecret: '',
      sharepointHostName: '',
      driveName: this.defaultDriveName,
      notes: '',
    };
  }

  private loadRegisteredApps(): void {
    try {
      const raw = localStorage.getItem(environment.registeredApplicationsStorageKey);
      this.registeredApps = raw ? JSON.parse(raw) : [];
    } catch {
      this.registeredApps = [];
    }
  }

  private saveRegisteredApps(): void {
    localStorage.setItem(environment.registeredApplicationsStorageKey, JSON.stringify(this.registeredApps));
  }

  // ── Navigation ──
  goHome(): void {
    this.view = 'home';
    this.clearMessages();
    this.resetWorkspaceSession();
  }

  openRegister(): void {
    this.view = 'register';
    this.editingAppId = null;
    this.registerForm = this.emptyRegisterForm();
    this.registerSuccess = false;
    this.clearMessages();
  }

  openAppsList(): void {
    this.view = 'apps-list';
    this.clearMessages();
  }

  openAppEdit(appId: string): void {
    const app = this.registeredApps.find(a => a.id === appId);
    if (!app) return;
    this.view = 'app-edit';
    this.editingAppId = appId;
    this.registerForm = { ...app };
    this.clearMessages();
  }

  openWorkspace(): void {
    this.view = 'workspace';
    this.workspaceTab = 'files';
    this.configProfile = 'tp-internal';
    this.externalConnected = false;
    this.clearMessages();
    if (this.tpInternalDefaults) {
      this.applyTpInternalDefaults();
      this.resetWorkspaceSession();
    } else {
      this.loadTpInternalConfigFromApi(() => {
        this.applyTpInternalDefaults();
        this.resetWorkspaceSession();
      });
    }
  }

  goToWorkspaceExternal(): void {
    this.view = 'workspace';
    this.workspaceTab = 'files';
    this.configProfile = 'tp-external';
    this.externalConnected = false;
    this.selectedRegisteredAppId = '';
    this.clearMessages();
    this.clearRuntimeCredentials();
    this.sitePath = '';
    this.filePath = '';
    this.resetWorkspaceSession();
  }

  openSampleCode(): void {
    this.view = 'sample-code';
    this.clearMessages();
  }

  // ── Config profile ──
  onConfigProfileChange(): void {
    this.clearMessages();
    this.externalConnected = false;
    this.resetWorkspaceSession();
    if (this.isTpInternal) {
      this.applyTpInternalDefaults();
    } else {
      this.clearRuntimeCredentials();
      this.sitePath = '';
      this.driveName = '';
      this.filePath = '';
    }
  }

  private applyTpInternalDefaults(): void {
    const d = this.tpInternalDefaults;
    if (!d) {
      this.error = 'SharePoint configuration not loaded from API yet.';
      return;
    }
    this.activeCredentials = {
      tenantId: d.tenantId ?? '',
      clientId: d.clientId ?? '',
      clientSecret: d.clientSecret ?? '',
      sharepointHostName: d.sharepointHostName ?? '',
    };
    // For internal defaults, store the raw site path (user-friendly)
    const rawSite = d.sitePath ?? '';
    this.sitePath = rawSite.startsWith('sites/') ? rawSite.slice(6) : rawSite;
    this.driveName = this.normalizeDriveName(d.driveName || d.defaultDriveName || '');
    this.filePath = d.filePath ?? '';
  }

  private normalizeDriveName(value: string | null | undefined): string {
    let s = (value ?? '').trim();
    if (!s) return '';
    for (let i = 0; i < 3; i++) {
      try {
        const decoded = decodeURIComponent(s.replace(/\+/g, ' '));
        if (decoded === s) break;
        s = decoded;
      } catch {
        s = s.replace(/%20/gi, ' ').replace(/\+/g, ' ');
        break;
      }
    }
    return s.trim();
  }

  private clearRuntimeCredentials(): void {
    this.activeCredentials = { tenantId: '', clientId: '', clientSecret: '', sharepointHostName: '' };
  }

  private clearMessages(): void {
    this.error = '';
    this.successMessage = '';
  }

  // ── Registration ──
  isRegisterFormValid(): boolean {
    const f = this.registerForm;
    return !!(f.displayName.trim() && f.tenantId.trim() && f.clientId.trim() && f.clientSecret.trim() && f.sharepointHostName.trim());
  }

  saveRegisteredApplication(): void {
    if (!this.isRegisterFormValid()) {
      this.error = 'Please complete all required registration fields.';
      return;
    }
    if (this.editingAppId) {
      // Update existing
      const idx = this.registeredApps.findIndex(a => a.id === this.editingAppId);
      if (idx === -1) return;
      this.registeredApps[idx] = {
        ...this.registerForm,
        id: this.editingAppId,
        displayName: this.registerForm.displayName.trim(),
        tenantId: this.registerForm.tenantId.trim(),
        clientId: this.registerForm.clientId.trim(),
        clientSecret: this.registerForm.clientSecret.trim(),
        sharepointHostName: this.registerForm.sharepointHostName.trim(),
        driveName: this.registerForm.driveName?.trim() || this.defaultDriveName,
        notes: this.registerForm.notes?.trim(),
      };
      this.saveRegisteredApps();
      this.successMessage = `Application "${this.registerForm.displayName}" updated.`;
      this.view = 'apps-list';
    } else {
      // Create new
      const app: RegisteredApplication = {
        ...this.registerForm,
        id: crypto.randomUUID(),
        displayName: this.registerForm.displayName.trim(),
        tenantId: this.registerForm.tenantId.trim(),
        clientId: this.registerForm.clientId.trim(),
        clientSecret: this.registerForm.clientSecret.trim(),
        sharepointHostName: this.registerForm.sharepointHostName.trim(),
        driveName: this.registerForm.driveName?.trim() || this.defaultDriveName,
        notes: this.registerForm.notes?.trim(),
      };
      this.registeredApps = [...this.registeredApps, app];
      this.saveRegisteredApps();
      this.registerSuccess = true;
      this.activeCredentials = {
        tenantId: app.tenantId,
        clientId: app.clientId,
        clientSecret: app.clientSecret,
        sharepointHostName: app.sharepointHostName,
      };
      this.error = '';
      this.successMessage = `"${app.displayName}" registered successfully.`;
    }
  }

  openWorkspaceTab(tab: WorkspaceTab): void {
    this.workspaceTab = tab;
    if (tab === 'lists') {
      this.loadLists();
    }
  }

  deleteApp(appId: string): void {
    const app = this.registeredApps.find(a => a.id === appId);
    if (!app) return;
    if (!confirm(`Delete "${app.displayName}"?`)) return;
    this.registeredApps = this.registeredApps.filter(a => a.id !== appId);
    this.saveRegisteredApps();
    this.successMessage = `Application "${app.displayName}" deleted.`;
  }

  onRegisteredAppSelected(): void {
    if (!this.selectedRegisteredAppId) {
      this.externalConnected = false;
      this.clearRuntimeCredentials();
      return;
    }
    const app = this.registeredApps.find(a => a.id === this.selectedRegisteredAppId);
    if (!app) return;
    this.activeCredentials = {
      tenantId: app.tenantId,
      clientId: app.clientId,
      clientSecret: app.clientSecret,
      sharepointHostName: app.sharepointHostName,
    };
    this.driveName = this.normalizeDriveName(app.driveName);
    this.externalConnected = true;
    this.clearMessages();
    this.successMessage = `Connected as "${app.displayName}".`;
  }

  applyExternalCredentials(): void {
    if (!this.activeCredentials.tenantId.trim() || !this.activeCredentials.clientId.trim() ||
        !this.activeCredentials.clientSecret.trim() || !this.activeCredentials.sharepointHostName.trim()) {
      this.error = 'Tenant, client ID, client secret, and SharePoint host are required.';
      return;
    }
    this.externalConnected = true;
    this.clearMessages();
    this.successMessage = 'Connection established. Enter a site name and library to browse.';
  }

  disconnectExternal(): void {
    this.externalConnected = false;
    this.selectedRegisteredAppId = '';
    this.clearRuntimeCredentials();
    this.resetWorkspaceSession();
  }

  // ── API helpers ──
  private buildHeaders(): HttpHeaders {
    const c = this.activeCredentials;
    let headers = new HttpHeaders()
      .set('Content-Type', 'application/json')
      .set('tenantId', c.tenantId)
      .set('clientId', c.clientId)
      .set('clientSecret', c.clientSecret)
      .set('sharepointHostName', c.sharepointHostName)
      .set('sitePath', this.apiSitePath)
      .set('api-version', environment.apiVersion);
    const library = this.effectiveDriveName;
    if (library) {
      headers = headers.set('driveName', library);
    }
    return headers;
  }

  private buildSitePathBody(): SitePathBodyFields {
    return {
      hostName: this.activeCredentials.sharepointHostName.trim(),
      sitePath: this.apiSitePath,
      driveName: this.effectiveDriveName,
    };
  }

  // ── File browsing ──
  loadChildren(): void {
    if (!this.hasActiveCredentials()) {
      this.error = 'Configure credentials first.';
      return;
    }
    if (!this.canUseWorkspace) return;
    if (!this.effectiveDriveName) {
      this.error = 'Enter a document library name (e.g. Documents).';
      return;
    }
    this.loading = true;
    this.error = '';
    this.items = [];
    const body: ListChildrenBodyRequest = {
      ...this.buildSitePathBody(),
      folderPath: this.currentPath || undefined,
    };
    this.http.post<ApiResponse<SharePointDriveItem[]>>(`${this.apiBaseUrl}/folder/list`, body, { headers: this.buildHeaders() }).subscribe({
      next: (res) => { this.items = res.data; this.loading = false; this.updateBreadcrumbs(); },
      error: (err) => { this.error = err.error?.message ?? 'Failed to load folder.'; this.loading = false; },
    });
  }

  streamFile(): void {
    if (!this.hasActiveCredentials()) { this.error = 'Configure credentials first.'; return; }
    if (!this.canUseWorkspace && this.isTpExternal) { this.error = 'Connect credentials before streaming.'; return; }
    if (!this.filePath.trim()) { this.error = 'Enter a file path to stream.'; return; }
    const path = this.filePath.trim();
    const fakeItem: SharePointDriveItem = { id: path, name: path.split('/').pop() ?? path, isFolder: false, size: 0, path };
    this.openItem(fakeItem);
  }

  openItem(item: SharePointDriveItem): void {
    if (item.isFolder) {
      this.currentPath = item.path ?? '';
      this.loadChildren();
      return;
    }
    this.selectedItem = item;
    this.fileType = getFileType(item);
    this.mediaMimeType = guessMimeFromName(item.path ?? item.name);
    this.mediaPlaybackUnsupported = false;
    this.showViewer = true;
    this.fileContent = '';
    this.clearBlob();
    this.streamAndPreview(item);
  }

  goBack(): void {
    if (!this.currentPath) return;
    const parts = this.currentPath.split('/');
    parts.pop();
    this.currentPath = parts.join('/');
    this.loadChildren();
  }

  navigateBreadcrumb(index: number): void {
    this.currentPath = index === 0 ? '' : this.breadcrumbs.slice(1, index + 1).join('/');
    this.loadChildren();
  }

  closeViewer(): void {
    this.showViewer = false;
    this.selectedItem = null;
    this.viewerLoading = false;
    this.mediaPlaybackUnsupported = false;
    this.clearBlob();
  }

  private resetWorkspaceSession(): void {
    this.items = [];
    this.currentPath = '';
    this.breadcrumbs = [];
    this.closeViewer();
    this.spLists = [];
    this.selectedList = null;
    this.listItems = [];
    this.listColumns = [];
    this.showListItemForm = false;
    this.editingListItem = null;
    this.listItemForm = {};
  }

  private clearBlob(): void {
    if (this.blobUrl?.startsWith('blob:')) URL.revokeObjectURL(this.blobUrl);
    this.blobUrl = '';
    this.safeBlobUrl = null;
  }

  private buildStreamUrl(filePath: string): string {
    const c = this.activeCredentials;
    const params = new URLSearchParams({
      filePath,
      tenantId: c.tenantId,
      clientId: c.clientId,
      clientSecret: c.clientSecret,
      sharepointHostName: c.sharepointHostName,
      sitePath: this.apiSitePath,
      driveName: this.effectiveDriveName,
    });
    return `${this.apiBaseUrl.replace(/\/$/, '')}/StreamFile?${params.toString()}`;
  }

  private streamAndPreview(item: SharePointDriveItem): void {
    this.viewerLoading = true;
    this.error = '';
    const path = item.path ?? item.name;
    const type = getFileType(item);

    if (type === 'video' || type === 'audio') {
      this.clearBlob();
      this.blobUrl = this.buildStreamUrl(path);
      this.safeBlobUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.blobUrl);
      return;
    }

    const body: StreamFileBodyRequest = { filePath: path, ...this.buildSitePathBody() };
    this.http.post(`${this.apiBaseUrl}/StreamFile`, body, {
      headers: this.buildHeaders(),
      responseType: 'blob',
      observe: 'response',
    }).subscribe({
      next: (res: HttpResponse<Blob>) => {
        const raw = res.body;
        if (!raw) { this.error = 'Empty file response.'; this.viewerLoading = false; return; }
        const typed = raw.type && raw.type !== 'application/octet-stream' ? raw : new Blob([raw], { type: guessMimeFromName(item.name) });
        this.blobUrl = URL.createObjectURL(typed);
        this.safeBlobUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.blobUrl);
        this.viewerLoading = false;
        if (type === 'text') typed.text().then(t => { this.fileContent = t; });
      },
      error: (err) => { this.error = err.error?.message ?? 'Failed to load file.'; this.viewerLoading = false; },
    });
  }

  onStreamCanPlay(): void { this.viewerLoading = false; this.mediaPlaybackUnsupported = false; }
  onStreamError(): void {
    this.viewerLoading = false;
    const label = this.selectedItem ? fileLabel(this.selectedItem) : '';
    if (isBrowserLimitedVideo(label)) {
      this.mediaPlaybackUnsupported = true;
      this.error = 'This format may not play in the browser. Use Download.';
    } else {
      this.error = 'Stream failed. Check site path, file path, and API availability.';
    }
  }

  downloadFile(): void {
    if (!this.selectedItem) return;
    const body: FileByPathRequest = { filePath: this.selectedItem.path ?? this.selectedItem.name, ...this.buildSitePathBody() };
    this.http.post(`${this.apiBaseUrl}/file/download`, body, { headers: this.buildHeaders(), responseType: 'blob' }).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = this.selectedItem!.name;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => { this.error = 'Download failed.'; },
    });
  }

  getFileIcon(item: SharePointDriveItem): string { return getFileIcon(getFileType(item)); }
  getFileSize(item: SharePointDriveItem): string { return formatSize(item); }

  private updateBreadcrumbs(): void {
    this.breadcrumbs = this.currentPath ? ['Root', ...this.currentPath.split('/')] : ['Root'];
  }

  toggleConfigPanel(): void { this.configPanelCollapsed = !this.configPanelCollapsed; }

  // ── Path change handlers ──
  onSitePathChange(value: string): void {
    if (!value?.trim()) { this.driveName = ''; this.filePath = ''; }
    else { this.filePath = ''; }
    this.clearBrowseOnly();
  }

  onDriveNameChange(value: string): void {
    const normalized = this.normalizeDriveName(value);
    if (normalized !== this.driveName) { this.driveName = normalized; }
    if (!normalized) { this.filePath = ''; }
    this.clearBrowseOnly();
  }

  onFilePathChange(): void { /* no-op */ }

  private clearBrowseOnly(): void {
    this.items = [];
    this.currentPath = '';
    this.breadcrumbs = [];
    this.closeViewer();
  }

  // ── SharePoint Lists ──
  loadLists(): void {
    if (!this.hasActiveCredentials()) { this.error = 'Configure credentials first.'; return; }
    if (!this.apiSitePath) { this.error = 'Enter a site name first.'; return; }
    this.listLoading = true;
    this.error = '';
    this.spLists = [];
    this.selectedList = null;
    this.listItems = [];
    this.http.post<ApiResponse<SharePointListInfo[]>>(`${this.apiBaseUrl}/lists`, this.buildSitePathBody(), { headers: this.buildHeaders() }).subscribe({
      next: (res) => { this.spLists = res.data; this.listLoading = false; },
      error: (err) => { this.error = err.error?.message ?? 'Failed to load lists.'; this.listLoading = false; },
    });
  }

  selectList(list: SharePointListInfo): void {
    this.selectedList = list;
    this.listItems = [];
    this.listColumns = [];
    this.showListItemForm = false;
    this.editingListItem = null;
    this.listItemForm = {};
    this.loadListItems(list.id);
  }

  loadListItems(listId: string): void {
    this.listLoading = true;
    this.listItems = [];
    this.listColumns = [];
    const body = { ...this.buildSitePathBody(), listId };
    this.http.post<ApiResponse<SharePointListItem[]>>(`${this.apiBaseUrl}/list/items`, body, { headers: this.buildHeaders() }).subscribe({
      next: (res) => {
        this.listItems = res.data;
        this.listLoading = false;
        // Extract column names from first item
        if (res.data.length > 0) {
          this.listColumns = Object.keys(res.data[0].fields).filter(k =>
            !['@odata.etag', 'id', 'createdDateTime', 'lastModifiedDateTime'].includes(k)
          );
        }
      },
      error: (err) => { this.error = err.error?.message ?? 'Failed to load list items.'; this.listLoading = false; },
    });
  }

  openNewListItem(): void {
    this.showListItemForm = true;
    this.editingListItem = null;
    this.listItemForm = {};
    this.listColumns.forEach(col => { this.listItemForm[col] = ''; });
  }

  openEditListItem(item: SharePointListItem): void {
    this.showListItemForm = true;
    this.editingListItem = item;
    this.listItemForm = {};
    this.listColumns.forEach(col => {
      const val = item.fields[col];
      this.listItemForm[col] = val != null ? String(val) : '';
    });
  }

  cancelListItemForm(): void {
    this.showListItemForm = false;
    this.editingListItem = null;
    this.listItemForm = {};
  }

  saveListItem(): void {
    if (!this.selectedList) return;
    const fields: Record<string, unknown> = {};
    Object.entries(this.listItemForm).forEach(([k, v]) => {
      if (v.trim()) fields[k] = v.trim();
    });

    if (this.editingListItem) {
      // Update
      const body: ListItemUpdateBody = {
        ...this.buildSitePathBody(),
        listId: this.selectedList.id,
        itemId: this.editingListItem.id,
        fields,
      };
      this.http.post<ApiResponse<SharePointListItem>>(`${this.apiBaseUrl}/list/item/update`, body, { headers: this.buildHeaders() }).subscribe({
        next: () => { this.cancelListItemForm(); this.loadListItems(this.selectedList!.id); this.successMessage = 'Item updated.'; },
        error: (err) => { this.error = err.error?.message ?? 'Failed to update item.'; },
      });
    } else {
      // Create
      const body: ListItemCreateBody = {
        ...this.buildSitePathBody(),
        listId: this.selectedList.id,
        fields,
      };
      this.http.post<ApiResponse<SharePointListItem>>(`${this.apiBaseUrl}/list/item/create`, body, { headers: this.buildHeaders() }).subscribe({
        next: () => { this.cancelListItemForm(); this.loadListItems(this.selectedList!.id); this.successMessage = 'Item created.'; },
        error: (err) => { this.error = err.error?.message ?? 'Failed to create item.'; },
      });
    }
  }

  deleteListItem(item: SharePointListItem): void {
    if (!this.selectedList) return;
    if (!confirm('Delete this item?')) return;
    const body: ListItemDeleteBody = {
      ...this.buildSitePathBody(),
      listId: this.selectedList.id,
      itemId: item.id,
    };
    this.http.post<ApiResponse<unknown>>(`${this.apiBaseUrl}/list/item/delete`, body, { headers: this.buildHeaders() }).subscribe({
      next: () => { this.loadListItems(this.selectedList!.id); this.successMessage = 'Item deleted.'; },
      error: (err) => { this.error = err.error?.message ?? 'Failed to delete item.'; },
    });
  }

  getListItemField(item: SharePointListItem, field: string): unknown {
    return item.fields[field] ?? '—';
  }

  // ── Clipboard ──
  async copyToClipboard(text: string, label: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.successMessage = `${label} copied to clipboard.`;
      setTimeout(() => { this.successMessage = ''; }, 2500);
    } catch {
      this.error = 'Unable to copy to clipboard.';
    }
  }
}
