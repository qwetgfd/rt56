import { Component, OnDestroy, OnInit, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SharePointApiService } from '../services/sharepoint.api.service';
import { ApplicationDto, ApplicationTypeCode, ApplicationTypeDto } from '../core/sharepoint.types';
import { SHAREPOINT_ENV } from '../core/sharepoint.config';
import { SharepointIconComponent } from '../core/sharepoint-icon.component';
import {
  buildCurlCommands,
  curlScriptFilename,
  downloadCurlScript,
  FALLBACK_APPLICATION_TYPES,
  parseApiError,
  StatusAlerts,
} from '../core/sharepoint.utils';

@Component({
  selector: 'app-sharepoint-applications',
  standalone: true,
  imports: [CommonModule, FormsModule, SharepointIconComponent],
  templateUrl: './sharepoint-applications.component.html',
  styleUrls: ['./sharepoint-applications.component.css', '../sharepoint.styles.css'],
})
export class SharepointApplicationsComponent implements OnInit, OnDestroy {
  private readonly env = inject(SHAREPOINT_ENV);
  private readonly api = inject(SharePointApiService);
  readonly status = new StatusAlerts();

  @Output() useApplication = new EventEmitter<ApplicationDto>();
  @Output() navigateHome = new EventEmitter<void>();

  view: 'list' | 'form' = 'list';
  displayMode: 'cards' | 'table' = 'cards';
  searchTerm = '';
  typeFilter: 'all' | ApplicationTypeCode = 'all';
  applicationTypes: ApplicationTypeDto[] = [];
  applications: ApplicationDto[] = [];
  applicationForm: ApplicationFormModel = emptyApplicationForm('custom');
  appSecretVisible = false;
  appConsumerSecretVisible = false;

  ngOnDestroy(): void { this.status.destroy(); }
  ngOnInit(): void { this.loadApplicationTypes(); this.loadApplications(); }

  get isFormValid(): boolean {
    const f = this.applicationForm;
    return !!(f.applicationTypeCode && f.displayName.trim() && f.tenantId.trim() && f.clientId.trim() && f.clientSecret.trim() && f.hostName.trim());
  }

  get filteredApplications(): ApplicationDto[] {
    const q = this.searchTerm.trim().toLowerCase();
    return this.applications.filter((app) => {
      const typeOk = this.typeFilter === 'all' || app.applicationTypeCode === this.typeFilter;
      const searchOk = !q || [app.displayName, app.applicationTypeName, app.hostName, app.siteName, app.libraryName, app.clientId, app.applicationId, app.tenantId]
        .some((v) => (v ?? '').toLowerCase().includes(q));
      return typeOk && searchOk;
    });
  }

  get internalCount(): number { return this.countByType('tp_internal'); }
  get externalCount(): number { return this.countByType('tp_external'); }
  get customCount(): number { return this.countByType('custom'); }

  showList(): void { this.view = 'list'; this.loadApplications(); this.status.clear(); }
  showForm(typeCode: ApplicationTypeCode = 'custom'): void { this.view = 'form'; this.applicationForm = emptyApplicationForm(typeCode); this.status.clear(); }
  setDisplayMode(mode: 'cards' | 'table'): void { this.displayMode = mode; }
  setTypeFilter(filter: 'all' | ApplicationTypeCode): void { this.typeFilter = filter; }

  editApplication(app: ApplicationDto): void {
    this.view = 'form';
    this.applicationForm = {
      applicationId: app.applicationId,
      applicationTypeCode: app.applicationTypeCode as ApplicationTypeCode,
      displayName: app.displayName,
      tenantId: app.tenantId,
      clientId: app.clientId,
      clientSecret: app.clientSecret,
      consumerClientId: app.consumerClientId ?? '',
      consumerSecret: app.consumerSecret ?? '',
      hostName: app.hostName,
      siteName: app.siteName ?? '',
      libraryName: app.libraryName ?? '',
      notes: app.notes ?? '',
    };
    this.status.clear();
  }

  saveApplication(): void {
    if (!this.isFormValid) { this.status.error = 'Please fill in all required fields.'; return; }
    const f = this.applicationForm;
    const isNew = !f.applicationId;
    this.api.saveApplication({
      applicationId: f.applicationId ?? undefined,
      applicationTypeCode: f.applicationTypeCode,
      ownerKey: this.env.ownerKey,
      displayName: f.displayName.trim(),
      tenantId: f.tenantId.trim(),
      clientId: f.clientId.trim(),
      clientSecret: f.clientSecret.trim(),
      hostName: f.hostName.trim(),
      siteName: f.siteName.trim() || undefined,
      libraryName: f.libraryName.trim() || undefined,
      notes: f.notes.trim() || undefined,
    }).subscribe({
      next: (saved) => {
        this.applicationForm = { ...f, applicationId: saved.applicationId, consumerClientId: saved.consumerClientId ?? '', consumerSecret: saved.consumerSecret ?? '' };
        if (isNew) {
          this.downloadCurlForApp(saved.applicationId, saved.displayName, saved.consumerSecret);
          this.useApplication.emit(saved);
        } else {
          this.status.setSuccess(`"${saved.displayName}" updated.`);
          this.loadApplications();
        }
      },
      error: (err) => { this.status.error = parseApiError(err, 'Failed to save.'); },
    });
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

  deleteApplication(app: ApplicationDto): void {
    if (!confirm(`Delete "${app.displayName}"?`)) return;
    this.api.deleteApplication(app.applicationId).subscribe({
      next: () => { this.status.setSuccess(`Deleted "${app.displayName}".`); this.loadApplications(); },
      error: (err) => { this.status.error = parseApiError(err, 'Failed to delete.'); },
    });
  }

  applicationTypeName(code: string): string {
    return this.applicationTypes.find((t) => t.code === code)?.displayName ?? code;
  }

  private loadApplicationTypes(): void {
    this.api.listApplicationTypes().subscribe({
      next: (types) => { this.applicationTypes = types; },
      error: () => { this.applicationTypes = FALLBACK_APPLICATION_TYPES; },
    });
  }

  private loadApplications(): void {
    this.api.listApplications().subscribe({
      next: (apps) => { this.applications = apps; },
      error: (err) => { this.status.error = parseApiError(err, 'Failed to load applications.'); },
    });
  }

  private countByType(type: ApplicationTypeCode): number {
    return this.applications.filter((a) => a.applicationTypeCode === type).length;
  }
}

interface ApplicationFormModel {
  applicationId: string | null;
  applicationTypeCode: ApplicationTypeCode;
  displayName: string;
  tenantId: string;
  clientId: string;
  clientSecret: string;
  consumerClientId: string;
  consumerSecret: string;
  hostName: string;
  siteName: string;
  libraryName: string;
  notes: string;
}

function emptyApplicationForm(typeCode: ApplicationTypeCode): ApplicationFormModel {
  return {
    applicationId: null,
    applicationTypeCode: typeCode,
    displayName: '',
    tenantId: '',
    clientId: '',
    clientSecret: '',
    consumerClientId: '',
    consumerSecret: '',
    hostName: '',
    siteName: '',
    libraryName: '',
    notes: '',
  };
}
