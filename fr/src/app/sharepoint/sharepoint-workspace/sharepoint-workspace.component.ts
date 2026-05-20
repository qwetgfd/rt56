import {
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  Input,
  OnDestroy,
  OnInit,
  Output,
  EventEmitter,
  ViewChild,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml, SafeResourceUrl } from '@angular/platform-browser';
import { SHAREPOINT_ENV } from '../core/sharepoint.config';
import { SharePointApiService } from '../services/sharepoint.api.service';
import {
  ApplicationDto,
  ApplicationTypeCode,
  ApplicationTypeDto,
  FileKind,
  SharePointItemDto,
  SharePointLibraryDto,
  WorkspaceConnection,
} from '../core/sharepoint.types';
import {
  connectionFromApplication,
  copyCurlText,
  CurlModalResult,
  CurlStep,
  curlScriptFilename,
  detectFileKind,
  downloadCurlScript,
  FALLBACK_APPLICATION_TYPES,
  generateCurlCommands,
  highlightCurl,
  isApplicationTypeCode,
  parseApiError,
  sortSharePointItems,
  StatusAlerts,
} from '../core/sharepoint.utils';
import { SharepointIconComponent } from '../core/sharepoint-icon.component';
import {
  SP_APP_DROPDOWN_CONFIG,
  SP_LIBRARY_DROPDOWN_CONFIG,
  SpDropdownOption,
  unwrapNgxDropdownSelection,
} from '../core/sharepoint-ngx-dropdown.config';
import { SelectDropDownModule } from 'ngx-select-dropdown';
import { RichPreviewMode, SharePointFilePreviewService } from '../services/sharepoint-file-preview.service';

const STORAGE_KEY = 'sp_workspace_prefs';


type FileDisplayMode = 'list' | 'grid';

interface WorkspacePrefs {
  sidebarCollapsed: boolean;
  configMode: ApplicationTypeCode;
  fileDisplayMode?: FileDisplayMode;
}

@Component({
  selector: 'app-sharepoint-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule, SharepointIconComponent, SelectDropDownModule],
  templateUrl: './sharepoint-workspace.component.html',
  styleUrls: ['./sharepoint-workspace.component.css', '../sharepoint.styles.css'],
})
export class SharepointWorkspaceComponent implements OnInit, OnDestroy {
  private readonly env = inject(SHAREPOINT_ENV);

  @Input() initialApplicationId: string | null = null;
  @Output() navigateHome = new EventEmitter<void>();
  @Output() navigateApplications = new EventEmitter<void>();
  @Output() registerInternal = new EventEmitter<void>();
  @Output() launchApplicationConsumed = new EventEmitter<void>();

  applicationTypes: ApplicationTypeDto[] = [];
  applications: ApplicationDto[] = [];

  configMode: ApplicationTypeCode = 'tp_internal';
  selectedInternalAppId = '';
  internalAppDropdownOptions: SpDropdownOption[] = [];
  selectedInternalAppDropdown: SpDropdownOption | null = null;
  readonly appDropdownConfig = SP_APP_DROPDOWN_CONFIG;

  libraryDropdownOptions: SpDropdownOption[] = [];
  selectedLibraryDropdown: SpDropdownOption | null = null;
  readonly libraryDropdownConfig = SP_LIBRARY_DROPDOWN_CONFIG;

  fileDisplayMode: FileDisplayMode = 'list';
  private pendingApplicationId: string | null = null;

  libraries: SharePointLibraryDto[] = [];
  loadingLibraries = false;

  workspaceConnection: WorkspaceConnection = {};
  workspaceConnected = false;

  browsing = false;
  currentPath = '';
  items: SharePointItemDto[] = [];
  breadcrumbs: string[] = [];

  private navHistory: string[] = [];
  private navHistoryIndex = -1;

  selectedItem: SharePointItemDto | null = null;
  showViewer = false;
  viewerKind: FileKind = 'unknown';
  viewerLoading = false;
  viewerUrl: SafeResourceUrl | null = null;
  viewerRawUrl = '';
  viewerText = '';
  viewerMediaError = '';
  viewerMediaStatus = '';
  viewerUnsupportedMsg = '';

