import { ChangeDetectorRef, Component, EventEmitter, NgZone, OnDestroy, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SelectDropDownModule, SelectDropDownService } from 'ngx-select-dropdown';
import { environment } from '../../environments/environment';
import { MODULE_BRANDING, REG_SHOWCASE_DEMO, SP_APPLICATIONS, spFileCountLabel, spLibraryCountLabel, spSiteCountLabel } from '../core/sharepoint.messages';
import { SHAREPOINT_ENV } from '../core/sharepoint.config';
import { SharepointIconComponent, SharepointLogoComponent, SharepointStatusAlertsComponent, SharepointTpLogoComponent, type SpIconName } from '../core/sharepoint.ui';
import { ApplicationCatalog, ApplicationDto, ApplicationTypeCode, ApplicationTypeDto, ExternalSiteConnectivityResultDto, SharePointLibraryDto, SiteConnectivityCheckRequest } from '../core/sharepoint.types';
import { applicationFormFromDto, applicationSiteCount, APPLICATION_TYPE_ID, applicationTypeByCode, applicationTypeById, applicationTypeCodeFromId, applicationTypeDisplayName, awaitApi, buildCurlCommands, curlScriptFilename, delay, downloadCurlScript, emptyApplicationForm, emptyApplicationSite, parseApiError, parseSharePointSiteUrl, partitionRegisteredSites, pickDefaultLibraryName, preferredLibraryNames, registrationTypeOptions as buildRegistrationTypeOptions, StatusAlerts, type ApplicationFormModel, type ApplicationSiteFormModel } from '../core/sharepoint.utils';
import { SharePointApiService } from '../services/sharepoint-api.service';

type ConnectivityTestState = 'idle' | 'testing' | 'success' | 'failed';
type SiteTypeCode = 'tp_internal' | 'tp_external' | 'tp_user_delegated';

interface ConnectivityGlimpse {
  icon: SpIconName;
  label: string;
  value: string;
}

const CONNECTIVITY_TEST_MIN_DISPLAY_MS = 5000;

