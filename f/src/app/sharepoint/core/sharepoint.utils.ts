import { NgxDropdownConfig } from 'ngx-select-dropdown';
import { firstValueFrom, type Observable, type Subscription } from 'rxjs';
import { MODULE_BRANDING, SP_APPLICATIONS, SP_CURL_BUILD, SP_DROPDOWN, SP_PREVIEW, SP_WORKSPACE } from './sharepoint.messages';
import type { SharepointUserConfig } from './sharepoint.config';
import type { ApiEnvelope, ApplicationDto, ApplicationTypeCode, ApplicationTypeDto, ExternalSiteConnectivityResultDto, FileKind, MeDrivesDiscoveryReportDto, SharePointItemDto, SharePointLibraryDto, UserConnectSiteRequest, WorkspaceConnection } from './sharepoint.types';
import { getMaterialFileIcon } from '@baybreezy/file-extension-icon';

const REGISTERED_APP_CODES = new Set<string>(['tp_internal', 'tp_external', 'tp_user_delegated']);

export type ApiResult<T> = { ok: true; value: T; message?: string; error?: unknown } | { ok: false; message: string; error: unknown };

export function parseApiError(err: unknown, fallback: string): string {
  const e = err as { error?: { message?: string }; message?: string };
  return e?.error?.message || e?.message || fallback;
}