  @ViewChild('officePreviewHost') officePreviewHost?: ElementRef<HTMLDivElement>;

  readonly status = new StatusAlerts();

  sidebarCollapsed = false;
  isMobile = false;
  mobileDrawerOpen = false;

  wsSecretVisible = false;
  wsSummarySecretVisible = false;
  wsConsumerSummarySecretVisible = false;

  curlModalOpen = false;
  curlModalLoading = false;
  curlModalData: CurlModalResult | null = null;
  curlModalCopied = false;
  curlStepCopied: number | null = null;

  constructor(
    private readonly api: SharePointApiService,
    private readonly sanitizer: DomSanitizer,
    private readonly previewSvc: SharePointFilePreviewService,
    private readonly cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    if (this.initialApplicationId?.trim()) {
      this.pendingApplicationId = this.initialApplicationId.trim();
    }
    this.loadPreferences();
    this.loadApplicationTypes();
    this.loadApplications();
    this.checkViewport();
    if (!this.pendingApplicationId) {
      this.configMode = this.configMode || 'tp_internal';
      this.ensureDisconnectedState();
    }
  }

  ngOnDestroy(): void {
    this.status.destroy();
    if (this.viewerRawUrl.startsWith('blob:')) {
      URL.revokeObjectURL(this.viewerRawUrl);
    }
    this.clearRichPreviewDom();
    this.previewSvc.teardown();
  }

  /** Host element must exist in DOM before we render (PDF, Office, CSV). */
  get hasRichPreviewHost(): boolean {
    if (!this.showViewer || !this.selectedItem) return false;
    const name = (this.selectedItem.path ?? this.selectedItem.name).toLowerCase();
    if (this.viewerKind === 'pdf' || this.viewerKind === 'csv' || this.viewerKind === 'excel') return true;
    if (this.viewerKind === 'powerpoint') return name.endsWith('.pptx');
    if (this.viewerKind === 'word') return name.endsWith('.docx');
    return false;
  }

  @HostListener('window:resize')
  onResize(): void {
    this.checkViewport();
  }

  private checkViewport(): void {
    const wasMobile = this.isMobile;
    this.isMobile = window.innerWidth <= 768;
    if (this.isMobile && !wasMobile) {
      this.sidebarCollapsed = true;
    }
  }

  toggleSidebar(): void {
    this.sidebarCollapsed = !this.sidebarCollapsed;
    if (this.isMobile) {
      this.mobileDrawerOpen = !this.sidebarCollapsed;
    }
    this.savePreferences();
  }

  closeMobileDrawer(): void {
    if (this.isMobile) {
      this.mobileDrawerOpen = false;
      this.sidebarCollapsed = true;
    }
  }

