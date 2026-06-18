import { inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { MsalService } from '@azure/msal-angular';
import { AccountInfo, InteractionRequiredAuthError, PopupRequest, PublicClientApplication } from '@azure/msal-browser';
import { firstValueFrom, Observable, catchError, from, map, switchMap, tap, throwError } from 'rxjs';
import { SHAREPOINT_ENV, type SharepointUserConfig } from '../core/sharepoint.config';
import { SP_API, SP_USER_AUTH } from '../core/sharepoint.messages';
import { ApiEnvelope, ApplicationDto, MeDrivesDiscoveryReportDto, SharePointItemDto, UserBrowseRequest, UserConnectSiteRequest, UserConnectSiteResultDto, UserFileRequest } from '../core/sharepoint.types';
import { decodeJwtPayload, encodePath64, logMeDrivesDiagnostics, unwrapApiEnvelope } from '../core/sharepoint.utils';

const DEFAULT_GRAPH_SCOPES = [
  'https://graph.microsoft.com/User.Read',
  'https://graph.microsoft.com/Files.Read',
  'https://graph.microsoft.com/Sites.Read.All',
];

const CONNECT_AFTER_LOGIN_KEY = 'sharepoint-user-connect-after-login';

export type MsalPreAngularBootstrap = 'popup-only' | 'ready';

@Injectable({ providedIn: 'root' })
export class SharePointUserAuthService {
  private static sharedPca: PublicClientApplication | null = null;
  private static sharedPcaKey: string | null = null;

  private readonly env = inject(SHAREPOINT_ENV);
  private readonly msal = inject(MsalService, { optional: true });
  private pca: PublicClientApplication | null = null;
  private initializePromise: Promise<void> | null = null;

  static async bootstrapBeforeAngular(user: SharepointUserConfig): Promise<MsalPreAngularBootstrap> {
    if (!user.authority?.trim() || !user.clientId?.trim()) return 'ready';

    const client = await SharePointUserAuthService.ensureSharedPca(user.clientId, user.authority);

    if (SharePointUserAuthService.isPopupWindow()) {
      SharePointUserAuthService.showPopupSigningInMessage();
      await client.handleRedirectPromise();
      return 'popup-only';
    }

    return 'ready';
  }

  private static isPopupWindow(): boolean {
    return typeof window !== 'undefined' && window.opener != null && window.opener !== window;
  }

  private static showPopupSigningInMessage(): void {
    document.body.replaceChildren();
    const el = document.createElement('p');
    el.textContent = SP_USER_AUTH.completingSignIn;
    el.style.cssText = 'margin:2rem;font-family:system-ui,sans-serif;color:#323130';
    document.body.appendChild(el);
  }

  private static async ensureSharedPca(clientId: string, authority: string): Promise<PublicClientApplication> {
    const key = `${authority}:${clientId}`;
    if (!SharePointUserAuthService.sharedPca || SharePointUserAuthService.sharedPcaKey !== key) {
      SharePointUserAuthService.sharedPca = new PublicClientApplication({
        auth: {
          clientId,
          authority,
          redirectUri: window.location.origin,
        },
        cache: { cacheLocation: 'sessionStorage' },
      });
      SharePointUserAuthService.sharedPcaKey = key;
      await SharePointUserAuthService.sharedPca.initialize();
    }
    return SharePointUserAuthService.sharedPca;
  }

  readonly account = signal<AccountInfo | null>(null);
  readonly isSignedIn = signal(false);

  private get user(): SharepointUserConfig {
    return this.env;
  }

  get isConfigured(): boolean {
    return !!(this.user.authority?.trim() && this.user.clientId?.trim());
  }

  get graphScopes(): string[] {
    const configured = this.user.userGraphScopes?.filter((s) => s.trim().length > 0);
    return configured?.length ? configured : DEFAULT_GRAPH_SCOPES;
  }

  get hasSitesReadAllScope(): boolean {
    return this.graphScopes.some((s) => /Sites\.Read\.All/i.test(s));
  }

  initialize(): Promise<void> {
    if (!this.isConfigured) return Promise.resolve();
    if (!this.initializePromise) {
      this.initializePromise = this.useEnvironment().catch((e) => {
        this.initializePromise = null;
        throw e;
      });
    }
    return this.initializePromise;
  }

  async useCredentials(authority: string, clientId: string): Promise<void> {
    this.pca = await SharePointUserAuthService.ensureSharedPca(clientId, authority);
    const msalAccount = this.readMsalAccount();
    if (msalAccount) {
      this.pca.setActiveAccount(msalAccount);
      this.applyAccount(msalAccount);
      return;
    }
    const redirect = await this.pca.handleRedirectPromise();
    if (redirect?.account) {
      this.pca.setActiveAccount(redirect.account);
      this.applyAccount(redirect.account);
      return;
    }
    const account = this.pca.getActiveAccount() ?? this.pca.getAllAccounts()[0] ?? null;
    if (account) {
      this.pca.setActiveAccount(account);
      this.applyAccount(account);
    } else {
      this.clearLocalSession();
    }
  }

  async syncFromMsalAccount(account: AccountInfo): Promise<void> {
    if (!this.isConfigured) return;
    this.pca = await SharePointUserAuthService.ensureSharedPca(this.user.clientId, this.user.authority);
    this.pca.setActiveAccount(account);
    this.applyAccount(account);
  }

  async useEnvironment(): Promise<void> {
    await this.useCredentials(this.user.authority, this.user.clientId);
  }

  async useApplication(app: ApplicationDto): Promise<void> {
    await this.useCredentials(`https://login.microsoftonline.com/${app.tenantId}`, app.clientId);
  }

  async signInRedirect(): Promise<void> {
    await this.initialize();
    const existing =
      this.readMsalAccount() ??
      this.requireClient().getActiveAccount() ??
      this.account();
    if (existing) {
      this.requireClient().setActiveAccount(existing);
      this.applyAccount(existing);
      return;
    }
    if (this.msal) {
      await firstValueFrom(this.msal.loginRedirect({ scopes: this.graphScopes }));
      return;
    }
    await this.requireClient().loginRedirect(this.buildLoginRequest('select_account'));
  }

  async signInPopup(): Promise<void> {
    await this.initialize();
    const result = await this.requireClient().loginPopup(this.buildLoginRequest('select_account'));
    if (result.account) {
      this.requireClient().setActiveAccount(result.account);
      this.applyAccount(result.account);
    }
  }

  async consentPopup(): Promise<void> {
    await this.initialize();
    const client = this.requireClient();
    const account = client.getActiveAccount() ?? this.account();
    const request: PopupRequest = {
      scopes: this.graphScopes,
      prompt: 'consent',
      ...(account ? { account } : {}),
    };
    try {
      const result = account
        ? await client.acquireTokenPopup(request)
        : await client.loginPopup(request);
      if (result.account) {
        client.setActiveAccount(result.account);
        this.applyAccount(result.account);
      }
    } catch (e) {
      if (e instanceof InteractionRequiredAuthError) {
        const login = await client.loginPopup({ ...request, prompt: 'consent' });
        if (login.account) {
          client.setActiveAccount(login.account);
          this.applyAccount(login.account);
        }
        return;
      }
      throw e;
    }
  }

  markConnectAfterLogin(): void {
    sessionStorage.setItem(CONNECT_AFTER_LOGIN_KEY, '1');
  }

  consumeConnectAfterLogin(): boolean {
    const pending = sessionStorage.getItem(CONNECT_AFTER_LOGIN_KEY) === '1';
    if (pending) sessionStorage.removeItem(CONNECT_AFTER_LOGIN_KEY);
    return pending && this.isSignedIn();
  }

  async signOut(): Promise<void> {
    const client = this.pca;
    if (!client) return;
    const active = client.getActiveAccount();
    this.clearLocalSession();
    if (active) {
      await client.logoutRedirect({ account: active });
    }
  }

  async acquireGraphToken(): Promise<string> {
    await this.initialize();
    const client = this.requireClient();
    const account = this.readMsalAccount() ?? client.getActiveAccount() ?? this.account();
    if (!account) throw new Error(SP_USER_AUTH.signInFromHomeFirst);
    const scopes = this.graphScopes;
    if (this.msal) {
      try {
        return (await firstValueFrom(this.msal.acquireTokenSilent({ scopes, account }))).accessToken;
      } catch (e) {
        if (e instanceof InteractionRequiredAuthError) {
          return (await firstValueFrom(this.msal.acquireTokenPopup({ scopes, account }))).accessToken;
        }
        throw e;
      }
    }
    return this.acquireTokenFromPca(client, scopes, account, 'consent');
  }

  parseRedirectErrorFromUrl(): string | null {
    const hash = window.location.hash?.startsWith('#') ? window.location.hash.slice(1) : window.location.hash ?? '';
    if (!hash) return null;
    const params = new URLSearchParams(hash);
    const raw = params.get('error_description') ?? params.get('error');
    if (!raw) return null;
    try {
      return decodeURIComponent(raw.replace(/\+/g, ' '));
    } catch {
      return raw;
    }
  }

  clearRedirectHashFromUrl(): void {
    const path = window.location.pathname + window.location.search;
    window.history.replaceState(null, '', path);
  }

  private readMsalAccount(): AccountInfo | null {
    if (!this.msal) return null;
    return this.msal.instance.getActiveAccount() ?? this.msal.instance.getAllAccounts()[0] ?? null;
  }

  private buildLoginRequest(prompt: 'select_account' | 'consent'): PopupRequest {
    return { scopes: this.graphScopes, prompt };
  }

  private applyAccount(account: AccountInfo): void {
    this.account.set(account);
    this.isSignedIn.set(true);
  }

  private clearLocalSession(): void {
    this.account.set(null);
    this.isSignedIn.set(false);
  }

  private async acquireTokenFromPca(
    client: PublicClientApplication,
    scopes: string[],
    account: AccountInfo,
    popupPrompt?: PopupRequest['prompt'],
  ): Promise<string> {
    try {
      return (await client.acquireTokenSilent({ scopes, account })).accessToken;
    } catch (e) {
      if (e instanceof InteractionRequiredAuthError) {
        return (await client.acquireTokenPopup({ scopes, account, ...(popupPrompt ? { prompt: popupPrompt } : {}) })).accessToken;
      }
      throw e;
    }
  }

  private requireClient(): PublicClientApplication {
    const client = this.pca ?? SharePointUserAuthService.sharedPca;
    if (!client) throw new Error(SP_USER_AUTH.notInitialized);
    this.pca = client;
    return client;
  }
}

@Injectable({ providedIn: 'root' })
export class SharePointUserApiService {
  private readonly baseUrl = inject(SHAREPOINT_ENV).apiBaseUrl.replace(/\/$/, '');
  private readonly http = inject(HttpClient);
  private readonly auth = inject(SharePointUserAuthService);

  connectSite(request: UserConnectSiteRequest): Observable<UserConnectSiteResultDto> {
    return this.post(`${this.baseUrl}/workspace/user/connect-site`, request, 'connect-site');
  }

  debugMeDrives(request: UserConnectSiteRequest): Observable<MeDrivesDiscoveryReportDto> {
    return this.post(`${this.baseUrl}/workspace/user/debug-me-drives`, request, 'debug-me-drives');
  }

  browseFolder(driveId: string, folderPath?: string): Observable<SharePointItemDto[]> {
    const body: UserBrowseRequest = { driveId, folderPath: folderPath || undefined };
    return this.post(`${this.baseUrl}/workspace/user/browse`, body, 'browse');
  }

  fetchFileBlob(driveId: string, filePath: string): Observable<Blob> {
    const body: UserFileRequest = { driveId, filePath };
    return from(this.auth.acquireGraphToken()).pipe(
      switchMap((token) =>
        this.http.post(`${this.baseUrl}/workspace/user/fetchfile`, body, {
          headers: this.graphJsonHeaders(token),
          responseType: 'blob',
        }),
      ),
    );
  }

  resolveStreamingUrl(driveId: string, filePath: string): Observable<string> {
    return from(this.auth.acquireGraphToken()).pipe(
      map((token) => this.buildStreamingUrl(driveId, filePath, token)),
    );
  }

  buildStreamingUrl(driveId: string, filePath: string, accessToken: string): string {
    const params = new URLSearchParams({
      driveId: driveId.trim(),
      path64: encodePath64(filePath),
      access_token: accessToken,
    });
    return `${this.baseUrl}/workspace/user/file?${params.toString()}`;
  }

  private graphJsonHeaders(token: string): HttpHeaders {
    return new HttpHeaders({ Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' });
  }

  private post<T>(url: string, body: unknown, label: string): Observable<T> {
    return from(this.auth.acquireGraphToken()).pipe(
      tap((token) => {
        const claims = decodeJwtPayload(token);
        console.log(`[SharePoint User] ${label} request`, { url, body });
        console.log(`[SharePoint User] ${label} token claims`, {
          scp: claims?.['scp'],
          roles: claims?.['roles'],
          tid: claims?.['tid'],
          upn: claims?.['upn'] ?? claims?.['preferred_username'],
          aud: claims?.['aud'],
          exp: claims?.['exp'],
        });
      }),
      switchMap((token) =>
        this.http.post<ApiEnvelope<T>>(url, body, {
          headers: this.graphJsonHeaders(token),
          observe: 'response',
        }),
      ),
      tap((res) => {
        console.log(`[SharePoint User] ${label} HTTP ${res.status}`, res.body);
        const data = res.body?.data as MeDrivesDiscoveryReportDto | undefined;
        if (data?.drives) logMeDrivesDiagnostics(`${label} success diagnostics`, data);
      }),
      map((res) => unwrapApiEnvelope(res.body, SP_API.requestFailed) as T),
      catchError((err: unknown) => this.logAndRethrow(label, err)),
    );
  }

  private logAndRethrow(label: string, err: unknown): Observable<never> {
    console.error(`[SharePoint User] ${label} failed`, err);
    if (err instanceof HttpErrorResponse) {
      console.log(`[SharePoint User] ${label} HTTP ${err.status}`, err.error);
      const data = (err.error as ApiEnvelope<unknown> | null)?.data as
        | MeDrivesDiscoveryReportDto
        | { graphUrl?: string; graphStatus?: number; graphBody?: string; tokenScopes?: string }
        | null;
      if (data && 'graphUrl' in data && data.graphUrl) {
        console.group(`[SharePoint User] ${label} Graph failure`);
        console.log('graphUrl', data.graphUrl);
        console.log('graphStatus', data.graphStatus);
        console.log('tokenScopes', data.tokenScopes);
        console.log('graphBody', data.graphBody);
        console.groupEnd();
      } else if (data && 'drives' in data) {
        logMeDrivesDiagnostics(`${label} error diagnostics (see data)`, data);
      }
    }
    return throwError(() => err);
  }
}
