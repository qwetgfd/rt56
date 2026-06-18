import { ChangeDetectorRef, Component, ElementRef, EventEmitter, HostListener, Input, NgZone, OnChanges, OnDestroy, OnInit, Output, SimpleChanges, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml, SafeResourceUrl } from '@angular/platform-browser';
import { SelectDropDownModule, SelectDropDownService } from 'ngx-select-dropdown';
import { MODULE_BRANDING, SP_APPLICATIONS, SP_WORKSPACE, spFolderItemCountLabel } from '../core/sharepoint.messages';
import { SHAREPOINT_ENV } from '../core/sharepoint.config';
import { SharepointIconComponent, SharepointOrbitLoaderComponent, SharepointStatusAlertsComponent } from '../core/sharepoint.ui';
import { ApplicationDto, ApplicationTypeCode, ApplicationTypeDto, FileKind, ProcessConfigSharePointSelection, RichPreviewMode, SharePointItemDto, SharePointLibraryDto, WorkspaceConnection } from '../core/sharepoint.types';
import { applicationTypeDisplayName, awaitApi, buildCurlCommands, buildUserSiteConnectRequest, buildWorkspaceBreadcrumbs, connectionFromApplication, connectionFromApplicationSite, copyCurlText, curlSampleFilePath, CurlModalResult, CurlStep, curlScriptFilename, defaultUserSiteInput, delay, detectFileKind, downloadCurlScript, emptyWorkspaceConnection, findDropdownOption, FolderNavHistory, formatFileSize, highlightCurl, mapApplicationSitesToDropdownOptions, mapApplicationsToDropdownOptions, mapInternalAppsToDropdownOptions, mapLibrariesToDropdownOptions, mapRegisteredSitesToDropdownOptions, mapUserDelegatedSitesToDropdownOptions, normalizeWorkspaceConfigMode, preferredLibraryNames, readWorkspacePrefs, registeredApplicationSites, removeWorkspacePrefs, resolveUnsupportedPreviewMessage, resolveWorkspaceLibraryName, sharePointFolderIconSrc, sharePointItemExtensionLabel, sharePointItemIconSrc, sortSharePointItems, SP_APP_DROPDOWN_CONFIG, SP_LIBRARY_DROPDOWN_CONFIG, SpDropdownOption, StatusAlerts, unwrapNgxDropdownSelection, usesRichPreviewHost, waitForDomHost, workspaceModeFromType, writeWorkspacePrefs, type WorkspaceFileDisplayMode } from '../core/sharepoint.utils';
import type { Observable } from 'rxjs';
import { SharePointApiService } from '../services/sharepoint-api.service';
import { SharePointFilePreviewService } from '../services/sharepoint-preview.service';
import { SharePointUserApiService, SharePointUserAuthService } from '../services/sharepoint-user.service';