  private loadPreferences(): void {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (raw) {
        const prefs: WorkspacePrefs = JSON.parse(raw);
        this.sidebarCollapsed = prefs.sidebarCollapsed ?? false;
        this.configMode = prefs.configMode ?? 'tp_internal';
        this.fileDisplayMode = prefs.fileDisplayMode === 'grid' ? 'grid' : 'list';
      }
    } catch {
      // ignore parse errors
    }
  }

  setFileDisplayMode(mode: FileDisplayMode): void {
    this.fileDisplayMode = mode;
    this.savePreferences();
  }

  private savePreferences(): void {
    try {
      const prefs: WorkspacePrefs = {
        sidebarCollapsed: this.sidebarCollapsed,
        configMode: this.configMode,
        fileDisplayMode: this.fileDisplayMode,
      };
      localStorage.setItem(STORAGE_KEY, JSON.stringify(prefs));
    } catch {
      // ignore storage errors
    }
  }

  get internalApplications(): ApplicationDto[] {
    return this.applications.filter((a) => a.applicationTypeCode === 'tp_internal');
  }

  private emptyWorkspaceConnection(): WorkspaceConnection {
    return {
      applicationId: null,
      tenantId: '',
      clientId: '',
      clientSecret: '',
      consumerClientId: '',
      consumerSecret: '',
      hostName: '',
      siteName: '',
      libraryName: '',
    };
  }

  private clearBrowseState(): void {
    this.items = [];
    this.currentPath = '';
    this.breadcrumbs = [];
    this.libraries = [];
    this.libraryDropdownOptions = [];
    this.selectedLibraryDropdown = null;
    this.browsing = false;
    this.resetNavigationHistory();
    this.closeViewer();
  }

  private rebuildInternalAppDropdownOptions(): void {
    this.internalAppDropdownOptions = [...this.internalApplications.map((a) => ({
      id: a.applicationId,
      label: a.displayName,
      searchText: `${a.siteName ?? ''} ${a.hostName ?? ''}`.trim(),
    }))];
    this.syncInternalAppDropdownSelection();
  }

  private syncInternalAppDropdownSelection(): void {
    this.selectedInternalAppDropdown =
      this.internalAppDropdownOptions.find((o) => o.id === this.selectedInternalAppId) ?? null;
  }

  private rebuildLibraryDropdownOptions(): void {
    this.libraryDropdownOptions = [...this.libraries.map((l) => ({ id: l.name, label: l.name }))];
    this.syncLibraryDropdownSelection();
  }

  private syncLibraryDropdownSelection(): void {
    const name = this.workspaceConnection.libraryName?.trim();
    this.selectedLibraryDropdown =
      this.libraryDropdownOptions.find((o) => o.id === name) ?? null;
  }

  onInternalAppDropdownModelChange(model: unknown): void {
    const item = unwrapNgxDropdownSelection(model);
    this.selectedInternalAppDropdown = item;
    const nextId = item?.id?.trim() ?? '';
    if (!nextId) {
      if (this.selectedInternalAppId) {
        this.selectedInternalAppId = '';
        this.onInternalApplicationChange();
      }
      return;
    }
    if (nextId === this.selectedInternalAppId) return;
    this.selectedInternalAppId = nextId;
    this.onInternalApplicationChange();
  }

  onLibraryDropdownModelChange(model: unknown): void {
    const item = unwrapNgxDropdownSelection(model);
    this.selectedLibraryDropdown = item;
    const nextName = item?.id?.trim() ?? '';
    if (!nextName || nextName === this.workspaceConnection.libraryName) return;
    this.workspaceConnection.libraryName = nextName;
    this.onLibraryChange();
  }

  private ensureDisconnectedState(): void {
    this.workspaceConnected = false;
    this.selectedInternalAppId = '';
    this.selectedInternalAppDropdown = null;
    this.libraries = [];
    this.items = [];
    this.currentPath = '';
    this.breadcrumbs = [];
    this.resetNavigationHistory();
    this.workspaceConnection = this.emptyWorkspaceConnection();
  }

  resetWorkspace(): void {
    this.status.clear();
    this.pendingApplicationId = null;
    this.curlModalOpen = false;
    this.curlModalData = null;
    this.closeViewer();
    this.configMode = 'tp_internal';
    this.ensureDisconnectedState();
    this.wsSecretVisible = false;
    this.wsSummarySecretVisible = false;
    this.wsConsumerSummarySecretVisible = false;
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore
    }
    this.savePreferences();
    this.status.setSuccess('Workspace reset. Choose a connection to continue.');
  }

  onLibraryChange(): void {
    if (!this.workspaceConnected || !this.workspaceConnection.libraryName?.trim()) return;
    this.currentPath = '';
    this.breadcrumbs = [];
    this.resetNavigationHistory();
    this.loadChildren('');
  }

  get isModeInternal(): boolean { return this.configMode === 'tp_internal'; }
  get isModeExternal(): boolean { return this.configMode === 'tp_external'; }
  get isModeCustom(): boolean { return this.configMode === 'custom'; }

  get selectedInternalApp(): ApplicationDto | undefined {
    return this.applications.find((a) => a.applicationId === this.selectedInternalAppId);
  }

  get connectionHasRequiredFields(): boolean {
    const c = this.workspaceConnection;
    if (c.applicationId) return true;
    return !!(c.tenantId?.trim() && c.clientId?.trim() && c.clientSecret?.trim() && c.hostName?.trim());
  }

  get canBrowse(): boolean {
    return this.workspaceConnected && this.connectionHasRequiredFields && !!this.workspaceConnection.siteName?.trim();
  }

  get applicationTypeLabel(): string {
    return this.applicationTypes.find((t) => t.code === this.configMode)?.displayName ?? this.configMode;
  }

  get canNavigateBack(): boolean {
    return this.navHistoryIndex > 0;
  }

  get canNavigateUp(): boolean {
    return !!this.currentPath?.trim();
  }

  get canNavigateForward(): boolean {
    return this.navHistoryIndex < this.navHistory.length - 1;
  }

  get currentLocationLabel(): string {
    if (!this.currentPath) return 'Root';
    return this.currentPath.split('/').pop() ?? 'Root';
  }

  onConfigModeChange(): void {
    this.status.clear();
    this.clearBrowseState();
    this.workspaceConnected = false;
    this.workspaceConnection = this.emptyWorkspaceConnection();
    this.selectedInternalAppId = '';
    this.selectedInternalAppDropdown = null;
    this.savePreferences();
  }

  onInternalApplicationChange(): void {
    this.status.clear();
    this.clearBrowseState();

    const app = this.selectedInternalApp;
    if (!app) {
      this.workspaceConnected = false;
      this.workspaceConnection = this.emptyWorkspaceConnection();
      this.selectedInternalAppDropdown = null;
      return;
    }

    this.syncInternalAppDropdownSelection();
    this.workspaceConnection = {
      applicationId: app.applicationId,
      tenantId: app.tenantId,
      clientId: app.clientId,
      clientSecret: app.clientSecret,
      consumerClientId: app.consumerClientId ?? '',
      consumerSecret: app.consumerSecret ?? '',
      hostName: app.hostName,
      siteName: app.siteName ?? '',
      libraryName: app.libraryName ?? '',
    };
    this.workspaceConnected = true;
    if (this.workspaceConnection.siteName?.trim()) {
      this.refreshLibraries();
    } else {
      this.tryAutoBrowse();
    }
    this.savePreferences();
  }

  refreshLibraries(): void {
    if (!this.connectionHasRequiredFields || !this.workspaceConnection.siteName?.trim()) { this.libraries = []; return; }
    this.loadingLibraries = true;
    this.api.listLibraries(this.workspaceConnection).subscribe({
      next: (libs) => {
        this.libraries = libs;
        this.loadingLibraries = false;
        const current = this.workspaceConnection.libraryName?.trim().toLowerCase();
        if (!current && libs.length) this.workspaceConnection.libraryName = libs[0].name;
        else if (current && !libs.some((l) => l.name.toLowerCase() === current)) {
          this.workspaceConnection.libraryName = libs[0]?.name ?? this.env.defaultLibraryName;
        }
        this.rebuildLibraryDropdownOptions();
        this.tryAutoBrowse();
      },
      error: (err) => { this.loadingLibraries = false; this.libraries = []; this.status.error = parseApiError(err, 'Could not load libraries.'); },
    });
  }

  applyExternalConnection(): void {
    this.status.clear();
    const c = this.workspaceConnection;
    if (!c.tenantId?.trim() || !c.clientId?.trim() || !c.clientSecret?.trim() || !c.hostName?.trim()) {
      this.status.error = 'Tenant ID, Client ID, Client Secret, and Host Name are required.'; return;
    }
    if (!c.siteName?.trim()) { this.status.error = 'Site Name is required to connect.'; return; }
    this.workspaceConnected = true;
    this.status.setSuccess('Connected. Opening your library…');
    this.resetNavigationHistory();
    this.refreshLibraries();
    this.tryAutoBrowse();
  }

  disconnectWorkspace(): void {
    this.clearBrowseState();
    this.workspaceConnected = false;
    if (this.isModeInternal) {
      this.selectedInternalAppId = '';
      this.selectedInternalAppDropdown = null;
      this.workspaceConnection = this.emptyWorkspaceConnection();
    }
  }

  private resetNavigationHistory(): void {
    this.navHistory = [''];
    this.navHistoryIndex = 0;
  }

  private pushNavigation(path: string): void {
    // Truncate forward history when navigating to a new path
    if (this.navHistoryIndex < this.navHistory.length - 1) {
      this.navHistory = this.navHistory.slice(0, this.navHistoryIndex + 1);
    }
    // Don't push duplicate of current
    if (this.navHistory[this.navHistoryIndex] === path) return;
    this.navHistory.push(path);
    this.navHistoryIndex = this.navHistory.length - 1;
  }

  navigateBack(): void {
    if (!this.canNavigateBack) return;
    this.navHistoryIndex--;
    const path = this.navHistory[this.navHistoryIndex];
    this.loadChildren(path, false);
  }

  navigateForward(): void {
    if (!this.canNavigateForward) return;
    this.navHistoryIndex++;
    const path = this.navHistory[this.navHistoryIndex];
    this.loadChildren(path, false);
  }

  navigateRoot(): void {
    this.loadChildren('');
  }

  refreshCurrentFolder(): void {
    this.loadChildren(this.currentPath, true);
  }

  loadChildren(folderPath = '', addToHistory = true): void {
    if (!this.canBrowse) return;
    this.browsing = true;
    this.status.clear();
    this.api.browseFolder(this.workspaceConnection, folderPath).subscribe({
      next: (items) => {
        this.items = sortSharePointItems(items);
        this.currentPath = folderPath;
        this.breadcrumbs = this.buildBreadcrumbs(folderPath);
        this.browsing = false;
        if (addToHistory) this.pushNavigation(folderPath);
      },
      error: (err) => { this.browsing = false; this.items = []; this.status.error = parseApiError(err, 'Failed to load folder.'); },
    });
  }

  openItem(item: SharePointItemDto): void {
    if (item.isFolder) { this.loadChildren(item.path ?? item.name); return; }
    this.selectedItem = item;
    this.viewerKind = detectFileKind(item);
    this.viewerLoading = true;
    this.viewerText = '';
    this.viewerUrl = null;
    this.viewerRawUrl = '';
    this.viewerMediaError = '';
    this.viewerUnsupportedMsg = '';
    this.viewerMediaStatus = '';
    this.clearRichPreviewDom();
    this.previewSvc.teardown();
    this.showViewer = true;
    const filePath = item.path ?? item.name;
    const lower = filePath.toLowerCase();

    if (this.viewerKind === 'video' || this.viewerKind === 'audio') {
      this.playMediaStream(filePath);
      return;
    }

    if (this.viewerKind === 'pdf') {
      void this.runRichPreview(filePath, 'pdf');
      return;
    }

    if (this.viewerKind === 'csv') {
      void this.runRichPreview(filePath, 'csv');
      return;
    }

    if (this.viewerKind === 'excel') {
      void this.runRichPreview(filePath, 'excel');
      return;
    }

    if (this.viewerKind === 'word') {
      if (!lower.endsWith('.docx')) {
        this.viewerUnsupportedMsg =
          'Legacy Word (.doc) and RTF previews are not supported in the browser. Use Download.';
        this.viewerLoading = false;
        return;
      }
      void this.runRichPreview(filePath, 'docx');
      return;
    }

    if (this.viewerKind === 'powerpoint') {
      if (!lower.endsWith('.pptx')) {
        this.viewerUnsupportedMsg = 'Legacy .ppt format is not supported in-browser. Use Download or convert to .pptx.';
        this.viewerLoading = false;
        return;
      }
      void this.runRichPreview(filePath, 'pptx');
      return;
    }

    this.loadFileAsBlob(filePath);
  }

  onMediaError(): void {
    if (this.viewerKind !== 'video' && this.viewerKind !== 'audio') return;
    this.viewerLoading = false;
    this.viewerMediaStatus = '';
    this.viewerMediaError =
      'Stream playback failed (codec or network). Use MP4/M4V (H.264) for best results, or Download. ' +
      'In DevTools Network you should see multiple small GETs with status 206, not one huge download.';
  }

  onMediaCanPlay(mediaEl?: HTMLMediaElement): void {
    this.viewerMediaStatus = '';
    this.viewerMediaError = '';
    this.viewerLoading = false;
    if (this.viewerKind === 'video' && mediaEl) {
      void mediaEl.play().catch(() => {});
    }
  }

  /**
   * Progressive HTTP Range streaming — browser requests byte ranges from the API (no full-file download).
   */
  private playMediaStream(filePath: string): void {
    this.viewerLoading = true;
    this.viewerMediaError = '';
    this.viewerMediaStatus = 'Connecting stream…';
    this.revokeViewerBlobUrl();
    this.viewerUrl = null;

    this.api.resolveStreamingUrl(this.workspaceConnection, filePath).subscribe({
      next: (url) => {
        this.viewerRawUrl = url;
        this.viewerUrl = this.sanitizer.bypassSecurityTrustResourceUrl(url);
        this.viewerLoading = false;
        this.viewerMediaStatus = 'Streaming — playback starts as data arrives…';
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.viewerLoading = false;
        this.viewerMediaStatus = '';
        this.viewerMediaError = parseApiError(err, 'Could not open media stream.');
        this.cdr.markForCheck();
      },
    });
  }

  private revokeViewerBlobUrl(): void {
    if (this.viewerRawUrl.startsWith('blob:')) {
      URL.revokeObjectURL(this.viewerRawUrl);
    }
  }

  private mediaMimeType(item: SharePointItemDto): string {
    const mime = item.mimeType?.trim().toLowerCase();
    if (mime && !mime.includes('octet-stream') && mime !== 'application/binary') return mime;
    const name = (item.path ?? item.name).toLowerCase();
    const map: Record<string, string> = {
      '.mp4': 'video/mp4',
      '.m4v': 'video/mp4',
      '.mov': 'video/quicktime',
      '.qt': 'video/quicktime',
      '.webm': 'video/webm',
      '.avi': 'video/x-msvideo',
      '.wmv': 'video/x-ms-wmv',
      '.asf': 'video/x-ms-asf',
      '.mkv': 'video/x-matroska',
      '.flv': 'video/x-flv',
      '.f4v': 'video/x-flv',
      '.3gp': 'video/3gpp',
      '.3g2': 'video/3gpp2',
      '.mpg': 'video/mpeg',
      '.mpeg': 'video/mpeg',
      '.mpe': 'video/mpeg',
      '.ogv': 'video/ogg',
      '.ts': 'video/mp2t',
      '.m2ts': 'video/mp2t',
      '.mts': 'video/mp2t',
      '.mp3': 'audio/mpeg',
      '.wav': 'audio/wav',
      '.ogg': 'audio/ogg',
      '.oga': 'audio/ogg',
      '.m4a': 'audio/mp4',
      '.aac': 'audio/aac',
      '.flac': 'audio/flac',
      '.wma': 'audio/x-ms-wma',
      '.opus': 'audio/opus',
      '.weba': 'audio/webm',
      '.aiff': 'audio/aiff',
      '.aif': 'audio/aiff',
      '.amr': 'audio/amr',
      '.caf': 'audio/x-caf',
    };
    for (const [ext, type] of Object.entries(map)) {
      if (name.endsWith(ext)) return type;
    }
    return this.viewerKind === 'audio' ? 'audio/mpeg' : 'video/mp4';
  }

  private formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`;
    if (bytes < 1073741824) return `${(bytes / 1048576).toFixed(1)} MB`;
    return `${(bytes / 1073741824).toFixed(1)} GB`;
  }
  private loadFileAsBlob(filePath: string): void {
    this.api.fetchFileBlob(this.workspaceConnection, filePath).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        this.viewerRawUrl = url;
        this.viewerUrl = this.sanitizer.bypassSecurityTrustResourceUrl(url);
        if (this.viewerKind === 'text') blob.text().then((t) => { this.viewerText = t; });
        this.viewerLoading = false;
      },
      error: (err) => {
        this.viewerLoading = false;
        this.status.error = parseApiError(err, 'Failed to load file.');
      },
    });
  }

  private clearRichPreviewDom(): void {
    const el = this.officePreviewHost?.nativeElement;
    if (el) el.innerHTML = '';
  }

  private async waitForPreviewHost(): Promise<HTMLElement | null> {
    for (let i = 0; i < 30; i++) {
      this.cdr.detectChanges();
      await new Promise<void>((r) => requestAnimationFrame(() => r()));
      const host = this.officePreviewHost?.nativeElement;
      if (host) return host;
      await new Promise<void>((r) => setTimeout(r, 20));
    }
    return null;
  }

  /** Loads file via POST then renders in-browser (PDF iframe, pptx-preview, etc.). */
  private async runRichPreview(filePath: string, mode: RichPreviewMode): Promise<void> {
    this.viewerLoading = true;
    this.viewerMediaError = '';
    this.api.fetchFileBlob(this.workspaceConnection, filePath).subscribe({
      next: async (blob) => {
        const host = await this.waitForPreviewHost();
        if (!host) {
          this.viewerMediaError = 'Preview panel did not load. Close and try again.';
          this.viewerLoading = false;
          this.cdr.markForCheck();
          return;
        }
        this.previewSvc.teardown();
        host.innerHTML = '';
        try {
          if (mode === 'pdf') await this.previewSvc.renderPdf(host, blob);
          else if (mode === 'pptx') await this.previewSvc.renderPptx(host, blob);
          else if (mode === 'docx') await this.previewSvc.renderDocx(host, blob);
          else if (mode === 'excel') await this.previewSvc.renderExcelTable(host, blob);
          else if (mode === 'csv') {
            const text = await blob.text();
            this.previewSvc.renderCsvTable(host, text);
          }
        } catch (e) {
          this.viewerMediaError =
            e instanceof Error ? e.message : 'Preview failed for this file. Try Download.';
        } finally {
          this.viewerLoading = false;
          this.cdr.markForCheck();
        }
      },
      error: (err) => {
        this.viewerLoading = false;
        this.viewerMediaError = parseApiError(err, 'Failed to download file for preview.');
        this.cdr.markForCheck();
      },
    });
  }

  closeViewer(): void {
    this.revokeViewerBlobUrl();
    this.clearRichPreviewDom();
    this.previewSvc.teardown();
    this.showViewer = false;
    this.selectedItem = null;
    this.viewerKind = 'unknown';
    this.viewerLoading = false;
    this.viewerUrl = null;
    this.viewerRawUrl = '';
    this.viewerText = '';
    this.viewerMediaError = '';
    this.viewerMediaStatus = '';
    this.viewerUnsupportedMsg = '';
  }

  downloadSelected(): void {
    if (!this.selectedItem) return;
    const filePath = this.selectedItem.path ?? this.selectedItem.name;
    this.api.fetchFileBlob(this.workspaceConnection, filePath).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = this.selectedItem!.name; a.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => { this.status.error = parseApiError(err, 'Download failed.'); },
    });
  }

  private buildBreadcrumbs(folderPath: string): string[] {
    const site = this.workspaceConnection.siteName?.trim() || 'Site';
    const library = this.workspaceConnection.libraryName || 'Documents';
    const folders = folderPath ? folderPath.split('/').filter(Boolean) : [];
    return [site, library, ...folders];
  }

  navigateBreadcrumb(index: number): void {
    if (index <= 1) {
      this.loadChildren('');
      return;
    }
    const folderParts = this.breadcrumbs.slice(2, index + 1);
    this.loadChildren(folderParts.join('/'));
  }

  goBack(): void {
    if (!this.canNavigateUp) return;
    const parts = this.currentPath.split('/').filter(Boolean);
    parts.pop();
    this.loadChildren(parts.join('/'));
  }

  get canShowCurlGuide(): boolean {
    return !!(
      this.workspaceConnection.applicationId &&
      this.workspaceConnection.consumerSecret?.trim()
    );
  }

  async openCurlModal(): Promise<void> {
    if (!this.canShowCurlGuide || this.curlModalLoading) return;

    this.curlModalOpen = true;
    this.curlModalLoading = true;
    this.curlModalData = null;
    this.curlModalCopied = false;

    const sampleFile = this.items.find((i) => !i.isFolder);
    const samplePath = sampleFile
      ? (sampleFile.path ?? (this.currentPath ? `${this.currentPath}/${sampleFile.name}` : sampleFile.name))
      : (this.currentPath ? `${this.currentPath}/your-file.mp4` : 'folder/your-file.mp4');

    this.curlModalData = await generateCurlCommands({
      apiBaseUrl: this.env.apiBaseUrl,
      apiVersion: this.env.apiVersion,
      applicationId: this.workspaceConnection.applicationId!,
      apiSecret: this.workspaceConnection.consumerSecret!.trim(),
      displayName: this.selectedInternalApp?.displayName ?? this.workspaceConnection.siteName ?? 'SharePoint Workspace',
      sampleFolderPath: this.currentPath,
      sampleFilePath: samplePath,
    });
    this.curlModalLoading = false;
  }

  closeCurlModal(): void {
    this.curlModalOpen = false;
    this.curlModalLoading = false;
    this.curlModalCopied = false;
    this.curlStepCopied = null;
  }

  curlHighlight(command: string): SafeHtml {
    return this.sanitizer.bypassSecurityTrustHtml(highlightCurl(command));
  }

  async copyCurlModal(): Promise<void> {
    if (!this.curlModalData) return;
    this.curlModalCopied = await copyCurlText(this.curlModalData.fullText);
    if (this.curlModalCopied) {
      setTimeout(() => { this.curlModalCopied = false; }, 2000);
    }
  }

  async copyCurlStep(step: CurlStep): Promise<void> {
    const ok = await copyCurlText(step.command);
    if (ok) {
      this.curlStepCopied = step.number;
      setTimeout(() => {
        if (this.curlStepCopied === step.number) this.curlStepCopied = null;
      }, 2000);
    }
  }

  downloadCurlScript(): void {
    if (!this.curlModalData) return;
    downloadCurlScript(
      this.curlModalData.fullText,
      curlScriptFilename(this.curlModalData.displayName),
    );
  }

  getFileKind(item: SharePointItemDto): FileKind { return detectFileKind(item); }

  getFileExtension(item: SharePointItemDto): string {
    if (item.isFolder) return 'Folder';
    const name = item.name;
    const dot = name.lastIndexOf('.');
    if (dot <= 0 || dot === name.length - 1) return 'File';
    return name.slice(dot + 1).toUpperCase();
  }

  getFileSize(item: SharePointItemDto): string {
    if (item.isFolder) return `${item.childCount ?? 0} items`;
    if (item.size < 1024) return `${item.size} B`;
    if (item.size < 1048576) return `${(item.size / 1024).toFixed(1)} KB`;
    if (item.size < 1073741824) return `${(item.size / 1048576).toFixed(1)} MB`;
    return `${(item.size / 1073741824).toFixed(1)} GB`;
  }

  private tryAutoBrowse(): void {
    if (this.canBrowse && !this.browsing && !this.items.length) {
      this.loadChildren('');
    }
  }

  private loadApplicationTypes(): void {
    this.api.listApplicationTypes().subscribe({
      next: (types) => { this.applicationTypes = types; },
      error: () => { this.applicationTypes = FALLBACK_APPLICATION_TYPES; },
    });
  }

  private loadApplications(): void {
    this.api.listApplications().subscribe({
      next: (apps) => {
        this.applications = apps;
        this.rebuildInternalAppDropdownOptions();
        this.tryApplyPendingApplication();
      },
      error: (err) => { this.status.error = parseApiError(err, 'Failed to load applications.'); },
    });
  }

  private tryApplyPendingApplication(): void {
    const id = this.pendingApplicationId;
    if (!id) return;
    const app = this.applications.find((a) => a.applicationId === id);
    if (!app) {
      this.status.error = 'Could not find the registered application in the workspace.';
      this.pendingApplicationId = null;
      this.launchApplicationConsumed.emit();
      return;
    }
    this.pendingApplicationId = null;
    this.applyRegisteredApplication(app);
    this.launchApplicationConsumed.emit();
  }

  private applyRegisteredApplication(app: ApplicationDto): void {
    if (isApplicationTypeCode(app.applicationTypeCode)) this.configMode = app.applicationTypeCode;
    if (this.isModeInternal) {
      this.selectedInternalAppId = app.applicationId;
      this.onInternalApplicationChange();
      this.status.setSuccess(`Connected to "${app.displayName}".`);
      return;
    }
    this.workspaceConnection = connectionFromApplication(app);
    this.applyExternalConnection();
  }

  goRegisterInternal(): void { this.registerInternal.emit(); }
  trackByIndex(index: number): number { return index; }
}