@Component({
  selector: 'app-sharepoint-applications',
  standalone: true,
  imports: [CommonModule, FormsModule, SelectDropDownModule, SharepointIconComponent, SharepointLogoComponent, SharepointStatusAlertsComponent, SharepointTpLogoComponent],
  templateUrl: './sharepoint-applications.component.html',
  styleUrls: ['./sharepoint-applications.component.css', './sharepoint-reg-video.css', '../sharepoint.styles.css'],
})
export class SharepointApplicationsComponent implements OnInit, OnDestroy {
  private readonly env = inject(SHAREPOINT_ENV);
  private readonly api = inject(SharePointApiService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly ngZone = inject(NgZone);
  private readonly dropdownSvc = inject(SelectDropDownService);
  readonly branding = MODULE_BRANDING;
  readonly m = SP_APPLICATIONS;
  readonly regDemo = REG_SHOWCASE_DEMO;
  readonly status = new StatusAlerts();

  @Output() useApplication = new EventEmitter<ApplicationDto>();
  @Output() navigateHome = new EventEmitter<void>();

  view: 'list' | 'form' = 'list';
  displayMode: 'cards' | 'table' = 'cards';
  searchTerm = '';
  typeFilter: 'all' | SiteTypeCode = 'all';
  applicationTypes: ApplicationTypeDto[] = [];
  applications: ApplicationDto[] = [];
  applicationForm: ApplicationFormModel = emptyApplicationForm();
  activeSiteIndex = 0;
  siteUrlInput = '';
  availableLibraries: SharePointLibraryDto[] = [];
  siteUrlParsed = false;
  siteUrlParsing = false;
  siteDetailsOpen = false;
  private siteUrlDebounce: ReturnType<typeof setTimeout> | null = null;
  private siteUrlParseFinishTimer: ReturnType<typeof setTimeout> | null = null;
  private siteUrlParseStartedAt = 0;
  private readonly siteUrlParseMinMs = 420;
  appSecretVisible = false;
  appConsumerSecretVisible = false;
  checkingConnectivity = false;
  connectivityTestState: ConnectivityTestState = 'idle';
  connectivityInlineResult: ExternalSiteConnectivityResultDto | null = null;
  connectivityInlineError: string | null = null;
  private readonly siteConnectivityByIndex = new Map<number, {
    state: ConnectivityTestState;
    result: ExternalSiteConnectivityResultDto | null;
    error: string | null;
  }>();
  private readonly siteLibrariesByIndex = new Map<number, SharePointLibraryDto[]>();
  advancedSettingsOpen = false;
  connectivityVerifyPhaseIndex = 0;
  connectivityGlimpse: ConnectivityGlimpse | null = null;
  connectivityGlimpseKey = 0;
  connectivityAwaitingGlimpse = false;
  private verifyPhaseTimer: ReturnType<typeof setInterval> | null = null;
  private glimpseTimers: ReturnType<typeof setTimeout>[] = [];

  ngOnDestroy(): void {
    if (this.siteUrlDebounce) clearTimeout(this.siteUrlDebounce);
    if (this.siteUrlParseFinishTimer) clearTimeout(this.siteUrlParseFinishTimer);
    this.stopVerifyPhaseCycle();
    this.clearConnectivityGlimpses();
    this.status.destroy();
  }

  async ngOnInit(): Promise<void> { await this.loadApplicationCatalog(); }

  get isExternalForm(): boolean { return this.applicationForm.applicationTypeId === APPLICATION_TYPE_ID.external; }
  get isInternalForm(): boolean { return this.applicationForm.applicationTypeId === APPLICATION_TYPE_ID.internal; }
  get isUserForm(): boolean { return this.applicationForm.applicationTypeId === APPLICATION_TYPE_ID.userDelegated; }
  get showFullCredentialForm(): boolean { return this.isExternalForm; }
  get needsConnectivityTest(): boolean { return this.isInternalForm || this.isExternalForm || this.isUserForm; }
  get isEditMode(): boolean { return !!this.applicationForm.applicationId; }

  get activeSite(): ApplicationSiteFormModel {
    if (!this.applicationForm.sites.length) {
      return emptyApplicationSite();
    }
    return this.applicationForm.sites[this.activeSiteIndex] ?? this.applicationForm.sites[0];
  }

  siteCountLabel(app: ApplicationDto): string {
    return spSiteCountLabel(applicationSiteCount(app));
  }

  resolveSiteHost(site: ApplicationSiteFormModel): string {
    const fromSite = site.hostName.trim();
    if (fromSite) return fromSite;
    if (this.isInternalForm) return this.env.hostName?.trim() || '';
    return '';
  }

  get isFormValid(): boolean {
    const f = this.applicationForm;
    const ownerOk = !!f.owner.trim();
    const siteOk = f.sites.some((s) => !!s.siteName.trim());
    const displayOk = !!f.displayName.trim();
    const hostOk = f.sites.some((s) => !!this.resolveSiteHost(s));
    if (this.isUserForm || this.isInternalForm) {
      return ownerOk && siteOk && displayOk && hostOk;
    }
    const base = ownerOk && displayOk && f.tenantId.trim() && f.clientId.trim() && f.clientSecret.trim() && hostOk;
    if (!f.applicationId) return base && siteOk;
    return base;
  }

  get formUi() { return { entra: this.isExternalForm, connectivity: this.needsConnectivityTest && !this.isEditMode }; }

  get filteredApplications(): ApplicationDto[] {
    const q = this.searchTerm.trim().toLowerCase();
    return this.applications.filter((app) => {
      const typeOk = this.typeFilter === 'all' || app.applicationTypeCode === this.typeFilter;
      const searchOk = !q || [app.displayName, app.owner, app.coOwner, app.applicationTypeName, app.hostName, app.siteName, app.libraryName, app.clientId, app.applicationId, app.tenantId]
        .some((v) => (v ?? '').toLowerCase().includes(q));
      return typeOk && searchOk;
    });
  }

  get registrationTypeOptions() { return buildRegistrationTypeOptions(this.applicationTypes); }

  get internalTypeLabel(): string {
    return applicationTypeById(this.applicationTypes, APPLICATION_TYPE_ID.internal)?.displayName ?? SP_APPLICATIONS.typeFallback.internal;
  }

  get externalTypeLabel(): string {
    return applicationTypeById(this.applicationTypes, APPLICATION_TYPE_ID.external)?.displayName ?? SP_APPLICATIONS.typeFallback.external;
  }

  get userTypeLabel(): string {
    return applicationTypeById(this.applicationTypes, APPLICATION_TYPE_ID.userDelegated)?.displayName ?? SP_APPLICATIONS.typeFallback.userDelegated;
  }

  get connectivityHint(): string {
    if (this.isInternalForm) return SP_APPLICATIONS.connectivity.hintInternal;
    if (this.isUserForm) return SP_APPLICATIONS.connectivity.hintUser;
    return SP_APPLICATIONS.connectivity.hintExternal;
  }

  get connectivitySuccessDetail(): string {
    const r = this.connectivityInlineResult;
    if (!r) return '';
    const parts = [r.siteTitle, r.siteName, r.hostName].map((s) => s?.trim()).filter((s): s is string => !!s);
    parts.push(`${spLibraryCountLabel(r.libraryCount)}, ${spFileCountLabel(r.fileCount)}`);
    return parts.join(' · ');
  }

  get connectivityAriaLabel(): string {
    const { connectivityTestState: state, connectivityInlineError: err, connectivityInlineResult: result } = this;
    if (state === 'testing') return SP_APPLICATIONS.connectivity.ariaTesting(this.connectivityVerifyPhaseLabel, this.connectivityTestTarget);
    if (state === 'success') return SP_APPLICATIONS.connectivity.ariaSuccess(this.connectivitySuccessDetail);
    if (state === 'failed') return err || result?.message || SP_APPLICATIONS.connectivity.ariaFailed;
    return SP_APPLICATIONS.connectivity.ariaIdle;
  }

  get connectivityVerifyPhaseLabel(): string {
    const phases = this.verifyPhases();
    return phases[this.connectivityVerifyPhaseIndex] ?? phases[0];
  }

  get verifyProgressPct(): number {
    const total = this.verifyPhases().length || 1;
    return ((this.connectivityVerifyPhaseIndex + 1) / total) * 100;
  }

  verifyPhases(): readonly string[] {
    return this.isExternalForm ? SP_APPLICATIONS.connectivity.phasesExternal : SP_APPLICATIONS.connectivity.phasesInternal;
  }

  verifyPhaseShort(index: number): string {
    const shorts = this.isExternalForm ? SP_APPLICATIONS.connectivity.phaseShortExternal : SP_APPLICATIONS.connectivity.phaseShortInternal;
    return shorts[index] ?? `${index + 1}`;
  }

  get connectivityTestTarget(): string {
    const site = this.activeSite;
    const siteName = site.siteName.trim();
    const host = this.resolveSiteHost(site);
    if (host && siteName) return `${host}${SP_APPLICATIONS.connectivity.testTargetSeparator}${siteName}`;
    return siteName || MODULE_BRANDING.siteLabel;
  }

  get canTestConnectivity(): boolean {
    const site = this.activeSite;
    if (!site.siteName.trim() || !this.resolveSiteHost(site)) return false;
    if (this.isInternalForm || this.isUserForm) return true;
    const f = this.applicationForm;
    return !!(f.tenantId.trim() && f.clientId.trim() && f.clientSecret.trim());
  }

  async showList(): Promise<void> {
    this.view = 'list';
    await this.loadApplicationCatalog();
    this.status.clear();
  }

  showForm(): void {
    this.view = 'form';
    this.advancedSettingsOpen = false;
    this.siteDetailsOpen = false;
    this.siteUrlParsed = false;
    this.siteUrlParsing = false;
    this.activeSiteIndex = 0;
    this.applicationForm = emptyApplicationForm(APPLICATION_TYPE_ID.internal, { includeDefaultSite: false });
    this.siteUrlInput = '';
    this.availableLibraries = [];
    this.clearAllSiteConnectivity();
    this.applyDefaultOwner();
    this.resetConnectivityTest();
    this.status.clear();
  }

  setRegistrationType(applicationTypeId: number): void {
    if (this.applicationForm.applicationTypeId === applicationTypeId) return;
    this.applicationForm.applicationTypeId = applicationTypeId;
    this.applicationForm.applicationTypeCode = applicationTypeCodeFromId(this.applicationTypes, applicationTypeId);
    this.availableLibraries = [];
    this.clearAllSiteConnectivity();
    this.applyDefaultOwner();
    if (this.siteUrlInput.trim()) this.applySiteUrl({ silent: true });
  }

  applySiteUrl(options?: { silent?: boolean }): boolean {
    const trimmed = this.siteUrlInput.trim();
    if (!trimmed) {
      this.siteUrlParsed = false;
      return false;
    }
    const parsed = parseSharePointSiteUrl(trimmed);
    if (!parsed) {
      this.siteUrlParsed = false;
      if (!options?.silent) {
        this.status.setApiErrorMessage(SP_APPLICATIONS.validation.urlParse);
      }
      return false;
    }
    this.status.clear();
    if (!this.applicationForm.sites.length) {
      this.applicationForm.sites = [emptyApplicationSite()];
      this.activeSiteIndex = 0;
    }
    const site = this.applicationForm.sites[this.activeSiteIndex];
    if (parsed.hostName) site.hostName = parsed.hostName;
    if (parsed.siteName) site.siteName = parsed.siteName;
    if (parsed.libraryName) site.libraryName = parsed.libraryName;
    if (parsed.folderPath) site.folderPath = parsed.folderPath;
    this.syncFieldsFromActiveSite();
    this.siteUrlParsed = !!parsed.siteName;
    this.syncLibraryFromAvailable();
    this.invalidateActiveSiteConnectivity();
    this.siteUrlInput = '';
    this.siteUrlParsed = false;
    this.cdr.markForCheck();
    return true;
  }

  private beginSiteUrlParse(): void {
    this.siteUrlParsing = true;
    this.siteUrlParseStartedAt = Date.now();
    this.cdr.markForCheck();
  }

  private endSiteUrlParse(): void {
    if (this.siteUrlParseFinishTimer) clearTimeout(this.siteUrlParseFinishTimer);
    const elapsed = Date.now() - this.siteUrlParseStartedAt;
    const remaining = Math.max(0, this.siteUrlParseMinMs - elapsed);
    this.siteUrlParseFinishTimer = setTimeout(() => {
      this.ngZone.run(() => {
        this.siteUrlParsing = false;
        this.siteUrlParseFinishTimer = null;
        this.cdr.markForCheck();
      });
    }, remaining);
  }

  onSiteUrlInput(): void {
    const trimmed = this.siteUrlInput.trim();
    if (!trimmed) {
      this.siteUrlParsing = false;
      this.siteUrlParsed = false;
      if (this.siteUrlDebounce) clearTimeout(this.siteUrlDebounce);
      return;
    }
    this.beginSiteUrlParse();
    if (this.siteUrlDebounce) clearTimeout(this.siteUrlDebounce);
    this.siteUrlDebounce = setTimeout(() => {
      this.ngZone.run(() => {
        this.applySiteUrl({ silent: true });
        this.endSiteUrlParse();
      });
    }, 450);
  }

  onSiteUrlBlur(): void {
    if (this.siteUrlDebounce) {
      clearTimeout(this.siteUrlDebounce);
      this.siteUrlDebounce = null;
    }
    if (!this.siteUrlInput.trim()) return;
    this.beginSiteUrlParse();
    this.applySiteUrl();
    this.endSiteUrlParse();
  }

  onSiteUrlPaste(): void {
    setTimeout(() => {
      this.ngZone.run(() => {
        if (!this.siteUrlInput.trim()) return;
        this.beginSiteUrlParse();
        this.applySiteUrl();
        this.endSiteUrlParse();
      });
    }, 0);
  }

  toggleSiteDetails(): void {
    this.siteDetailsOpen = !this.siteDetailsOpen;
  }

  private syncApplicationTypeFromCatalog(): void {
    const type = applicationTypeById(this.applicationTypes, this.applicationForm.applicationTypeId)
      ?? applicationTypeByCode(this.applicationTypes, this.applicationForm.applicationTypeCode);
    if (!type) return;
    this.applicationForm.applicationTypeId = type.applicationTypeId;
    this.applicationForm.applicationTypeCode = type.code as ApplicationTypeCode;
  }

  private syncFieldsFromActiveSite(): void {
    if (!this.applicationForm.sites.length) return;
    const site = this.applicationForm.sites[this.activeSiteIndex] ?? this.applicationForm.sites[0];
    this.applicationForm.hostName = site.hostName;
    this.applicationForm.siteName = site.siteName;
    this.applicationForm.libraryName = site.libraryName;
    this.applicationForm.folderPath = site.folderPath;
  }

  private syncFieldsToActiveSite(): void {
    if (!this.applicationForm.sites.length) return;
    const site = this.applicationForm.sites[this.activeSiteIndex];
    if (!site) return;
    site.hostName = this.applicationForm.hostName;
    site.siteName = this.applicationForm.siteName;
    site.libraryName = this.applicationForm.libraryName;
    site.folderPath = this.applicationForm.folderPath;
  }

  private saveActiveSiteConnectivity(): void {
    this.siteConnectivityByIndex.set(this.activeSiteIndex, {
      state: this.connectivityTestState,
      result: this.connectivityInlineResult,
      error: this.connectivityInlineError,
    });
    this.siteLibrariesByIndex.set(this.activeSiteIndex, [...this.availableLibraries]);
  }

  private loadActiveSiteConnectivity(): void {
    const cached = this.siteConnectivityByIndex.get(this.activeSiteIndex);
    this.connectivityTestState = cached?.state ?? 'idle';
    this.connectivityInlineResult = cached?.result ?? null;
    this.connectivityInlineError = cached?.error ?? null;
    this.availableLibraries = [...(this.siteLibrariesByIndex.get(this.activeSiteIndex) ?? [])];
  }

  private invalidateActiveSiteConnectivity(): void {
    this.siteConnectivityByIndex.delete(this.activeSiteIndex);
    this.siteLibrariesByIndex.delete(this.activeSiteIndex);
    this.resetConnectivityTest();
  }

  private clearAllSiteConnectivity(): void {
    this.siteConnectivityByIndex.clear();
    this.siteLibrariesByIndex.clear();
    this.resetConnectivityTest();
  }

  private allSitesConnectivityVerified(): boolean {
    return this.applicationForm.sites.every(
      (site, index) => !site.siteName.trim() || this.siteConnectivityByIndex.get(index)?.state === 'success',
    );
  }

  private firstUnverifiedSiteIndex(): number | null {
    for (let i = 0; i < this.applicationForm.sites.length; i++) {
      const site = this.applicationForm.sites[i];
      if (!site.siteName.trim()) continue;
      if (this.siteConnectivityByIndex.get(i)?.state !== 'success') return i;
    }
    return null;
  }

  selectSite(index: number): void {
    if (index < 0 || index >= this.applicationForm.sites.length) return;
    this.saveActiveSiteConnectivity();
    this.syncFieldsToActiveSite();
    this.activeSiteIndex = index;
    this.syncFieldsFromActiveSite();
    this.loadActiveSiteConnectivity();
    this.siteUrlInput = '';
    this.siteUrlParsed = false;
    this.cdr.markForCheck();
  }

  addSite(): void {
    if (this.applicationForm.sites.length) {
      this.saveActiveSiteConnectivity();
      this.syncFieldsToActiveSite();
    }
    this.applicationForm.sites.push(emptyApplicationSite());
    this.activeSiteIndex = this.applicationForm.sites.length - 1;
    this.siteUrlInput = '';
    this.siteUrlParsed = false;
    this.syncFieldsFromActiveSite();
    this.loadActiveSiteConnectivity();
    this.resetConnectivityTest();
    this.cdr.markForCheck();
  }

  removeSite(index: number): void {
    if (this.applicationForm.sites.length <= 1) return;
    this.applicationForm.sites.splice(index, 1);
    this.clearAllSiteConnectivity();
    if (this.activeSiteIndex >= this.applicationForm.sites.length) {
      this.activeSiteIndex = this.applicationForm.sites.length - 1;
    }
    this.syncFieldsFromActiveSite();
    this.siteUrlInput = '';
    this.siteUrlParsed = false;
    this.cdr.markForCheck();
  }

  private buildSiteUrlInput(site: ApplicationSiteFormModel): string {
    const host = this.resolveSiteHost(site);
    const path = site.siteName.trim();
    if (!path) return '';
    if (host) {
      const tail = [path, site.libraryName, site.folderPath].filter((p) => p?.trim()).join('/');
      return `https://${host}/${tail}`;
    }
    return path;
  }

  private syncLibraryFromAvailable(): void {
    if (!this.availableLibraries.length) return;
    this.applicationForm.libraryName = pickDefaultLibraryName(
      this.availableLibraries,
      preferredLibraryNames({
        primary: this.applicationForm.libraryName,
        libraryName: this.env.libraryName,
        defaultLibraryName: this.env.defaultLibraryName,
      }),
    );
  }

  private applyLibrariesFromProbe(libs?: SharePointLibraryDto[]): void {
    this.availableLibraries = libs ?? [];
    this.syncLibraryFromAvailable();
    this.cdr.markForCheck();
  }

  onSiteFieldChange(): void {
    this.syncFieldsToActiveSite();
    this.onConnectivityInputsChange();
  }

  trackBySiteIndex(index: number): number {
    return index;
  }

  isSiteConnectivityVerified(index: number): boolean {
    return this.siteConnectivityByIndex.get(index)?.state === 'success';
  }

  onConnectivityInputsChange(): void {
    if (this.connectivityTestState === 'testing') return;
    if (this.connectivityTestState !== 'idle') this.invalidateActiveSiteConnectivity();
  }

  resetConnectivityTest(): void {
    this.stopVerifyPhaseCycle();
    this.clearConnectivityGlimpses();
    this.connectivityTestState = 'idle';
    this.connectivityInlineResult = null;
    this.connectivityInlineError = null;
    this.connectivityVerifyPhaseIndex = 0;
    this.availableLibraries = [];
  }

  setTypeFilter(filter: 'all' | SiteTypeCode): void { this.typeFilter = filter; }

  toggleAdvancedSettings(): void {
    this.advancedSettingsOpen = !this.advancedSettingsOpen;
  }

  private applyDefaultOwner(): void {
    if (this.applicationForm.owner.trim()) return;
    const name =
      sessionStorage.getItem('userFullName')?.trim() ||
      sessionStorage.getItem('FullName')?.trim() ||
      environment.userFullName?.trim() ||
      '';
    if (name) this.applicationForm.owner = name;
    if (!this.applicationForm.ownerUpn.trim()) {
      const upn =
        sessionStorage.getItem('upn')?.trim() ||
        sessionStorage.getItem('emailID')?.trim() ||
        sessionStorage.getItem('fullUPN')?.trim() ||
        '';
      if (upn) this.applicationForm.ownerUpn = upn;
    }
  }

  async testSiteConnectivity(): Promise<void> {
    const siteLabel = MODULE_BRANDING.siteLabel.toLowerCase();
    if (!this.applicationForm.sites.length) {
      this.status.setApiErrorMessage(SP_APPLICATIONS.connectivity.addSiteBeforeTest(siteLabel));
      return;
    }
    this.syncFieldsToActiveSite();
    const site = this.applicationForm.sites[this.activeSiteIndex];
    const siteName = site.siteName.trim();
    if (!siteName) {
      this.status.setApiErrorMessage(SP_APPLICATIONS.connectivity.enterSiteBeforeTest(siteLabel));
      return;
    }
    if (!this.canTestConnectivity) {
      this.status.setApiErrorMessage(this.connectivityValidationMessage());
      return;
    }
    await this.runSiteConnectivityCheck(siteName, false);
  }

  setDisplayMode(mode: 'cards' | 'table'): void { this.displayMode = mode; }

  editApplication(app: ApplicationDto): void {
    this.view = 'form';
    this.applicationForm = applicationFormFromDto(app);
    this.syncApplicationTypeFromCatalog();
    this.activeSiteIndex = 0;
    this.syncFieldsFromActiveSite();
    this.siteUrlInput = this.buildSiteUrlInput(this.activeSite);
    this.siteUrlParsed = !!this.activeSite.siteName.trim();
    this.resetConnectivityTest();
    this.status.clear();
  }

  async saveApplication(): Promise<void> {
    const f = this.applicationForm;
    if (!this.isFormValid) {
      this.status.setApiErrorMessage(this.formValidationMessage());
      return;
    }
    if (!f.applicationId) {
      if (this.allSitesConnectivityVerified()) {
        await this.persistApplication();
        return;
      }
      const pendingIndex = this.firstUnverifiedSiteIndex();
      if (pendingIndex !== null && pendingIndex !== this.activeSiteIndex) {
        this.selectSite(pendingIndex);
        this.status.setApiErrorMessage(SP_APPLICATIONS.connectivity.testSiteBeforeRegister(this.applicationForm.sites[pendingIndex].siteName.trim()));
        return;
      }
      if (!this.canTestConnectivity) {
        this.status.setApiErrorMessage(this.connectivityValidationMessage());
        return;
      }
      const site = this.applicationForm.sites[this.activeSiteIndex];
      await this.runSiteConnectivityCheck(site.siteName.trim(), true);
      return;
    }
    await this.persistApplication();
  }

  private async persistApplication(): Promise<void> {
    const f = this.applicationForm;
    this.syncFieldsToActiveSite();
    const isNew = !f.applicationId;
    const sites = f.sites
      .filter((s) => s.siteName.trim())
      .map((s, index) => ({
        hostName: this.resolveSiteHost(s),
        siteName: s.siteName.trim(),
        libraryName: s.libraryName.trim() || undefined,
        folderPath: s.folderPath.trim() || undefined,
        sortOrder: index,
      }));
    const primary = sites[0];
    const payload: Partial<ApplicationDto> = {
      applicationId: f.applicationId ?? undefined,
      applicationTypeCode: f.applicationTypeCode,
      ownerKey: this.env.ownerKey,
      owner: f.owner.trim(),
      ownerUpn: f.ownerUpn.trim() || undefined,
      coOwner: f.coOwner.trim() || undefined,
      coOwnerUpn: f.coOwnerUpn.trim() || undefined,
      notes: f.notes.trim() || undefined,
      displayName: f.displayName.trim(),
      siteName: primary?.siteName,
      libraryName: primary?.libraryName,
      hostName: primary?.hostName ?? this.resolveSiteHost(this.activeSite),
      sites,
      ...(this.isExternalForm
        ? {
            tenantId: f.tenantId.trim(),
            clientId: f.clientId.trim(),
            clientSecret: f.clientSecret.trim(),
          }
        : {
            tenantId: '',
            clientId: '',
            clientSecret: '',
          }),
    };

    const result = await awaitApi(this.api.saveApplication(payload), SP_APPLICATIONS.api.saveFailed);
    if (!result.ok) {
      this.status.setApiError(result.error, SP_APPLICATIONS.api.saveFailed);
      return;
    }
    const saved = result.value;
    this.applicationForm = applicationFormFromDto(saved);
    this.syncApplicationTypeFromCatalog();
    this.applicationForm.consumerClientId = saved.consumerClientId ?? this.applicationForm.consumerClientId;
    this.applicationForm.consumerSecret = saved.consumerSecret ?? this.applicationForm.consumerSecret;
    this.activeSiteIndex = 0;
    this.syncFieldsFromActiveSite();
    if (isNew) {
      this.downloadCurlForApp(saved.applicationId, saved.displayName, saved.consumerSecret);
      this.useApplication.emit(saved);
    } else {
      this.status.setSuccess(SP_APPLICATIONS.list.saveSuccess(saved.displayName));
      await this.loadApplicationCatalog();
    }
  }

  private formValidationMessage(): string {
    const v = SP_APPLICATIONS.validation;
    if (!this.applicationForm.owner.trim()) return v.ownerRequired;
    if (!this.applicationForm.displayName.trim()) return v.nameRequired;
    if (!this.applicationForm.sites.some((s) => s.siteName.trim())) return v.siteRequired;
    if (!this.applicationForm.sites.some((s) => this.resolveSiteHost(s))) {
      return this.isInternalForm
        ? v.internalHostMissing
        : v.hostExtractHint(MODULE_BRANDING.hostLabel.toLowerCase());
    }
    if (this.isExternalForm) return v.entraRequired;
    return v.fieldsRequired;
  }

  private connectivityValidationMessage(): string {
    const v = SP_APPLICATIONS.validation;
    const site = this.activeSite;
    if (!site.siteName.trim()) return v.sitePathBeforeTest;
    if (!this.resolveSiteHost(site)) return v.hostUnresolved;
    return v.entraBeforeTest;
  }

  private buildConnectivityRequest(siteName: string): SiteConnectivityCheckRequest {
    const f = this.applicationForm;
    const hostName = this.resolveSiteHost(this.activeSite);
    if (this.isInternalForm || this.isUserForm) {
      return { siteName, hostName };
    }
    return {
      siteName,
      hostName,
      tenantId: f.tenantId.trim(),
      clientId: f.clientId.trim(),
      clientSecret: f.clientSecret.trim(),
    };
  }

  private syncConnectivityView(): void {
    this.cdr.markForCheck();
  }

  private startVerifyPhaseCycle(): void {
    this.stopVerifyPhaseCycle();
    this.connectivityVerifyPhaseIndex = 0;
    this.verifyPhaseTimer = setInterval(() => {
      this.ngZone.run(() => {
        this.connectivityVerifyPhaseIndex = (this.connectivityVerifyPhaseIndex + 1) % this.verifyPhases().length;
        this.syncConnectivityView();
      });
    }, 1650);
  }

  private stopVerifyPhaseCycle(): void {
    if (this.verifyPhaseTimer !== null) {
      clearInterval(this.verifyPhaseTimer);
      this.verifyPhaseTimer = null;
    }
  }

  private clearConnectivityGlimpses(): void {
    for (const timer of this.glimpseTimers) clearTimeout(timer);
    this.glimpseTimers = [];
    this.connectivityGlimpse = null;
    this.connectivityAwaitingGlimpse = false;
  }

  private queueGlimpse(glimpse: ConnectivityGlimpse, showAtMs: number, visibleMs: number): void {
    const showTimer = setTimeout(() => {
      this.ngZone.run(() => {
        this.connectivityGlimpseKey += 1;
        this.connectivityGlimpse = glimpse;
        this.connectivityAwaitingGlimpse = false;
        this.syncConnectivityView();
      });
    }, showAtMs);
    const hideTimer = setTimeout(() => {
      this.ngZone.run(() => {
        if (this.connectivityGlimpse === glimpse) this.connectivityGlimpse = null;
        this.syncConnectivityView();
      });
    }, showAtMs + visibleMs);
    this.glimpseTimers.push(showTimer, hideTimer);
  }

  private scheduleConnectivityGlimpses(data: ExternalSiteConnectivityResultDto): void {
    const glimpses: ConnectivityGlimpse[] = [];
    const host = data.hostName?.trim() || this.applicationForm.hostName.trim();
    const site = data.siteTitle?.trim() || data.siteName?.trim() || this.applicationForm.siteName.trim();
    const g = SP_APPLICATIONS.connectivity;
    if (host) glimpses.push({ icon: 'building', label: g.glimpseHost, value: host });
    if (site) glimpses.push({ icon: 'cloud', label: g.glimpseSite, value: site });
    if (data.isConnected) {
      glimpses.push({ icon: 'folder', label: g.glimpseLibraries, value: spLibraryCountLabel(data.libraryCount) });
      glimpses.push({ icon: 'file', label: g.glimpseFiles, value: spFileCountLabel(data.fileCount) });
    }
    if (!glimpses.length && data.message?.trim()) {
      glimpses.push({ icon: 'info', label: g.glimpseResponse, value: data.message.trim() });
    }
    if (!glimpses.length) return;

    const visibleMs = 1150;
    const gapMs = 180;
    const startMs = 320;
    glimpses.forEach((glimpse, index) => {
      this.queueGlimpse(glimpse, startMs + index * (visibleMs + gapMs), visibleMs);
    });
  }

  private async runSiteConnectivityCheck(siteName: string, proceedToSave: boolean): Promise<void> {
    this.syncFieldsToActiveSite();
    this.checkingConnectivity = true;
    this.connectivityTestState = 'testing';
    this.connectivityInlineResult = null;
    this.connectivityInlineError = null;
    this.status.clear();
    this.clearConnectivityGlimpses();
    this.connectivityAwaitingGlimpse = true;
    this.startVerifyPhaseCycle();
    this.syncConnectivityView();
    const startedAt = Date.now();
    try {
      const result = await awaitApi(
        this.api.validateSiteConnectivity(this.buildConnectivityRequest(siteName)),
        SP_APPLICATIONS.connectivity.couldNotVerify,
      );
      if (result.ok) {
        this.scheduleConnectivityGlimpses(result.value);
      } else {
        const host = this.applicationForm.hostName.trim();
        if (host) {
          this.queueGlimpse(
            { icon: 'alert', label: SP_APPLICATIONS.connectivity.glimpseCheckFailed, value: result.message.slice(0, 48) },
            400,
            1400,
          );
        }
      }
      const remainingMs = CONNECTIVITY_TEST_MIN_DISPLAY_MS - (Date.now() - startedAt);
      if (remainingMs > 0) await delay(remainingMs);
      if (!result.ok) {
        this.connectivityTestState = 'failed';
        this.connectivityInlineError = result.message;
        this.saveActiveSiteConnectivity();
        if (proceedToSave) this.status.setApiErrorMessage(result.message);
        return;
      }
      const connectivity = result.value;
      if (connectivity?.isConnected) {
        this.connectivityTestState = 'success';
        this.connectivityInlineResult = connectivity;
        this.applyLibrariesFromProbe(connectivity.libraries);
        this.saveActiveSiteConnectivity();
        if (proceedToSave) {
          if (this.allSitesConnectivityVerified()) {
            await this.persistApplication();
          } else {
            this.status.setApiErrorMessage(SP_APPLICATIONS.connectivity.testEachSiteBeforeRegister);
          }
        }
        return;
      }
      this.connectivityTestState = 'failed';
      this.connectivityInlineResult = connectivity ?? null;
      this.saveActiveSiteConnectivity();
      if (proceedToSave) {
        this.status.setApiErrorMessage(connectivity?.message || SP_APPLICATIONS.connectivity.verifyFailedDetail);
      }
    } catch (error) {
      this.connectivityTestState = 'failed';
      this.connectivityInlineError = parseApiError(error, SP_APPLICATIONS.connectivity.couldNotVerify);
      this.saveActiveSiteConnectivity();
      if (proceedToSave) this.status.setApiErrorMessage(this.connectivityInlineError);
    } finally {
      this.checkingConnectivity = false;
      this.stopVerifyPhaseCycle();
      this.clearConnectivityGlimpses();
      this.syncConnectivityView();
    }
  }

  downloadCurlForApp(
    applicationId = this.applicationForm.applicationId,
    displayName = this.applicationForm.displayName,
    apiSecret = this.applicationForm.consumerSecret,
  ): void {
    if (!applicationId || !apiSecret?.trim()) return;
    const result = buildCurlCommands({
      apiBaseUrl: this.env.apiBaseUrl,
      apiVersion: this.env.apiVersion,
      applicationId,
      apiSecret: apiSecret.trim(),
      displayName: displayName.trim(),
    });
    downloadCurlScript(result.fullText, curlScriptFilename(displayName));
  }

  async deleteApplication(app: ApplicationDto): Promise<void> {
    if (!confirm(SP_APPLICATIONS.list.deleteConfirm(app.displayName))) return;
    const result = await awaitApi(this.api.deleteApplication(app.applicationId), SP_APPLICATIONS.api.deleteFailed);
    if (!result.ok) {
      this.status.setApiError(result.error, SP_APPLICATIONS.api.deleteFailed);
      return;
    }
    this.status.setSuccess(SP_APPLICATIONS.list.deleted(app.displayName));
    await this.loadApplicationCatalog();
  }

  applicationTypeName(code: string): string { return applicationTypeDisplayName(this.applicationTypes, code); }
  trackByApplicationId(_index: number, app: ApplicationDto): string { return app.applicationId; }
  countByType(type: SiteTypeCode): number {
    return this.applications.filter((a) => a.applicationTypeCode === type).length;
  }

  private applyApplicationCatalog(catalog: ApplicationCatalog): void {
    this.applicationTypes = catalog.types;
    const partitioned = partitionRegisteredSites(catalog.applications);
    this.applications = partitioned.applications;
  }

  private async loadApplicationCatalog(): Promise<void> {
    const result = await awaitApi(this.api.loadApplicationCatalog(), SP_APPLICATIONS.api.loadFailed);
    if (!result.ok) {
      this.status.setApiError(result.error, SP_APPLICATIONS.api.loadFailed);
      return;
    }
    this.applyApplicationCatalog(result.value);
  }
}