@Component({
  selector: 'app-sharepoint-workspace',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SharepointIconComponent,
    SharepointOrbitLoaderComponent,
    SharepointStatusAlertsComponent,
    SelectDropDownModule,
  ],
  templateUrl: './sharepoint-workspace.component.html',
  styleUrls: ['./sharepoint-workspace.component.css', '../sharepoint.styles.css', '../sharepoint-ambient.css'],
  host: {
    class: 'sp-workspace-host',
    '[class.sp-workspace-host--process-config]': 'processConfigMode',
    '[class.sp-workspace-host--file-pick]': 'filePickMode',
  },
})
export class SharepointWorkspaceComponent implements OnInit, OnChanges, OnDestroy {
  readonly env = inject(SHAREPOINT_ENV);
  readonly branding = MODULE_BRANDING;
  readonly m = SP_WORKSPACE;
  private readonly api = inject(SharePointApiService);
  private readonly userApi = inject(SharePointUserApiService);
  readonly auth = inject(SharePointUserAuthService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly previewSvc = inject(SharePointFilePreviewService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly ngZone = inject(NgZone);
  private readonly dropdownSvc = inject(SelectDropDownService);

  // #region SharePoint — Wizard embed: detached CD tree (NG0103)
  private processConfigCdDetached = false;
  // #endregion

  openDropdownId: string | null = null;

  @Input() initialApplicationId: string | null = null;
  @Input() initialApplicationSiteId: string | null = null;
  @Input() initialLibraryName: string | null = null;
  @Input() initialFolderPath: string | null = null;
  @Input() processConfigMode = false;
  @Input() filePickMode = false;
  @Input() pickFileBusy = false;
  @Output() pickFile = new EventEmitter<void>();
  @Output() viewerOpenChange = new EventEmitter<boolean>();
  @Output() navigateHome = new EventEmitter<void>();
  @Output() navigateApplications = new EventEmitter<void>();
  @Output() registerInternal = new EventEmitter<void>();
  @Output() launchApplicationConsumed = new EventEmitter<void>();
  @Output() processConfigSelectionChange = new EventEmitter<void>();

  applicationTypes: ApplicationTypeDto[] = [];
  applications: ApplicationDto[] = [];

  configMode: ApplicationTypeCode = 'tp_internal';
  selectedInternalAppId = '';
  selectedRegisteredSiteId = '';
  selectedFilePickAppId = '';
  selectedFilePickSiteIndex = -1;
  internalAppDropdownOptions: SpDropdownOption[] = [];
  registeredSiteDropdownOptions: SpDropdownOption[] = [];
  filePickAppDropdownOptions: SpDropdownOption[] = [];
  filePickSiteDropdownOptions: SpDropdownOption[] = [];
  userSiteDropdownOptions: SpDropdownOption[] = [];
  selectedInternalAppDropdown: SpDropdownOption | null = null;
  selectedRegisteredSiteDropdown: SpDropdownOption | null = null;
  selectedFilePickAppDropdown: SpDropdownOption | null = null;
  selectedFilePickSiteDropdown: SpDropdownOption | null = null;
  selectedUserSiteDropdown: SpDropdownOption | null = null;
  readonly appDropdownConfig = SP_APP_DROPDOWN_CONFIG;

  libraryDropdownOptions: SpDropdownOption[] = [];
  selectedLibraryDropdown: SpDropdownOption | null = null;
  readonly libraryDropdownConfig = SP_LIBRARY_DROPDOWN_CONFIG;

  fileDisplayMode: WorkspaceFileDisplayMode = 'list';
  private pendingApplicationId: string | null = null;

  libraries: SharePointLibraryDto[] = [];
  loadingLibraries = false;
  librariesManualRefresh = false;
  private librariesRefreshInFlight = false;
  connectingSite = false;
  userSiteReconnecting = false;
  connectLoaderPhase = 1;
  private connectLoaderTimer: ReturnType<typeof setInterval> | null = null;

  workspaceConnection: WorkspaceConnection = {};
  workspaceConnected = false;
  private activeApplicationSiteId: string | null = null;

  browsing = false;
  readonly skeletonRows = Array.from({ length: 8 }, (_, i) => i);
  readonly skeletonGridCells = Array.from({ length: 12 }, (_, i) => i);
  currentPath = '';
  items: SharePointItemDto[] = [];
  breadcrumbs: string[] = [];

  private readonly folderNav = new FolderNavHistory();

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
  private viewerGeneration = 0;

  @ViewChild('officePreviewHost') officePreviewHost?: ElementRef<HTMLDivElement>;

  readonly status = new StatusAlerts();

  sidebarCollapsed = false;
  isCompact = false;
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

  userAccountModalOpen = false;
  userAccountBusy = false;

  userModeConfirmOpen = false;
  private previousConfigMode: ApplicationTypeCode = 'tp_internal';

  async ngOnInit(): Promise<void> {
    if (this.processConfigMode) {
      this.initProcessConfigCdIsolation();
    }
    if (this.initialApplicationId?.trim()) {
      this.pendingApplicationId = this.initialApplicationId.trim();
    }
    this.loadPreferences();
    if (this.filePickMode || this.processConfigMode) {
      this.sidebarCollapsed = false;
    }
    this.checkViewport();
    if (!this.pendingApplicationId) {
      this.configMode = this.configMode || 'tp_internal';
      this.previousConfigMode =
        this.configMode === 'user_delegated' ? 'tp_internal' : this.configMode;
      this.ensureDisconnectedState();
    } else {
      this.previousConfigMode = this.configMode;
    }
    await this.loadApplicationCatalog();
    if (this.processConfigMode) {
      this.ensureDisconnectedState();
      await new Promise<void>((resolve) => {
        this.runProcessConfigAsync(async () => {
          await this.restoreProcessConfigSelection();
          resolve();
        });
      });
    } else if (this.filePickMode) {
      this.ensureDisconnectedState();
    } else if (this.isModeUser && this.auth.isConfigured) {
      await this.initUserDelegatedMode();
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.processConfigMode) return;
    const initialChanged = !!(
      changes['initialApplicationId']
      || changes['initialApplicationSiteId']
      || changes['initialLibraryName']
      || changes['initialFolderPath']
    );
    if (initialChanged) {
      if (this.initialApplicationId?.trim()) {
        this.pendingApplicationId = this.initialApplicationId.trim();
      }
      if (this.applications.length) {
        void this.reloadProcessConfigInitials();
      }
    }
  }

  async reloadProcessConfigInitials(): Promise<void> {
    if (!this.processConfigMode) return;
    await this.scheduleAsync(async () => {
      await this.restoreProcessConfigSelection();
      this.notifyProcessConfigSelectionChange();
    });
  }

  private notifyProcessConfigSelectionChange(): void {
    if (this.processConfigMode) {
      this.processConfigSelectionChange.emit();
    }
  }

  ngOnDestroy(): void {
    if (this.processConfigCdDetached) {
      this.cdr.reattach();
      this.processConfigCdDetached = false;
    }
    this.status.destroy();
    this.stopConnectLoaderAnimation();
    if (this.viewerRawUrl.startsWith('blob:')) {
      URL.revokeObjectURL(this.viewerRawUrl);
    }
    this.clearRichPreviewDom();
    this.previewSvc.teardown();
  }

  get hasRichPreviewHost(): boolean {
    if (!this.showViewer || !this.selectedItem || this.viewerUnsupportedMsg) return false;
    const path = this.selectedItem.path ?? this.selectedItem.name;
    return usesRichPreviewHost(this.viewerKind, path);
  }

  get viewerShowsMedia(): boolean {
    return !this.viewerUnsupportedMsg && !this.viewerLoading && !this.viewerMediaError;
  }

  @HostListener('window:resize')
  onResize(): void {
    this.checkViewport();
  }

  private checkViewport(): void {
    const wasCompact = this.isCompact;
    this.isCompact = window.innerWidth <= 1024;
    this.isMobile = window.innerWidth <= 768;
    if (this.isCompact && !wasCompact && !this.filePickMode) {
      this.sidebarCollapsed = true;
      this.mobileDrawerOpen = false;
    }
  }

  openConfigurePanel(): void {
    this.sidebarCollapsed = false;
    if (this.isCompact) {
      this.mobileDrawerOpen = true;
    }
    this.savePreferences();
  }

  toggleSidebar(): void {
    this.sidebarCollapsed = !this.sidebarCollapsed;
    if (this.isCompact) {
      this.mobileDrawerOpen = !this.sidebarCollapsed;
    } else {
      this.mobileDrawerOpen = false;
    }
    this.savePreferences();
  }

  closeMobileDrawer(): void {
    if (this.isCompact) {
      this.mobileDrawerOpen = false;
      this.sidebarCollapsed = true;
    }
  }

  private loadPreferences(): void {
    const prefs = readWorkspacePrefs();
    if (!prefs) return;
    this.sidebarCollapsed = prefs.sidebarCollapsed ?? false;
    this.configMode = normalizeWorkspaceConfigMode(prefs.configMode);
    this.fileDisplayMode = prefs.fileDisplayMode === 'grid' ? 'grid' : 'list';
  }

  setFileDisplayMode(mode: WorkspaceFileDisplayMode): void {
    if (this.fileDisplayMode === mode) {
      return;
    }
    this.fileDisplayMode = mode;
    this.savePreferences();
    this.refreshView();
  }

  private savePreferences(): void {
    writeWorkspacePrefs({
      sidebarCollapsed: this.sidebarCollapsed,
      configMode: this.configMode,
      fileDisplayMode: this.fileDisplayMode,
    });
  }

  internalApplications(): ApplicationDto[] {
    return this.applications.filter((a) => a.applicationTypeCode === 'tp_internal');
  }

  userDelegatedSites(): ApplicationDto[] {
    return this.applications.filter((a) => a.applicationTypeCode === 'tp_user_delegated');
  }

  selectedRegisteredSite(): ApplicationDto | undefined {
    return this.applications.find((a) => a.applicationId === this.selectedRegisteredSiteId);
  }

  selectedUserSite = this.selectedRegisteredSite;

  selectedFilePickApplication(): ApplicationDto | undefined {
    return this.applications.find((a) => a.applicationId === this.selectedFilePickAppId);
  }

  selectedFilePickSite() {
    const app = this.selectedFilePickApplication();
    if (!app || this.selectedFilePickSiteIndex < 0) return undefined;
    return registeredApplicationSites(app)[this.selectedFilePickSiteIndex];
  }

  selectedInternalApp(): ApplicationDto | undefined {
    return this.applications.find((a) => a.applicationId === this.selectedInternalAppId);
  }

  filePickDisconnectedHint(): string {
    return this.applications.length ? SP_WORKSPACE.connect.filePickHasApps : SP_WORKSPACE.connect.filePickNoApps;
  }

  private clearBrowseState(): void {
    this.items = [];
    this.currentPath = '';
    this.breadcrumbs = [];
    this.libraries = [];
    this.libraryDropdownOptions = [];
    this.selectedLibraryDropdown = null;
    this.browsing = false;
    this.connectingSite = false;
    this.resetNavigationHistory();
    this.closeViewer();
  }

  private rebuildDropdownOptions(): void {
    this.internalAppDropdownOptions = mapInternalAppsToDropdownOptions(this.applications);
    this.registeredSiteDropdownOptions = mapRegisteredSitesToDropdownOptions(this.applications);
    this.filePickAppDropdownOptions = mapApplicationsToDropdownOptions(this.applications);
    this.userSiteDropdownOptions = mapUserDelegatedSitesToDropdownOptions(this.applications);
    this.selectedInternalAppDropdown = findDropdownOption(this.internalAppDropdownOptions, this.selectedInternalAppId);
    this.selectedRegisteredSiteDropdown = findDropdownOption(this.registeredSiteDropdownOptions, this.selectedRegisteredSiteId);
    this.selectedUserSiteDropdown = findDropdownOption(this.userSiteDropdownOptions, this.selectedRegisteredSiteId);
    this.selectedFilePickAppDropdown = findDropdownOption(this.filePickAppDropdownOptions, this.selectedFilePickAppId);
    this.rebuildFilePickSiteDropdownOptions();
  }

  private rebuildFilePickSiteDropdownOptions(): void {
    const app = this.selectedFilePickApplication();
    this.filePickSiteDropdownOptions = app ? mapApplicationSitesToDropdownOptions(app) : [];
    const id = this.selectedFilePickSiteIndex >= 0 ? String(this.selectedFilePickSiteIndex) : '';
    this.selectedFilePickSiteDropdown = findDropdownOption(this.filePickSiteDropdownOptions, id);
  }

  private rebuildLibraryDropdownOptions(): void {
    this.libraryDropdownOptions = mapLibrariesToDropdownOptions(this.libraries);
    this.selectedLibraryDropdown = findDropdownOption(this.libraryDropdownOptions, this.workspaceConnection.libraryName);
  }

  onWorkspaceDropdownOpen(instanceId: string): void {
    for (const id of this.workspaceDropdownIds) {
      if (id !== instanceId) this.dropdownSvc.closeDropdown(id);
    }
    this.openDropdownId = instanceId;
    this.refreshView();
  }

  onWorkspaceDropdownClose(): void {
    this.openDropdownId = null;
    this.refreshView();
  }

  private readonly workspaceDropdownIds = [
    'sp-internal-app',
    'sp-registered-site',
    'sp-filepick-app',
    'sp-filepick-site',
    'sp-user-site',
    'sp-library',
  ] as const;

  private closeAllWorkspaceDropdowns(): void {
    for (const id of this.workspaceDropdownIds) {
      this.dropdownSvc.closeDropdown(id);
    }
    this.openDropdownId = null;
  }

  private scheduleCloseWorkspaceDropdowns(): void {
    setTimeout(() => {
      this.closeAllWorkspaceDropdowns();
      this.refreshView();
    }, 0);
  }

  // #region SharePoint — Stop workspace CD from participating in wizard form ticks
  private initProcessConfigCdIsolation(): void {
    if (this.processConfigCdDetached) {
      return;
    }
    this.processConfigCdDetached = true;
    this.cdr.detach();
  }

  // #region SharePoint — Refresh detached workspace view only
  private processConfigUiRefresh(): void {
    if (!this.processConfigCdDetached) {
      return;
    }
    this.cdr.detectChanges();
  }

  // #region SharePoint — HTTP/async outside zone, one local UI refresh
  private runProcessConfigAsync(work: () => void | Promise<void>): void {
    this.ngZone.runOutsideAngular(() => {
      void Promise.resolve(work()).finally(() => {
        this.ngZone.run(() => this.processConfigUiRefresh());
      });
    });
  }

  private scheduleAsync(work: () => Promise<void>): Promise<void> | void {
    return this.processConfigMode
      ? new Promise<void>((resolve) => { this.runProcessConfigAsync(async () => { await work(); resolve(); }); })
      : work();
  }

  private refreshView(): void {
    if (this.processConfigMode) {
      this.processConfigUiRefresh();
      return;
    }
    this.cdr.markForCheck();
  }

  selectedFilePickSiteValue(): string {
    return this.selectedFilePickSiteIndex >= 0 ? String(this.selectedFilePickSiteIndex) : '';
  }

  filePickSites() {
    const app = this.selectedFilePickApplication();
    return app ? registeredApplicationSites(app) : [];
  }

  onFilePickAppSelect(raw: string): void {
    const nextId = raw?.trim() ?? '';
    if (!nextId) {
      if (this.selectedFilePickAppId) {
        this.selectedFilePickAppId = '';
        this.selectedFilePickSiteIndex = -1;
        this.rebuildFilePickSiteDropdownOptions();
        this.disconnectWorkspace();
        this.refreshView();
      }
      return;
    }
    if (nextId === this.selectedFilePickAppId) return;
    this.selectedFilePickAppId = nextId;
    this.selectedFilePickSiteIndex = -1;
    this.selectedFilePickSiteDropdown = null;
    this.rebuildFilePickSiteDropdownOptions();
    this.disconnectWorkspace();
    const sites = this.filePickSites();
    if (this.processConfigMode) {
      this.processConfigUiRefresh();
    }
    if (sites.length === 1) {
      this.selectedFilePickSiteIndex = 0;
      this.selectedFilePickSiteDropdown = findDropdownOption(this.filePickSiteDropdownOptions, String(this.selectedFilePickSiteIndex));
      void this.scheduleAsync(async () => { await this.applyFilePickSiteConnection(); });
    } else if (this.processConfigMode) {
      this.processConfigUiRefresh();
    }
  }

  onFilePickAppDropdownModelChange(model: unknown): void {
    const item = unwrapNgxDropdownSelection(model);
    this.selectedFilePickAppDropdown = item;
    const nextId = item?.id?.trim() ?? '';
    this.onFilePickAppSelect(nextId);
  }

  onFilePickSiteSelect(raw: string): void {
    const nextIndex = raw === '' ? -1 : Number.parseInt(raw, 10);
    if (!Number.isFinite(nextIndex) || nextIndex < 0) {
      if (this.selectedFilePickSiteIndex >= 0) {
        this.selectedFilePickSiteIndex = -1;
        this.disconnectWorkspace();
        this.refreshView();
      }
      return;
    }
    if (nextIndex === this.selectedFilePickSiteIndex) return;
    this.selectedFilePickSiteIndex = nextIndex;
    this.selectedFilePickSiteDropdown = findDropdownOption(this.filePickSiteDropdownOptions, String(this.selectedFilePickSiteIndex));
    void this.scheduleAsync(async () => { await this.applyFilePickSiteConnection(); });
    if (this.processConfigMode) {
      this.processConfigUiRefresh();
    }
  }

  onFilePickSiteDropdownModelChange(model: unknown): void {
    const item = unwrapNgxDropdownSelection(model);
    this.selectedFilePickSiteDropdown = item;
    const raw = item?.id?.trim() ?? '';
    this.onFilePickSiteSelect(raw);
  }

  onFilePickLibrarySelect(libraryName: string): void {
    const nextName = libraryName?.trim() ?? '';
    if (!nextName || nextName === this.workspaceConnection.libraryName) return;
    this.workspaceConnection.libraryName = nextName;
    this.selectedLibraryDropdown = findDropdownOption(this.libraryDropdownOptions, nextName);
    this.onLibraryChange();
  }

  onFilePickLibraryDropdownModelChange(model: unknown): void {
    const item = unwrapNgxDropdownSelection(model);
    this.selectedLibraryDropdown = item;
    const nextName = item?.id?.trim() ?? '';
    if (!nextName || nextName === this.workspaceConnection.libraryName) return;
    this.workspaceConnection.libraryName = nextName;
    this.onLibraryChange();
  }

  private async applyFilePickSiteConnection(): Promise<void> {
    const app = this.selectedFilePickApplication();
    const site = this.selectedFilePickSite();
    if (!app || !site) return;

    this.status.clear();
    this.clearBrowseState();
    const typeCode = app.applicationTypeCode?.trim();
    this.configMode = workspaceModeFromType(typeCode);
    this.selectedRegisteredSiteId = app.applicationId;
    this.workspaceConnection = connectionFromApplicationSite(app, site);
    this.activeApplicationSiteId = site.applicationSiteId ?? null;

    if (typeCode === 'tp_user_delegated') {
      this.userSiteInput = site.siteName;
      await this.initUserDelegatedMode();
      await this.connectUserSite();
      return;
    }

    if (typeCode === 'tp_internal') {
      this.selectedInternalAppId = app.applicationId;
      this.selectedInternalAppDropdown = findDropdownOption(this.internalAppDropdownOptions, this.selectedInternalAppId);
      this.workspaceConnected = true;
      this.clearBrowseStateForReconnect();
      await (this.processConfigMode ? this.refreshLibrariesCore() : this.refreshLibraries());
      this.notifyProcessConfigSelectionChange();
      return;
    }

    await this.applyExternalConnection();
    this.notifyProcessConfigSelectionChange();
  }

  onRegisteredSiteDropdownModelChange(model: unknown): void {
    const item = unwrapNgxDropdownSelection(model);
    this.selectedRegisteredSiteDropdown = item;
    const nextId = item?.id?.trim() ?? '';
    if (!nextId) {
      if (this.selectedRegisteredSiteId) {
        this.selectedRegisteredSiteId = '';
        this.disconnectWorkspace();
      }
      return;
    }
    if (nextId === this.selectedRegisteredSiteId) return;
    this.selectedRegisteredSiteId = nextId;
    this.onRegisteredSiteChange();
  }

  onUserSiteDropdownModelChange(model: unknown): void {
    const item = unwrapNgxDropdownSelection(model);
    this.selectedUserSiteDropdown = item;
    const nextId = item?.id?.trim() ?? '';
    if (!nextId) {
      if (this.selectedRegisteredSiteId) {
        this.selectedRegisteredSiteId = '';
        this.disconnectWorkspace();
      }
      return;
    }
    if (nextId === this.selectedRegisteredSiteId) return;
    this.selectedRegisteredSiteId = nextId;
    this.onRegisteredUserSiteChange();
  }

  onRegisteredSiteChange(): void {
    const app = this.selectedRegisteredSite();
    if (!app) return;
    void this.applyRegisteredApplication(app);
  }

  onRegisteredUserSiteChange(): void {
    const app = this.selectedUserSite();
    if (!app) return;
    this.configMode = 'user_delegated';
    this.userSiteInput = app.siteName ?? '';
    this.workspaceConnection.hostName = app.hostName;
    this.workspaceConnection.siteName = app.siteName ?? '';
    this.workspaceConnection.libraryName = app.libraryName ?? '';
    void this.connectUserSite();
  }

  onInternalAppDropdownModelChange(model: unknown): void {
    const item = unwrapNgxDropdownSelection(model);
    this.selectedInternalAppDropdown = item;
    const nextId = item?.id?.trim() ?? '';
    if (!nextId) {
      if (this.selectedInternalAppId) {
        this.selectedInternalAppId = '';
        void this.onInternalApplicationChange();
      }
      return;
    }
    if (nextId === this.selectedInternalAppId) return;
    this.selectedInternalAppId = nextId;
    void this.onInternalApplicationChange();
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
    this.selectedFilePickAppId = '';
    this.selectedFilePickSiteIndex = -1;
    this.selectedFilePickAppDropdown = null;
    this.selectedFilePickSiteDropdown = null;
    this.libraries = [];
    this.items = [];
    this.currentPath = '';
    this.breadcrumbs = [];
    this.resetNavigationHistory();
    this.workspaceConnection = emptyWorkspaceConnection();
  }

  resetWorkspace(): void {
    this.status.clear();
    this.pendingApplicationId = null;
    this.curlModalOpen = false;
    this.curlModalData = null;
    this.userAccountModalOpen = false;
    this.userModeConfirmOpen = false;
    this.closeViewer();
    this.configMode = 'tp_internal';
    this.ensureDisconnectedState();
    this.wsSecretVisible = false;
    this.wsSummarySecretVisible = false;
    this.wsConsumerSummarySecretVisible = false;
    removeWorkspacePrefs();
    this.savePreferences();
    this.status.setSuccess(SP_WORKSPACE.connect.workspaceReset);
  }

  async onLibraryChange(): Promise<void> {
    if (!this.workspaceConnected || !this.workspaceConnection.libraryName?.trim()) return;
    this.currentPath = '';
    this.breadcrumbs = [];
    this.resetNavigationHistory();
    await this.loadChildren('');
  }

  get userSiteUrlPlaceholder(): string {
    if (this.env.siteName && this.env.hostName) {
      return SP_WORKSPACE.sidebar.siteUrlPlaceholderWithEnv(this.env.hostName, this.env.siteName);
    }
    return SP_WORKSPACE.sidebar.siteUrlPlaceholder;
  }

  get isModeInternal(): boolean { return this.configMode === 'tp_internal'; }
  get isModeExternal(): boolean { return this.configMode === 'tp_external'; }
  get isModeUser(): boolean { return this.configMode === 'user_delegated'; }

  get connectionHasRequiredFields(): boolean {
    const c = this.workspaceConnection;
    if (this.isModeUser) {
      if (this.filePickMode) {
        return !!this.selectedFilePickAppId && this.selectedFilePickSiteIndex >= 0 && this.auth.isConfigured;
      }
      if (this.userDelegatedSites().length) {
        return !!this.selectedRegisteredSiteId && this.auth.isConfigured;
      }
      return !!this.userSiteInput.trim() && this.auth.isConfigured;
    }
    if (c.applicationId) return true;
    return !!(c.tenantId?.trim() && c.clientId?.trim() && c.clientSecret?.trim() && c.hostName?.trim());
  }

  get canBrowse(): boolean {
    if (!this.workspaceConnected) return false;
    if (this.isModeUser) {
      return !!this.selectedUserLibraryDriveId() && !!this.workspaceConnection.libraryName?.trim();
    }
    return this.connectionHasRequiredFields && !!this.workspaceConnection.siteName?.trim();
  }

  get canConnectUser(): boolean {
    return this.isModeUser && this.connectionHasRequiredFields && this.auth.isConfigured;
  }

  get isWorkspaceConnecting(): boolean {
    return this.connectingSite || this.loadingLibraries;
  }

  get showInlineConnectLoader(): boolean {
    return this.isWorkspaceConnecting;
  }

  get showExplorerMain(): boolean {
    if (this.processConfigMode) {
      return this.workspaceConnected && !this.connectingSite;
    }
    return this.workspaceConnected && !this.connectingSite && !this.loadingLibraries;
  }

  get showConnectLoaderSteps(): boolean {
    return this.showInlineConnectLoader && !(this.loadingLibraries && this.workspaceConnected);
  }

  get workspaceLoaderTitleLine(): string {
    const c = SP_WORKSPACE.connect;
    if (this.loadingLibraries) {
      return this.librariesManualRefresh ? c.refreshing : c.fetchingLibraries;
    }
    if (this.userSiteReconnecting) return c.reconnectingToWorkspace;
    if (this.connectingSite && this.workspaceConnected) return c.reconnectingToWorkspace;
    return c.connectingToWorkspace;
  }

  get workspaceLoaderHint(): string {
    const c = SP_WORKSPACE.connect;
    if (this.loadingLibraries) {
      return this.librariesManualRefresh ? c.refreshingLibrariesHint : c.fetchingLibrariesHint;
    }
    return c.connectingToWorkspaceHint;
  }

  get workspaceLoaderAriaLabel(): string {
    if (this.loadingLibraries) {
      return this.librariesManualRefresh
        ? SP_WORKSPACE.connect.refreshing
        : SP_WORKSPACE.connect.fetchingLibraries;
    }
    return this.workspaceLoaderTitleLine;
  }

  get connectButtonLabel(): string {
    const c = SP_WORKSPACE.connect;
    if (this.isWorkspaceConnecting) return this.userSiteReconnecting ? c.reconnectingEllipsis : c.connectingEllipsis;
    return this.workspaceConnected ? c.reconnect : c.connect;
  }

  get applicationTypeLabel(): string {
    if (this.isModeUser) return SP_APPLICATIONS.typeFallback.userDelegated;
    return applicationTypeDisplayName(this.applicationTypes, this.configMode);
  }

  get canNavigateBack(): boolean {
    return this.folderNav.canBack;
  }

  get canNavigateUp(): boolean {
    return !!this.currentPath?.trim();
  }

  selectedUserLibraryDriveId(): string {
    const name = this.workspaceConnection.libraryName?.trim();
    if (!name) return '';
    return this.libraries.find((l) => l.name === name)?.id ?? '';
  }

  get canNavigateForward(): boolean {
    return this.folderNav.canForward;
  }

  filteredItems(): SharePointItemDto[] {
    if (!this.filePickMode || this.processConfigMode) return this.items;
    return this.items.filter((item) => item.isFolder || /\.(csv|xlsx)$/i.test(item.name));
  }

  processConfigurationSelection(): ProcessConfigSharePointSelection | null {
    if (!this.processConfigMode || !this.workspaceConnected) return null;
    const applicationId = this.workspaceConnection.applicationId?.trim();
    if (!applicationId) return null;
    const site = this.selectedFilePickSite();
    const libraryName = this.workspaceConnection.libraryName?.trim();
    if (!libraryName) return null;
    return {
      sharePointApplicationId: applicationId,
      sharePointApplicationSiteId: site?.applicationSiteId?.trim()
        ?? this.activeApplicationSiteId
        ?? '',
      sharePointLibraryName: libraryName,
      sharePointFolderPath: this.currentPath?.trim() ?? '',
    };
  }

  processConfigurationSelectionValid(): boolean {
    const selection = this.processConfigurationSelection();
    return !!selection?.sharePointApplicationId
      && !!selection.sharePointLibraryName;
  }

  private async restoreProcessConfigSelection(): Promise<void> {
    const appId = this.initialApplicationId?.trim()
      ?? this.pendingApplicationId?.trim();
    if (!appId) return;
    this.selectedFilePickAppId = appId;
    this.rebuildFilePickSiteDropdownOptions();
    const sites = this.filePickSites();
    if (!sites.length) return;

    const siteId = this.initialApplicationSiteId?.trim();
    let siteIndex = 0;
    if (siteId) {
      const found = sites.findIndex((s) => s.applicationSiteId === siteId);
      if (found >= 0) siteIndex = found;
    }
    this.selectedFilePickSiteIndex = siteIndex;
    this.selectedFilePickAppDropdown = findDropdownOption(this.filePickAppDropdownOptions, this.selectedFilePickAppId);
    this.selectedFilePickSiteDropdown = findDropdownOption(this.filePickSiteDropdownOptions, String(this.selectedFilePickSiteIndex));
    await this.applyFilePickSiteConnection();

    if (this.initialLibraryName?.trim()) {
      this.workspaceConnection.libraryName = this.initialLibraryName.trim();
      this.selectedLibraryDropdown = findDropdownOption(this.libraryDropdownOptions, this.workspaceConnection.libraryName);
    }
    if (this.initialFolderPath?.trim() && this.workspaceConnected) {
      this.currentPath = this.initialFolderPath.trim();
      this.breadcrumbs = buildWorkspaceBreadcrumbs({
        isUserMode: this.isModeUser,
        userSiteTitle: this.userSiteInput,
        siteName: this.workspaceConnection.siteName,
        libraryName: this.workspaceConnection.libraryName,
        folderPath: this.currentPath,
      });
      await (this.processConfigMode ? this.loadChildrenCore(this.currentPath) : this.loadChildren(this.currentPath));
    }
    this.refreshView();
    this.notifyProcessConfigSelectionChange();
  }

  get currentLocationLabel(): string {
    if (!this.currentPath) return SP_WORKSPACE.root;
    return this.currentPath.split('/').pop() ?? SP_WORKSPACE.root;
  }

  onConfigModeChange(): void {
    if (this.configMode === 'user_delegated') {
      this.configMode = this.previousConfigMode;
      this.userModeConfirmOpen = true;
      this.refreshView();
      return;
    }
    this.applyConfigModeChange();
  }

  onSignedInUserOptionClick(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    if (this.isModeUser) return;
    this.userModeConfirmOpen = true;
    this.refreshView();
  }

  confirmSignedInUserMode(): void {
    this.userModeConfirmOpen = false;
    this.configMode = 'user_delegated';
    this.applyConfigModeChange();
  }

  cancelSignedInUserMode(): void {
    this.userModeConfirmOpen = false;
    this.configMode = this.previousConfigMode;
    this.refreshView();
  }

  private applyConfigModeChange(): void {
    this.previousConfigMode = this.configMode;
    this.status.clear();
    this.clearBrowseState();
    this.workspaceConnected = false;
    this.workspaceConnection = emptyWorkspaceConnection();
    this.selectedInternalAppId = '';
    this.selectedInternalAppDropdown = null;
    if (this.isModeUser) {
      this.applyUserEnvironmentDefaults();
      if (this.auth.isConfigured) void this.initUserDelegatedMode();
    }
    this.savePreferences();
  }

  private async initUserDelegatedMode(): Promise<void> {
    try {
      await this.auth.initialize();
      if (this.auth.consumeConnectAfterLogin()) {
        await this.connectUserSite();
      }
    } catch (e) {
      this.status.setApiError(e, SP_WORKSPACE.auth.restoreSessionFailed);
    }
  }

  openUserAccountModal(): void {
    this.userAccountModalOpen = true;
  }

  closeUserAccountModal(): void {
    if (!this.userAccountBusy) this.userAccountModalOpen = false;
  }

  async signInUserPopup(): Promise<void> {
    this.userAccountBusy = true;
    try {
      await this.auth.signInPopup();
      this.userAccountModalOpen = false;
    } catch (e) {
      this.status.setApiError(e, SP_WORKSPACE.auth.signInFailed);
    } finally {
      this.userAccountBusy = false;
    }
  }

  async consentUserPopup(): Promise<void> {
    this.userAccountBusy = true;
    try {
      await this.auth.consentPopup();
      this.status.setSuccess(SP_WORKSPACE.auth.permissionsUpdated);
    } catch (e) {
      this.status.setApiError(e, SP_WORKSPACE.auth.consentFailed);
    } finally {
      this.userAccountBusy = false;
    }
  }

  async signOutUserFromModal(): Promise<void> {
    this.closeUserAccountModal();
    await this.auth.signOut();
  }

  async onInternalApplicationChange(): Promise<void> {
    this.status.clear();
    this.clearBrowseState();

    const app = this.selectedInternalApp();
    if (!app) {
      this.workspaceConnected = false;
      this.workspaceConnection = emptyWorkspaceConnection();
      this.selectedInternalAppDropdown = null;
      return;
    }

    this.selectedInternalAppDropdown = findDropdownOption(this.internalAppDropdownOptions, this.selectedInternalAppId);
    this.workspaceConnection = connectionFromApplication(app);
    this.workspaceConnected = true;
    this.clearBrowseStateForReconnect();
    if (this.workspaceConnection.siteName?.trim()) {
      await this.refreshLibraries();
    } else {
      await this.loadChildren('');
    }
    this.savePreferences();
  }

  async refreshLibraries(fromUser = false): Promise<void> {
    return this.scheduleAsync(async () => {
      this.librariesManualRefresh = fromUser;
      try {
        await this.refreshLibrariesCore();
      } finally {
        this.librariesManualRefresh = false;
      }
    });
  }

  private async refreshLibrariesCore(): Promise<void> {
    if (this.isModeUser) {
      if (!this.userSiteInput.trim()) { this.libraries = []; return; }
    } else if (!this.connectionHasRequiredFields || !this.workspaceConnection.siteName?.trim()) {
      this.libraries = []; return;
    }
    if (this.isModeUser) {
      await this.refreshUserSiteLibraries();
      return;
    }
    if (this.librariesRefreshInFlight) {
      return;
    }
    this.librariesRefreshInFlight = true;
    this.loadingLibraries = true;
    this.startConnectLoaderAnimation();
    if (this.processConfigMode) {
      this.processConfigUiRefresh();
    }
    try {
      const result = await awaitApi(this.api.listLibraries(this.workspaceConnection), SP_WORKSPACE.connect.couldNotLoadLibraries);
      if (!result.ok) {
        this.libraries = [];
        this.workspaceConnection.libraryName = '';
        this.selectedLibraryDropdown = null;
        this.status.setApiError(result.error, SP_WORKSPACE.connect.couldNotLoadLibraries);
        return;
      }
      await this.applyLibraries(result.value);
    } finally {
      this.loadingLibraries = false;
      this.librariesRefreshInFlight = false;
      this.stopConnectLoaderAnimation();
      if (!this.processConfigMode) {
        this.refreshView();
      }
    }
  }

  private async applyLibraries(libs: SharePointLibraryDto[]): Promise<void> {
    this.libraries = libs;
    const resolvedName = resolveWorkspaceLibraryName(
      libs,
      this.workspaceConnection.libraryName,
      preferredLibraryNames({
        libraryName: this.env.libraryName,
        defaultLibraryName: this.env.defaultLibraryName,
      }),
    );
    this.workspaceConnection.libraryName = resolvedName;
    this.rebuildLibraryDropdownOptions();
    if (!this.processConfigMode) {
      this.refreshView();
    }
    await this.tryAutoBrowse();
  }

  async applyExternalConnection(): Promise<void> {
    this.status.clear();
    const c = this.workspaceConnection;
    if (!c.tenantId?.trim() || !c.clientId?.trim() || !c.clientSecret?.trim() || !c.hostName?.trim()) {
      this.status.setApiErrorMessage(SP_WORKSPACE.connect.externalFieldsRequired);
      return;
    }
    if (!c.siteName?.trim()) {
      this.status.setApiErrorMessage(SP_WORKSPACE.connect.siteNameRequired);
      return;
    }
    const reconnecting = this.workspaceConnected;
    if (reconnecting) this.clearBrowseStateForReconnect();
    this.workspaceConnected = true;
    this.status.setSuccess(reconnecting ? SP_WORKSPACE.connect.connectionUpdated : SP_WORKSPACE.connect.connectedOpening, 5000);
    this.resetNavigationHistory();
    await this.refreshLibraries();
  }

  disconnectWorkspace(): void {
    this.clearBrowseState();
    this.workspaceConnected = false;
    this.userSiteReconnecting = false;
    this.stopConnectLoaderAnimation();
    if (this.filePickMode) {
      this.workspaceConnection = emptyWorkspaceConnection();
      return;
    }
    if (this.isModeInternal) {
      this.selectedInternalAppId = '';
      this.selectedInternalAppDropdown = null;
      this.workspaceConnection = emptyWorkspaceConnection();
    } else if (this.isModeUser) {
      this.applyUserEnvironmentDefaults();
      this.libraries = [];
      this.rebuildLibraryDropdownOptions();
    }
  }

  async connectUserSite(): Promise<void> {
    this.status.clear();
    if (!this.canConnectUser) {
      this.status.setApiErrorMessage(
        this.userDelegatedSites().length
          ? SP_WORKSPACE.connect.selectSiteAndSignIn
          : SP_WORKSPACE.connect.enterSiteLabel(MODULE_BRANDING.siteUrlOrNameLabel.toLowerCase()),
      );
      return;
    }
    if (!this.auth.isSignedIn()) {
      this.status.setApiErrorMessage(SP_WORKSPACE.connect.signInViaToolbar);
      this.openUserAccountModal();
      return;
    }
    this.userSiteReconnecting = this.workspaceConnected;
    this.clearBrowseStateForReconnect();
    await this.refreshUserSiteLibraries(true);
  }

  private clearBrowseStateForReconnect(): void {
    this.items = [];
    this.currentPath = '';
    this.breadcrumbs = [];
    this.resetNavigationHistory();
    this.closeViewer();
  }

  private startConnectLoaderAnimation(): void {
    this.stopConnectLoaderAnimation();
    this.connectLoaderPhase = 1;
    // #region SharePoint — No interval CD in Configuration First embed
    if (this.processConfigMode) {
      return;
    }
    // #endregion
    this.connectLoaderTimer = setInterval(() => {
      if (this.connectLoaderPhase < 3) {
        this.connectLoaderPhase += 1;
        this.refreshView();
      }
    }, 650);
  }

  private stopConnectLoaderAnimation(): void {
    if (this.connectLoaderTimer != null) {
      clearInterval(this.connectLoaderTimer);
      this.connectLoaderTimer = null;
    }
    this.connectLoaderPhase = 1;
  }

  private async refreshUserSiteLibraries(connecting = false): Promise<void> {
    const registered = this.selectedUserSite();
    const input = registered
      ? `${registered.hostName}/${registered.siteName ?? ''}`.replace(/\/+$/, '')
      : this.userSiteInput.trim();
    if (!input) {
      this.status.setApiErrorMessage(
        this.userDelegatedSites.length
          ? SP_WORKSPACE.connect.selectRegisteredSite
          : SP_WORKSPACE.connect.enterSiteLabel(MODULE_BRANDING.siteUrlOrNameLabel.toLowerCase()),
      );
      return;
    }
    this.loadingLibraries = true;
    this.connectingSite = connecting;
    const connectStartedAt = connecting ? Date.now() : 0;
    this.startConnectLoaderAnimation();
    try {
      const result = await awaitApi(
        this.userApi.connectSite(buildUserSiteConnectRequest(input, registered?.hostName ?? this.env.hostName)),
        SP_WORKSPACE.connect.couldNotOpenSite,
      );
      if (connecting) {
        const minLoaderMs = 4500;
        const elapsed = Date.now() - connectStartedAt;
        if (elapsed < minLoaderMs) await delay(minLoaderMs - elapsed);
      }
      if (!result.ok) {
        this.libraries = [];
        this.status.setApiError(result.error, SP_WORKSPACE.connect.couldNotOpenSite);
        const debug = await awaitApi(
          this.userApi.debugMeDrives(buildUserSiteConnectRequest(input, registered?.hostName ?? this.env.hostName)),
          'debug-me-drives',
        );
        if (debug.ok) console.log('[SharePoint User] debug-me-drives after connect failure', debug.value);
        else console.warn('[SharePoint User] debug-me-drives also failed', debug.error);
        return;
      }
      const site = result.value;
      this.workspaceConnected = true;
      this.workspaceConnection.hostName = site.hostName;
      this.userSiteTitle = site.siteTitle;
      this.applyLibraries(site.libraries);
      this.status.setSuccess(
        connecting ? SP_WORKSPACE.connect.connectedToSite(site.siteTitle) : SP_WORKSPACE.connect.librariesRefreshed,
        5000,
      );
    } finally {
      this.loadingLibraries = false;
      this.connectingSite = false;
      this.userSiteReconnecting = false;
      this.stopConnectLoaderAnimation();
      this.refreshView();
    }
  }

  userSiteInput = '';
  userSiteTitle = '';

  private applyUserEnvironmentDefaults(): void {
    this.workspaceConnection.hostName = this.env.hostName;
    this.workspaceConnection.libraryName = '';
    if (!this.userSiteInput.trim()) {
      this.userSiteInput = defaultUserSiteInput(this.env);
    }
    this.userSiteTitle = '';
  }

  private resetNavigationHistory(): void {
    this.folderNav.reset();
  }

  async navigateBack(): Promise<void> {
    if (!this.canNavigateBack) return;
    await this.loadChildren(this.folderNav.back(), false);
  }

  async navigateForward(): Promise<void> {
    if (!this.canNavigateForward) return;
    await this.loadChildren(this.folderNav.forward(), false);
  }

  async navigateRoot(): Promise<void> {
    await this.loadChildren('');
  }

  async refreshCurrentFolder(): Promise<void> {
    await this.loadChildren(this.currentPath, true);
  }

  async loadChildren(folderPath = '', addToHistory = true): Promise<void> {
    return this.scheduleAsync(async () => { await this.loadChildrenCore(folderPath, addToHistory); });
  }

  private async loadChildrenCore(folderPath = '', addToHistory = true): Promise<void> {
    if (!this.canBrowse) return;
    this.browsing = true;
    if (this.processConfigMode) {
      this.processConfigUiRefresh();
    } else {
      this.refreshView();
    }
    this.status.clearErrors();
    const browse$ = this.isModeUser
      ? this.userApi.browseFolder(this.selectedUserLibraryDriveId(), folderPath)
      : this.api.browseFolder(this.workspaceConnection, folderPath);
    const result = await awaitApi(browse$, SP_WORKSPACE.connect.failedLoadFolder);
    this.browsing = false;
    if (!result.ok) {
      this.items = [];
      this.status.setApiError(result.error, SP_WORKSPACE.connect.failedLoadFolder);
      this.refreshView();
      return;
    }
    this.items = sortSharePointItems(result.value);
    this.currentPath = folderPath;
    this.breadcrumbs = buildWorkspaceBreadcrumbs({
      isUserMode: this.isModeUser,
      userSiteTitle: this.userSiteTitle,
      siteName: this.workspaceConnection.siteName,
      libraryName: this.workspaceConnection.libraryName,
      folderPath,
    });
    if (addToHistory) this.folderNav.push(folderPath);
    if (this.processConfigMode) {
      this.processConfigUiRefresh();
      this.notifyProcessConfigSelectionChange();
    } else {
      this.refreshView();
    }
  }

  async openItem(item: SharePointItemDto): Promise<void> {
    if (item.isFolder) {
      await this.loadChildren(item.path ?? item.name);
      return;
    }
    if (this.filePickMode) {
      this.selectedItem = item;
      this.refreshView();
      return;
    }
    await this.openItemPreview(item);
  }

  private readonly richPreviewMode: Record<string, RichPreviewMode> = {
    pdf: 'pdf', csv: 'csv', excel: 'excel', word: 'docx', powerpoint: 'pptx',
  };

  async openItemPreview(item: SharePointItemDto): Promise<void> {
    if (item.isFolder) { await this.loadChildren(item.path ?? item.name); return; }
    this.selectedItem = item;
    const generation = ++this.viewerGeneration;
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
    this.viewerOpenChange.emit(true);
    const filePath = item.path ?? item.name;
    const unsupported = resolveUnsupportedPreviewMessage(item, this.viewerKind);
    if (unsupported) { this.setViewerUnsupported(unsupported); return; }

    switch (this.viewerKind) {
      case 'video':
      case 'audio': await this.loadViewerStream(filePath, generation, SP_WORKSPACE.viewer.couldNotOpenMedia, true); break;
      case 'image': await this.loadViewerStream(filePath, generation); break;
      case 'text': await this.loadFileAsBlob(filePath, generation); break;
      default: {
        const mode = this.richPreviewMode[this.viewerKind];
        if (mode) { await this.runRichPreview(filePath, mode, generation); break; }
        this.setViewerUnsupported(SP_WORKSPACE.viewer.unsupportedType);
      }
    }
  }

  private setViewerUnsupported(message: string): void {
    this.viewerUnsupportedMsg = message;
    this.viewerMediaError = '';
    this.viewerUrl = null;
    this.viewerRawUrl = '';
    this.viewerLoading = false;
    this.refreshView();
  }

  onMediaError(): void {
    if (this.viewerKind !== 'video' && this.viewerKind !== 'audio') return;
    if (this.viewerUnsupportedMsg) return;
    this.viewerLoading = false;
    this.viewerMediaStatus = '';
    const blocked = this.selectedItem ? resolveUnsupportedPreviewMessage(this.selectedItem, this.viewerKind) : '';
    if (blocked) { this.setViewerUnsupported(blocked); return; }
    this.viewerMediaError = SP_WORKSPACE.viewer.mediaPlayFailed;
    this.refreshView();
  }

  onMediaCanPlay(mediaEl?: HTMLMediaElement): void {
    this.viewerMediaStatus = '';
    this.viewerMediaError = '';
    this.viewerLoading = false;
    if (this.viewerKind === 'video' && mediaEl) {
      void mediaEl.play().catch(() => {});
    }
  }

  private resolveWorkspaceStreamingUrl(filePath: string) {
    return this.isModeUser
      ? this.userApi.resolveStreamingUrl(this.selectedUserLibraryDriveId(), filePath)
      : this.api.resolveStreamingUrl(this.workspaceConnection, filePath);
  }

  private isViewerGenerationStale(generation: number): boolean {
    return generation !== this.viewerGeneration;
  }

  private async loadViewerStream(
    filePath: string,
    generation: number,
    errorFallback: string = SP_WORKSPACE.viewer.couldNotOpenFile,
    clearMediaStatus = false,
  ): Promise<void> {
    this.viewerLoading = true;
    this.viewerMediaError = '';
    if (clearMediaStatus) this.viewerMediaStatus = '';
    this.revokeViewerBlobUrl();
    this.viewerUrl = null;

    const urlResult = await awaitApi(this.resolveWorkspaceStreamingUrl(filePath), errorFallback);
    if (this.isViewerGenerationStale(generation)) return;
    if (!urlResult.ok) {
      this.viewerLoading = false;
      this.viewerMediaError = urlResult.message;
      this.refreshView();
      return;
    }
    this.viewerRawUrl = urlResult.value;
    this.viewerUrl = this.sanitizer.bypassSecurityTrustResourceUrl(urlResult.value);
    this.viewerLoading = false;
    this.refreshView();
  }

  get viewerStreamIsBlobUrl(): boolean {
    return this.viewerRawUrl.startsWith('blob:');
  }

  private revokeViewerBlobUrl(): void {
    if (this.viewerRawUrl.startsWith('blob:')) {
      URL.revokeObjectURL(this.viewerRawUrl);
    }
  }

  private fetchWorkspaceFileBlob(filePath: string): Observable<Blob> {
    return this.isModeUser
      ? this.userApi.fetchFileBlob(this.selectedUserLibraryDriveId(), filePath)
      : this.api.fetchFileBlob(this.workspaceConnection, filePath);
  }

  private async loadFileAsBlob(filePath: string, generation: number): Promise<void> {
    const result = await awaitApi(this.fetchWorkspaceFileBlob(filePath), SP_WORKSPACE.viewer.loadFileFailed);
    if (this.isViewerGenerationStale(generation)) return;
    if (!result.ok) {
      this.viewerLoading = false;
      this.status.setApiError(result.error, SP_WORKSPACE.viewer.loadFileFailed);
      return;
    }
    const url = URL.createObjectURL(result.value);
    this.viewerRawUrl = url;
    this.viewerUrl = this.sanitizer.bypassSecurityTrustResourceUrl(url);
    if (this.viewerKind === 'text') this.viewerText = await result.value.text();
    if (this.isViewerGenerationStale(generation)) {
      URL.revokeObjectURL(url);
      return;
    }
    this.viewerLoading = false;
    this.refreshView();
  }

  private clearRichPreviewDom(): void {
    const el = this.officePreviewHost?.nativeElement;
    if (el) el.innerHTML = '';
  }

  private async runRichPreview(filePath: string, mode: RichPreviewMode, generation: number): Promise<void> {
    this.viewerLoading = true;
    this.viewerMediaError = '';
    this.viewerMediaStatus = '';
    this.refreshView();

    const blobResult = await awaitApi(this.fetchWorkspaceFileBlob(filePath), SP_WORKSPACE.viewer.previewLoadFailed);
    if (this.isViewerGenerationStale(generation)) return;
    if (!blobResult.ok) {
      this.viewerLoading = false;
      this.viewerMediaError = blobResult.message;
      this.refreshView();
      return;
    }

    const host = await waitForDomHost(
      () => this.officePreviewHost?.nativeElement,
      () => this.cdr.detectChanges(),
    );
    if (this.isViewerGenerationStale(generation)) return;
    if (!host) {
      this.viewerMediaError = SP_WORKSPACE.viewer.previewStartFailed;
      this.viewerLoading = false;
      this.refreshView();
      return;
    }
    this.previewSvc.teardown();
    host.innerHTML = '';
    const onPdfFirstPage = () => {
      this.viewerLoading = false;
      this.refreshView();
    };
    try {
      const blob = blobResult.value;
      if (mode === 'pdf') await this.previewSvc.renderPdf(host, blob, onPdfFirstPage);
      else if (mode === 'pptx') await this.previewSvc.renderPptx(host, blob);
      else if (mode === 'docx') await this.previewSvc.renderDocx(host, blob);
      else if (mode === 'excel') await this.previewSvc.renderExcelTable(host, blob);
      else if (mode === 'csv') this.previewSvc.renderCsvTable(host, await blob.text());
    } catch (e) {
      this.viewerMediaError = e instanceof Error ? e.message : SP_WORKSPACE.viewer.previewFailed;
    } finally {
      this.viewerLoading = false;
      this.viewerMediaStatus = '';
      this.refreshView();
    }
  }

  closeViewer(): void {
    this.viewerGeneration++;
    this.revokeViewerBlobUrl();
    this.clearRichPreviewDom();
    this.previewSvc.teardown();
    this.showViewer = false;
    this.viewerOpenChange.emit(false);
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

  async downloadSelected(): Promise<void> {
    if (!this.selectedItem) return;
    const filePath = this.selectedItem.path ?? this.selectedItem.name;
    const result = await awaitApi(this.fetchWorkspaceFileBlob(filePath), SP_WORKSPACE.viewer.downloadFailed);
    if (!result.ok) {
      this.status.setApiError(result.error, SP_WORKSPACE.viewer.downloadFailed);
      return;
    }
    const url = URL.createObjectURL(result.value);
    const a = document.createElement('a');
    a.href = url;
    a.download = this.selectedItem.name;
    a.click();
    URL.revokeObjectURL(url);
  }

  async navigateBreadcrumb(index: number): Promise<void> {
    if (index <= 1) {
      await this.loadChildren('');
      return;
    }
    const folderParts = this.breadcrumbs.slice(2, index + 1);
    await this.loadChildren(folderParts.join('/'));
  }

  async goBack(): Promise<void> {
    if (!this.canNavigateUp) return;
    const parts = this.currentPath.split('/').filter(Boolean);
    parts.pop();
    await this.loadChildren(parts.join('/'));
  }

  get canShowCurlGuide(): boolean {
    if (this.filePickMode || this.isModeUser) return false;
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

    await delay(600);
    const samplePath = curlSampleFilePath(this.items, this.currentPath);
    this.curlModalData = buildCurlCommands({
      apiBaseUrl: this.env.apiBaseUrl,
      apiVersion: this.env.apiVersion,
      applicationId: this.workspaceConnection.applicationId!,
      apiSecret: this.workspaceConnection.consumerSecret!.trim(),
      displayName: this.selectedInternalApp()?.displayName ?? this.workspaceConnection.siteName ?? MODULE_BRANDING.workspaceDefaultName,
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

  readonly connectLoaderSteps = SP_WORKSPACE.connect.connectLoaderSteps;

  readonly folderIconSrc = sharePointFolderIconSrc();

  itemCountLabel(count: number): string {
    return spFolderItemCountLabel(count);
  }

  fileIconSrc(item: SharePointItemDto): string {
    return sharePointItemIconSrc(item);
  }

  fileExtensionLabel(item: SharePointItemDto): string {
    return sharePointItemExtensionLabel(item);
  }

  fileDisplaySize(item: SharePointItemDto): string {
    if (item.isFolder) return spFolderItemCountLabel(item.childCount ?? 0);
    return formatFileSize(item.size);
  }

  formatFileSize = formatFileSize;

  trackByItemId(_index: number, item: SharePointItemDto): string {
    return item.id;
  }

  private async tryAutoBrowse(): Promise<void> {
    if (!this.canBrowse || this.browsing || this.items.length) {
      return;
    }
    if (this.processConfigMode) {
      await this.loadChildrenCore('');
      return;
    }
    await this.loadChildren('');
  }

  private async loadApplicationCatalog(): Promise<void> {
    await this.scheduleAsync(async () => {
      const result = await awaitApi(this.api.loadApplicationCatalog(), SP_APPLICATIONS.api.loadFailed);
      if (!result.ok) {
        this.status.setApiError(result.error, SP_APPLICATIONS.api.loadFailed);
        this.refreshView();
        return;
      }
      this.applicationTypes = result.value.types;
      this.applications = result.value.applications;
      this.rebuildDropdownOptions();
      await this.tryApplyPendingApplication();
      this.refreshView();
    });
  }

  private async tryApplyPendingApplication(): Promise<void> {
    const id = this.pendingApplicationId;
    if (!id) return;
    const app = this.applications.find((a) => a.applicationId === id);
    if (!app) {
      this.status.setApiErrorMessage(SP_WORKSPACE.connect.appNotFound);
      this.pendingApplicationId = null;
      this.launchApplicationConsumed.emit();
      return;
    }
    this.pendingApplicationId = null;
    await this.applyRegisteredApplication(app);
    this.launchApplicationConsumed.emit();
  }

  private async applyRegisteredApplication(app: ApplicationDto): Promise<void> {
    if (this.filePickMode) {
      this.selectedFilePickAppId = app.applicationId;
      this.selectedFilePickAppDropdown = findDropdownOption(this.filePickAppDropdownOptions, this.selectedFilePickAppId);
      this.rebuildFilePickSiteDropdownOptions();
      const sites = registeredApplicationSites(app);
      if (sites.length === 1) {
        this.selectedFilePickSiteIndex = 0;
        this.selectedFilePickSiteDropdown = findDropdownOption(this.filePickSiteDropdownOptions, String(this.selectedFilePickSiteIndex));
        await this.applyFilePickSiteConnection();
      } else {
        this.selectedFilePickSiteIndex = -1;
        this.selectedFilePickSiteDropdown = null;
        this.workspaceConnected = false;
        this.clearBrowseState();
        this.workspaceConnection = emptyWorkspaceConnection();
      }
      return;
    }

    const typeCode = app.applicationTypeCode;
    this.configMode = workspaceModeFromType(typeCode);
    this.selectedRegisteredSiteId = app.applicationId;
    this.selectedRegisteredSiteDropdown = findDropdownOption(this.registeredSiteDropdownOptions, this.selectedRegisteredSiteId);
    this.selectedUserSiteDropdown = findDropdownOption(this.userSiteDropdownOptions, this.selectedRegisteredSiteId);

    if (typeCode === 'tp_user_delegated') {
      this.userSiteInput = app.siteName ?? '';
      this.workspaceConnection = connectionFromApplication(app);
      await this.connectUserSite();
      this.status.setSuccess(SP_WORKSPACE.connect.connectedToApp(app.displayName));
      return;
    }

    if (typeCode === 'tp_internal') {
      this.selectedInternalAppId = app.applicationId;
      await this.onInternalApplicationChange();
      this.status.setSuccess(SP_WORKSPACE.connect.connectedToApp(app.displayName));
      return;
    }

    this.workspaceConnection = connectionFromApplication(app);
    await this.applyExternalConnection();
  }
}