export function encodePath64(filePath: string): string {
  const utf8 = unescape(encodeURIComponent(filePath));
  let bin = '';
  for (let i = 0; i < utf8.length; i++) {
    bin += String.fromCharCode(utf8.charCodeAt(i) & 0xff);
  }
  return btoa(bin).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

export function unwrapApiEnvelope<T>(res: ApiEnvelope<T> | null | undefined, fallback: string): T {
  if (!res?.success) throw new Error(res?.message ?? fallback);
  return res.data;
}

export function unwrapSiteConnectivityEnvelope(
  res: ApiEnvelope<ExternalSiteConnectivityResultDto> | null | undefined,
  fallback: string,
): ExternalSiteConnectivityResultDto {
  if (res?.success && res.data) return res.data;
  if (res?.data) return res.data;
  throw new Error(res?.message ?? fallback);
}

const TYPE_LABELS = SP_APPLICATIONS.typeFallback;

export const FALLBACK_APPLICATION_TYPES: ApplicationTypeDto[] = [
  { applicationTypeId: 3, code: 'tp_internal', displayName: TYPE_LABELS.internal },
  { applicationTypeId: 2, code: 'tp_external', displayName: TYPE_LABELS.external },
  { applicationTypeId: 4, code: 'tp_user_delegated', displayName: TYPE_LABELS.userDelegated },
];

export const APPLICATION_TYPE_ID = {
  external: 2,
  internal: 3,
  userDelegated: 4,
} as const;

export type RegistrationApplicationTypeId =
  (typeof APPLICATION_TYPE_ID)[keyof typeof APPLICATION_TYPE_ID];

export const REGISTRATION_TYPE_IDS: RegistrationApplicationTypeId[] = [
  APPLICATION_TYPE_ID.internal,
  APPLICATION_TYPE_ID.external,
  APPLICATION_TYPE_ID.userDelegated,
];

export function workspaceModeFromType(typeCode: string): ApplicationTypeCode {
  if (typeCode === 'tp_internal') return 'tp_internal';
  if (typeCode === 'tp_user_delegated') return 'user_delegated';
  return 'tp_external';
}

export function normalizeRegisteredApplicationTypeCode(code: string): 'tp_internal' | 'tp_external' {
  return code === 'tp_internal' ? 'tp_internal' : 'tp_external';
}

export function applicationTypeByCode(types: ApplicationTypeDto[], code: string): ApplicationTypeDto | undefined {
  const catalog = types.length ? types : FALLBACK_APPLICATION_TYPES;
  return catalog.find((t) => t.code === code);
}

export function applicationTypeById(types: ApplicationTypeDto[], applicationTypeId: number): ApplicationTypeDto | undefined {
  const catalog = types.length ? types : FALLBACK_APPLICATION_TYPES;
  return catalog.find((t) => t.applicationTypeId === applicationTypeId);
}

export function registrationTypeOptions(types: ApplicationTypeDto[]): ApplicationTypeDto[] {
  return REGISTRATION_TYPE_IDS.map(
    (id) => applicationTypeById(types, id) ?? FALLBACK_APPLICATION_TYPES.find((t) => t.applicationTypeId === id)!,
  );
}

export function applicationTypeCodeFromId(types: ApplicationTypeDto[], applicationTypeId: number): ApplicationTypeCode {
  const type = applicationTypeById(types, applicationTypeId);
  return (type?.code ?? 'tp_external') as ApplicationTypeCode;
}

export function normalizeWorkspaceConfigMode(mode: string | null | undefined): ApplicationTypeCode {
  if (mode === 'tp_internal' || mode === 'tp_external' || mode === 'user_delegated') return mode;
  return 'tp_external';
}

function isRegisteredAppCode(code: string): boolean {
  return REGISTERED_APP_CODES.has(code);
}

export function filterRegisterableApplicationTypes(types: ApplicationTypeDto[]): ApplicationTypeDto[] {
  return types.filter((t) => isRegisteredAppCode(t.code));
}

export function filterRegisteredApplications(apps: ApplicationDto[]): ApplicationDto[] {
  return apps.filter((a) => isRegisteredAppCode(a.applicationTypeCode));
}

export function partitionRegisteredSites(apps: ApplicationDto[]): {
  applications: ApplicationDto[];
  internalSites: ApplicationDto[];
  userDelegatedSites: ApplicationDto[];
} {
  const applications = filterRegisteredApplications(apps);
  return {
    applications,
    internalSites: applications.filter((a) => a.applicationTypeCode === 'tp_internal'),
    userDelegatedSites: applications.filter((a) => a.applicationTypeCode === 'tp_user_delegated'),
  };
}

export function partitionInternalApplications(apps: ApplicationDto[]): {
  applications: ApplicationDto[];
  internalApplications: ApplicationDto[];
  hasInternalApplication: boolean;
} {
  const { applications, internalSites } = partitionRegisteredSites(apps);
  return { applications, internalApplications: internalSites, hasInternalApplication: internalSites.length > 0 };
}

export async function awaitApi<T>(source: Observable<T>, fallback: string): Promise<ApiResult<T>> {
  try {
    return { ok: true, value: await firstValueFrom(source) };
  } catch (error) {
    return { ok: false, message: parseApiError(error, fallback), error };
  }
}

export function fireAndForget<T>(source: Observable<T>, onError?: (err: unknown) => void): Subscription {
  return source.subscribe({ error: (err) => onError?.(err) });
}

export function emptyWorkspaceConnection(): WorkspaceConnection {
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

export function applicationTypeDisplayName(types: ApplicationTypeDto[], code: string): string {
  return types.find((t) => t.code === code)?.displayName ?? code;
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1073741824) return `${(bytes / 1048576).toFixed(1)} MB`;
  return `${(bytes / 1073741824).toFixed(1)} GB`;
}

export function connectionFromApplication(app: ApplicationDto): WorkspaceConnection {
  const sites = registeredApplicationSites(app);
  const primary = sites[0];
  if (primary) return connectionFromApplicationSite(app, primary);
  return {
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
}

export function registeredApplicationSites(app: ApplicationDto): ApplicationSiteFormModel[] {
  return applicationSitesFromDto(app).filter((site) => site.siteName.trim());
}

export function connectionFromApplicationSite(app: ApplicationDto, site: ApplicationSiteFormModel): WorkspaceConnection {
  return {
    applicationId: app.applicationId,
    tenantId: app.tenantId,
    clientId: app.clientId,
    clientSecret: app.clientSecret,
    consumerClientId: app.consumerClientId ?? '',
    consumerSecret: app.consumerSecret ?? '',
    hostName: site.hostName.trim() || app.hostName,
    siteName: site.siteName.trim(),
    libraryName: site.libraryName.trim() || app.libraryName || '',
  };
}

export interface ApplicationSiteFormModel {
  applicationSiteId: string | null;
  hostName: string;
  siteName: string;
  libraryName: string;
  folderPath: string;
}

export function emptyApplicationSite(): ApplicationSiteFormModel {
  return {
    applicationSiteId: null,
    hostName: '',
    siteName: '',
    libraryName: '',
    folderPath: '',
  };
}

export function applicationSitesFromDto(app: ApplicationDto): ApplicationSiteFormModel[] {
  if (app.sites?.length) {
    return app.sites.map((s) => ({
      applicationSiteId: s.applicationSiteId ?? null,
      hostName: s.hostName ?? app.hostName ?? '',
      siteName: s.siteName ?? '',
      libraryName: s.libraryName ?? '',
      folderPath: s.folderPath ?? '',
    }));
  }
  if (app.siteName?.trim()) {
    return [{
      applicationSiteId: null,
      hostName: app.hostName ?? '',
      siteName: app.siteName,
      libraryName: app.libraryName ?? '',
      folderPath: '',
    }];
  }
  return [emptyApplicationSite()];
}

export interface ApplicationFormModel {
  applicationId: string | null;
  applicationTypeId: number;
  applicationTypeCode: ApplicationTypeCode;
  sourceInternalApplicationId: string | null;
  displayName: string;
  tenantId: string;
  clientId: string;
  clientSecret: string;
  consumerClientId: string;
  consumerSecret: string;
  hostName: string;
  siteName: string;
  libraryName: string;
  owner: string;
  ownerUpn: string;
  coOwner: string;
  coOwnerUpn: string;
  notes: string;
  folderPath: string;
  sites: ApplicationSiteFormModel[];
}

export function emptyApplicationForm(
  applicationTypeId: number = APPLICATION_TYPE_ID.external,
  options?: { includeDefaultSite?: boolean },
): ApplicationFormModel {
  const type = applicationTypeById(FALLBACK_APPLICATION_TYPES, applicationTypeId)
    ?? FALLBACK_APPLICATION_TYPES.find((t) => t.applicationTypeId === APPLICATION_TYPE_ID.external)!;
  const includeSite = options?.includeDefaultSite ?? true;
  return {
    applicationId: null,
    applicationTypeId: type.applicationTypeId,
    applicationTypeCode: type.code as ApplicationTypeCode,
    sourceInternalApplicationId: null,
    displayName: '',
    tenantId: '',
    clientId: '',
    clientSecret: '',
    consumerClientId: '',
    consumerSecret: '',
    hostName: '',
    siteName: '',
    libraryName: '',
    owner: '',
    ownerUpn: '',
    coOwner: '',
    coOwnerUpn: '',
    notes: '',
    folderPath: '',
    sites: includeSite ? [emptyApplicationSite()] : [],
  };
}

export function applicationFormFromDto(app: ApplicationDto): ApplicationFormModel {
  const sites = applicationSitesFromDto(app);
  const primary = sites[0] ?? emptyApplicationSite();
  const type = app.applicationTypeId
    ? applicationTypeById(FALLBACK_APPLICATION_TYPES, app.applicationTypeId)
    : applicationTypeByCode(FALLBACK_APPLICATION_TYPES, app.applicationTypeCode);
  return {
    applicationId: app.applicationId,
    applicationTypeId: type?.applicationTypeId ?? app.applicationTypeId ?? 0,
    applicationTypeCode: (type?.code ?? app.applicationTypeCode) as ApplicationTypeCode,
    sourceInternalApplicationId: null,
    displayName: app.displayName,
    tenantId: app.tenantId,
    clientId: app.clientId,
    clientSecret: app.clientSecret,
    consumerClientId: app.consumerClientId ?? '',
    consumerSecret: app.consumerSecret ?? '',
    hostName: primary.hostName,
    siteName: primary.siteName,
    libraryName: primary.libraryName,
    owner: app.owner ?? '',
    ownerUpn: app.ownerUpn ?? '',
    coOwner: app.coOwner ?? '',
    coOwnerUpn: app.coOwnerUpn ?? '',
    notes: app.notes ?? '',
    folderPath: primary.folderPath,
    sites,
  };
}

export function applicationSiteCount(app: ApplicationDto): number {
  if (app.sites?.length) return app.sites.length;
  return app.siteName?.trim() ? 1 : 0;
}

const NON_STREAMABLE_VIDEO = /\.(avi|wmv|mkv|flv|mpeg|mpg|mpe|m1v|m2v|vob|divx|xvid|3gp|3g2|f4v|asf|mts|m2ts|ts|hevc|h265|h264|rec)$/i;
const NON_STREAMABLE_AUDIO = /\.(wma|amr|mid|midi|caf|aiff|aif)$/i;

function fileMatchesExtension(
  item: { name: string; path?: string },
  pattern: RegExp,
): boolean {
  const pathKey = (item.path ?? item.name).toLowerCase();
  const fileName = item.name.toLowerCase();
  return pattern.test(pathKey) || pattern.test(fileName);
}

export const FILE_KIND_PATTERNS: Array<{ kind: FileKind; mime?: RegExp; extension: RegExp }> = [
  { kind: 'video', mime: /^video\//, extension: /\.(mp4|m4v|m4a|webm|mov|qt|avi|wmv|asf|mkv|flv|f4v|3gp|3g2|mpg|mpeg|mpe|m1v|m2v|ogv|ogm|ts|m2ts|mts|vob|divx|xvid|hevc|h265|h264|rec)$/i },
  { kind: 'audio', mime: /^audio\//, extension: /\.(mp3|wav|ogg|oga|flac|aac|m4a|wma|opus|weba|aiff|aif|amr|caf|mid|midi)$/i },
  { kind: 'pdf', mime: /pdf/, extension: /\.pdf$/i },
  { kind: 'word', mime: /word|opendocument\.text/, extension: /\.(doc|docx|odt|rtf)$/i },
  { kind: 'csv', mime: /^text\/csv/i, extension: /\.csv$/i },
  { kind: 'excel', mime: /excel|sheet/, extension: /\.(xls|xlsx|ods)$/i },
  { kind: 'powerpoint', mime: /powerpoint|presentation/, extension: /\.(ppt|pptx|odp)$/i },
  { kind: 'image', mime: /^image\//, extension: /\.(png|jpg|jpeg|gif|bmp|webp|svg|ico|tiff)$/i },
  { kind: 'text', mime: /^text\//, extension: /\.(txt|md|json|xml|log|cfg|ini|html|htm|css|js|ts|sh|bat|py|cs|java|sql|yaml|yml)$/i },
];

export function detectFileKind(item: { isFolder: boolean; path?: string; name: string; mimeType?: string }): FileKind {
  if (item.isFolder) return 'folder';
  const pathKey = (item.path ?? item.name).toLowerCase();
  const fileName = item.name.toLowerCase();
  const mime = (item.mimeType ?? '').toLowerCase();
  if (fileMatchesExtension(item, NON_STREAMABLE_VIDEO)) return 'unknown';
  if (fileMatchesExtension(item, NON_STREAMABLE_AUDIO)) return 'unknown';
  if (fileMatchesExtension(item, /\.djvu?$/)) return 'unknown';
  for (const p of FILE_KIND_PATTERNS) {
    if (p.mime?.test(mime) || p.extension.test(pathKey) || p.extension.test(fileName)) return p.kind;
  }
  return 'unknown';
}

export function resolveUnsupportedPreviewMessage(
  item: { name: string; path?: string },
  kind: FileKind,
): string | null {
  const p = SP_PREVIEW;
  if (fileMatchesExtension(item, /\.djvu?$/)) return p.djVu;
  if (fileMatchesExtension(item, NON_STREAMABLE_VIDEO)) return p.videoFormat;
  if (fileMatchesExtension(item, NON_STREAMABLE_AUDIO)) return p.audioFormat;
  if (kind === 'word' && !fileMatchesExtension(item, /\.docx$/)) return p.legacyWord;
  if (kind === 'powerpoint' && !fileMatchesExtension(item, /\.pptx$/)) return p.legacyPowerPoint;
  if (kind === 'excel' && fileMatchesExtension(item, /\.(xls|ods)$/)) return p.legacyExcel;
  return null;
}

export function sortSharePointItems<T extends { isFolder: boolean; name: string }>(items: T[]): T[] {
  return [...items].sort((a, b) => {
    if (a.isFolder !== b.isFolder) return a.isFolder ? -1 : 1;
    return a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });
  });
}

export class StatusAlerts {
  error = '';
  success = '';
  private timer: ReturnType<typeof setTimeout> | null = null;

  destroy(): void {
    this.cancelTimer();
  }

  dismissSuccess(): void {
    this.cancelTimer();
    this.success = '';
  }

  dismissError(): void {
    this.error = '';
  }

  clearErrors(): void {
    this.error = '';
  }

  setSuccess(message: string, dismissMs = 5000): void {
    this.cancelTimer();
    this.error = '';
    this.success = message;
    this.timer = setTimeout(() => this.dismissSuccess(), dismissMs);
  }

  clear(): void {
    this.error = '';
    this.dismissSuccess();
  }

  setApiError(err: unknown, fallback: string): void {
    this.error = parseApiError(err, fallback);
  }

  setApiErrorMessage(message: string): void {
    this.error = message;
  }

  private cancelTimer(): void {
    if (this.timer != null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }
}

export interface CurlModalInput {
  apiBaseUrl: string;
  apiVersion?: string;
  applicationId: string;
  apiSecret: string;
  displayName?: string;
  sampleFolderPath?: string;
  sampleFilePath?: string;
}

export interface CurlStep {
  number: number;
  title: string;
  description?: string;
  command: string;
  optional?: boolean;
}

export interface CurlModalResult {
  baseUrl: string;
  displayName: string;
  apiVersion: string;
  fullText: string;
  steps: CurlStep[];
}

export function buildCurlCommands(input: CurlModalInput): CurlModalResult {
  const base = input.apiBaseUrl.replace(/\/$/, '');
  const apiVersion = input.apiVersion?.trim() || '1.0';
  const name = input.displayName?.trim() || MODULE_BRANDING.applicationDefaultName;
  const versionHeader = `-H "x-tpdi-api-version: ${apiVersion}"`;
  const authHeader = `-H "Authorization: Bearer YOUR_ACCESS_TOKEN"`;
  const filePath = input.sampleFilePath ?? SP_CURL_BUILD.sampleFilePath;
  const step1 = `curl -X POST "${base}/auth/token" \\\n  ${versionHeader} \\\n  -H "x-application-id: ${input.applicationId}" \\\n  -H "x-client-secret: ${input.apiSecret}"`;
  const step2 = `curl -X GET "${base}/workspace/browse?path=${encodeURIComponent(input.sampleFolderPath ?? '')}" \\\n  ${versionHeader} \\\n  ${authHeader}`;
  const step3 = `curl -X POST "${base}/workspace/file" \\\n  ${versionHeader} \\\n  ${authHeader} \\\n  -H "Content-Type: application/json" \\\n  -d '${JSON.stringify({ filePath })}' \\\n  --output downloaded-file`;
  const steps: CurlStep[] = SP_CURL_BUILD.steps.map((step, index) => ({
    number: step.number,
    title: step.title,
    description: step.description,
    command: [step1, step2, step3][index],
    ...(step.optional ? { optional: true } : {}),
  }));
  return {
    baseUrl: base,
    displayName: name,
    apiVersion,
    fullText: `# ${name} — Streaming API\n# Base URL: ${base}\n# Header: x-tpdi-api-version: ${apiVersion}\n#\n# Step 1\n${step1}\n\n# Step 2\n${step2}\n\n# Step 3\n${step3}`,
    steps,
  };
}

export function highlightCurl(command: string): string {
  const esc = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  return esc(command)
    .split('\n')
    .map((line) => {
      if (line.trimStart().startsWith('#')) return `<span class="tok-comment">${line}</span>`;
      let out = line;
      out = out.replace(/\bcurl\b/g, '<span class="tok-cmd">curl</span>');
      out = out.replace(/(-X)\s+(\w+)/g, '<span class="tok-flag">$1</span> <span class="tok-method">$2</span>');
      out = out.replace(/(--output)\s+(\S+)/g, '<span class="tok-flag">$1</span> <span class="tok-arg">$2</span>');
      out = out.replace(/(-H)\s+"([^"]*)"/g, '<span class="tok-flag">$1</span> <span class="tok-header">"$2"</span>');
      out = out.replace(/(-d)\s+'([^']*)'/g, '<span class="tok-flag">$1</span> <span class="tok-json">\'$2\'</span>');
      out = out.replace(/"https?:\/\/[^"]+"/g, (m) => `<span class="tok-url">${m}</span>`);
      return out;
    })
    .join('\n');
}

export function curlScriptFilename(displayName: string): string {
  const slug = displayName.trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || SP_CURL_BUILD.scriptSlugFallback;
  return `${slug}-streaming-api.txt`;
}

export function downloadCurlScript(content: string, filename: string): void {
  const url = URL.createObjectURL(new Blob([content], { type: 'text/plain;charset=utf-8' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

export async function copyCurlText(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    return false;
  }
}

export function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export const SP_APP_DROPDOWN_CONFIG: NgxDropdownConfig = {
  displayKey: 'label',
  search: true,
  height: '220px',
  placeholder: SP_DROPDOWN.application.placeholder,
  searchPlaceholder: SP_DROPDOWN.application.searchPlaceholder,
  noResultsFound: SP_DROPDOWN.application.noResultsFound,
  clearOnSelection: false,
  limitTo: 0,
};

export const SP_SITE_DROPDOWN_CONFIG: NgxDropdownConfig = {
  displayKey: 'label',
  search: true,
  height: '160px',
  placeholder: SP_DROPDOWN.site.placeholder,
  searchPlaceholder: SP_DROPDOWN.site.searchPlaceholder,
  noResultsFound: SP_DROPDOWN.site.noResultsFound,
  searchOnKey: 'label',
  clearOnSelection: false,
  limitTo: 0,
};

export const SP_FILEPICK_SIDEBAR_DROPDOWN_CONFIG: NgxDropdownConfig = {
  ...SP_APP_DROPDOWN_CONFIG,
  height: '140px',
};

export const SP_LIBRARY_DROPDOWN_CONFIG: NgxDropdownConfig = {
  displayKey: 'label',
  search: true,
  height: '200px',
  placeholder: SP_DROPDOWN.library.placeholder,
  searchPlaceholder: SP_DROPDOWN.library.searchPlaceholder,
  noResultsFound: SP_DROPDOWN.library.noResultsFound,
  searchOnKey: 'label',
  clearOnSelection: false,
  limitTo: 0,
};

export interface SpDropdownOption {
  id: string;
  label: string;
  searchText?: string;
}

export function unwrapNgxDropdownSelection(model: unknown): SpDropdownOption | null {
  if (model == null) return null;
  if (typeof model === 'object' && 'value' in model) {
    return unwrapNgxDropdownSelection((model as { value: unknown }).value);
  }
  if (Array.isArray(model)) {
    const first = model[0];
    return first && typeof first === 'object' && 'id' in first ? (first as SpDropdownOption) : null;
  }
  if (typeof model === 'object' && 'id' in model && 'label' in model) {
    return model as SpDropdownOption;
  }
  return null;
}

export function decodeJwtPayload(token: string): Record<string, unknown> | null {
  try {
    const part = token.split('.')[1];
    if (!part) return null;
    const json = atob(part.replace(/-/g, '+').replace(/_/g, '/'));
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return null;
  }
}

export function logMeDrivesDiagnostics(label: string, report: unknown): void {
  console.group(`[SharePoint User] ${label}`);
  console.log(report);
  const r = report as MeDrivesDiscoveryReportDto | null | undefined;
  if (!r?.drives) {
    console.groupEnd();
    return;
  }
  console.log(`totalDriveCount=${r.totalDriveCount} strictMatch=${r.strictMatchCount} slugOnly=${r.siteSlugOnlyMatchCount}`);
  console.table(
    r.drives.map((d) => ({
      index: d.index,
      name: d.name,
      driveType: d.driveType,
      webUrl: d.webUrl,
      strict: d.strictSiteAndHostMatch,
      slug: d.siteSlugPathMatch,
      result: d.matchResult,
    })),
  );
  if (r.rawMeDrivesResponsePages?.length) {
    r.rawMeDrivesResponsePages.forEach((page, i) => {
      console.log(`raw /me/drives page ${i + 1}`, page);
    });
  }
  console.groupEnd();
}

export const WORKSPACE_PREFS_STORAGE_KEY = 'sp_workspace_prefs';

export type WorkspaceFileDisplayMode = 'list' | 'grid';

export interface WorkspacePrefs {
  sidebarCollapsed: boolean;
  configMode: ApplicationTypeCode;
  fileDisplayMode?: WorkspaceFileDisplayMode;
}

export function readWorkspacePrefs(): WorkspacePrefs | null {
  try {
    const raw = localStorage.getItem(WORKSPACE_PREFS_STORAGE_KEY);
    if (!raw) return null;
    return JSON.parse(raw) as WorkspacePrefs;
  } catch {
    return null;
  }
}

export function writeWorkspacePrefs(prefs: WorkspacePrefs): void {
  try {
    localStorage.setItem(WORKSPACE_PREFS_STORAGE_KEY, JSON.stringify(prefs));
  } catch {}
}

export function removeWorkspacePrefs(): void {
  try {
    localStorage.removeItem(WORKSPACE_PREFS_STORAGE_KEY);
  } catch {}
}

export function mapRegisteredSitesToDropdownOptions(apps: ApplicationDto[]): SpDropdownOption[] {
  return apps.map((a) => ({
    id: a.applicationId,
    label: a.displayName,
    searchText: `${a.siteName ?? ''} ${a.hostName ?? ''} ${a.applicationTypeName ?? ''}`.trim(),
  }));
}

export function mapApplicationsToDropdownOptions(apps: ApplicationDto[]): SpDropdownOption[] {
  return apps.map((a) => ({
    id: a.applicationId,
    label: a.displayName,
    searchText: `${a.displayName} ${a.applicationTypeName ?? ''}`.trim(),
  }));
}

export function mapApplicationSitesToDropdownOptions(app: ApplicationDto): SpDropdownOption[] {
  return registeredApplicationSites(app).map((site, index) => ({
    id: String(index),
    label: site.siteName,
    searchText: `${site.siteName} ${site.hostName}`.trim(),
  }));
}

export function mapInternalAppsToDropdownOptions(apps: ApplicationDto[]): SpDropdownOption[] {
  return mapRegisteredSitesToDropdownOptions(apps.filter((a) => a.applicationTypeCode === 'tp_internal'));
}

export function mapUserDelegatedSitesToDropdownOptions(apps: ApplicationDto[]): SpDropdownOption[] {
  return mapRegisteredSitesToDropdownOptions(apps.filter((a) => a.applicationTypeCode === 'tp_user_delegated'));
}

export function mapLibrariesToDropdownOptions(libs: SharePointLibraryDto[]): SpDropdownOption[] {
  return libs.map((l) => ({ id: l.name, label: l.name }));
}

export function findDropdownOption(options: SpDropdownOption[], id: string | undefined | null): SpDropdownOption | null {
  const key = id?.trim();
  if (!key) return null;
  return options.find((o) => o.id === key) ?? null;
}

export function pickDefaultLibraryName(libs: SharePointLibraryDto[], preferredNames: string[]): string {
  for (const name of preferredNames) {
    if (!name?.trim()) continue;
    const hit = libs.find((l) => l.name.localeCompare(name, undefined, { sensitivity: 'base' }) === 0);
    if (hit) return hit.name;
  }
  return libs[0]?.name ?? preferredNames.find((n) => n?.trim()) ?? '';
}

export function preferredLibraryNames(config: {
  primary?: string;
  libraryName?: string;
  defaultLibraryName?: string;
}): string[] {
  const names: string[] = [];
  for (const name of [
    config.primary,
    config.libraryName,
    config.defaultLibraryName,
    SP_WORKSPACE.defaultLibrary,
    SP_WORKSPACE.sharedDocumentsLibrary,
  ]) {
    const trimmed = name?.trim();
    if (!trimmed) continue;
    if (!names.some((n) => n.localeCompare(trimmed, undefined, { sensitivity: 'base' }) === 0)) {
      names.push(trimmed);
    }
  }
  return names;
}

export function resolveWorkspaceLibraryName(
  libs: SharePointLibraryDto[],
  current: string | undefined,
  preferredNames: string[],
): string {
  const defaultName = pickDefaultLibraryName(libs, preferredNames);
  const normalized = current?.trim().toLowerCase();
  if (!normalized && libs.length) return defaultName;
  if (normalized && !libs.some((l) => l.name.toLowerCase() === normalized)) return defaultName;
  return current?.trim() || defaultName;
}

export function buildWorkspaceBreadcrumbs(params: {
  isUserMode: boolean;
  userSiteTitle: string;
  siteName?: string;
  libraryName?: string;
  folderPath: string;
}): string[] {
  const site = params.isUserMode
    ? (params.userSiteTitle || MODULE_BRANDING.siteLabel)
    : (params.siteName?.trim() || MODULE_BRANDING.siteLabel);
  const library = params.libraryName || SP_WORKSPACE.defaultLibrary;
  const folders = params.folderPath ? params.folderPath.split('/').filter(Boolean) : [];
  return [site, library, ...folders];
}

export interface ParsedSharePointSiteUrl {
  hostName: string;
  siteName: string;
  libraryName?: string;
  folderPath?: string;
}

export function parseSharePointSiteUrl(input: string): ParsedSharePointSiteUrl | null {
  const trimmed = input.trim();
  if (!trimmed) return null;

  if (!trimmed.includes('sharepoint.com') && !trimmed.startsWith('http')) {
    const siteName = trimmed.replace(/^\/+/, '');
    return siteName ? { hostName: '', siteName } : null;
  }

  let url: URL;
  try {
    url = new URL(trimmed.includes('://') ? trimmed : `https://${trimmed}`);
  } catch {
    return null;
  }

  const hostName = url.hostname;
  if (!hostName.includes('sharepoint.com')) return null;

  let path = decodeURIComponent(url.pathname);
  const sharing = path.match(/\/:b:\/r\/(.+)$/i) || path.match(/\/:f:\/r\/(.+)$/i) || path.match(/\/:x:\/r\/(.+)$/i);
  if (sharing) path = `/${sharing[1]}`;
  path = path.replace(/\/Forms\/[^/]*\.aspx$/i, '').replace(/\.aspx$/i, '');

  const sitesIdx = path.toLowerCase().indexOf('/sites/');
  if (sitesIdx < 0) {
    const parts = path.split('/').filter(Boolean);
    if (parts.length >= 2) {
      return { hostName, siteName: parts[0], libraryName: parts[1], folderPath: parts.slice(2).join('/') || undefined };
    }
    return parts[0] ? { hostName, siteName: parts[0] } : { hostName, siteName: '' };
  }

  const rest = path.slice(sitesIdx + 1);
  const segments = rest.split('/').filter(Boolean);
  if (!segments.length) return { hostName, siteName: '' };

  const siteName = segments.length >= 2 && segments[0].toLowerCase() === 'sites'
    ? `sites/${segments[1]}`
    : `sites/${segments[0]}`;
  const tailStart = segments[0].toLowerCase() === 'sites' ? 2 : 1;
  const tail = segments.slice(tailStart);

  return {
    hostName,
    siteName,
    libraryName: tail[0],
    folderPath: tail.length > 1 ? tail.slice(1).join('/') : undefined,
  };
}

export function buildUserSiteConnectRequest(siteInput: string, defaultHostName: string): UserConnectSiteRequest {
  const trimmed = siteInput.trim();
  if (trimmed.includes('sharepoint.com') || trimmed.startsWith('http')) {
    return { siteUrl: trimmed, hostName: defaultHostName };
  }
  return { siteName: trimmed, hostName: defaultHostName };
}

export function defaultUserSiteInput(user: SharepointUserConfig): string {
  const site = user.siteName?.trim();
  const host = user.hostName?.trim();
  if (site && host) return `https://${host}/sites/${site}`;
  if (site) return site;
  return '';
}

export function sharePointItemExtensionLabel(item: SharePointItemDto): string {
  if (item.isFolder) return SP_WORKSPACE.explorer.folderTypeLabel;
  const name = item.name;
  const dot = name.lastIndexOf('.');
  if (dot <= 0 || dot === name.length - 1) return SP_WORKSPACE.explorer.fileTypeLabel;
  return name.slice(dot + 1).toUpperCase();
}

const SHAREPOINT_FOLDER_ICON_SRC = (() => {
  const svg =
    '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">' +
    '<path fill="#a67c00" d="M4 7V5.5A1.5 1.5 0 0 1 5.5 4H10l1.5 1.5H4V7z"/>' +
    '<path fill="#d4a72c" d="M4 7h6.2L12.5 9H20a2 2 0 0 1 2 2v8.5A2.5 2.5 0 0 1 19.5 22H4.5A2.5 2.5 0 0 1 2 19.5V7z"/>' +
    '</svg>';
  return `data:image/svg+xml,${encodeURIComponent(svg)}`;
})();

export function sharePointFolderIconSrc(): string {
  return SHAREPOINT_FOLDER_ICON_SRC;
}

export function sharePointItemIconSrc(item: { isFolder: boolean; name: string; path?: string }): string {
  if (item.isFolder) return sharePointFolderIconSrc();
  return getMaterialFileIcon(item.path ?? item.name);
}

export function usesRichPreviewHost(kind: FileKind, filePath: string): boolean {
  const lower = filePath.toLowerCase();
  if (kind === 'pdf' || kind === 'csv' || kind === 'excel') return true;
  if (kind === 'powerpoint') return lower.endsWith('.pptx');
  if (kind === 'word') return lower.endsWith('.docx');
  return false;
}

export async function waitForDomHost(
  probe: () => HTMLElement | undefined,
  onTick?: () => void,
  attempts = 30,
  delayMs = 20,
): Promise<HTMLElement | null> {
  for (let i = 0; i < attempts; i++) {
    onTick?.();
    await new Promise<void>((r) => requestAnimationFrame(() => r()));
    const host = probe();
    if (host) return host;
    await delay(delayMs);
  }
  return null;
}

export function curlSampleFilePath(items: SharePointItemDto[], currentPath: string): string {
  const sampleName = SP_CURL_BUILD.sampleFilePath.replace(/^.*\//, '');
  const sampleFile = items.find((i) => !i.isFolder);
  if (sampleFile) {
    return sampleFile.path ?? (currentPath ? `${currentPath}/${sampleFile.name}` : sampleFile.name);
  }
  return currentPath ? `${currentPath}/${sampleName}` : SP_CURL_BUILD.sampleFilePath;
}

export class FolderNavHistory {
  private paths = [''];
  private index = 0;

  reset(): void {
    this.paths = [''];
    this.index = 0;
  }

  get canBack(): boolean {
    return this.index > 0;
  }

  get canForward(): boolean {
    return this.index < this.paths.length - 1;
  }

  push(path: string): void {
    if (this.index < this.paths.length - 1) {
      this.paths = this.paths.slice(0, this.index + 1);
    }
    if (this.paths[this.index] === path) return;
    this.paths.push(path);
    this.index = this.paths.length - 1;
  }

  back(): string {
    this.index--;
    return this.paths[this.index];
  }

  forward(): string {
    this.index++;
    return this.paths[this.index];
  }
}
