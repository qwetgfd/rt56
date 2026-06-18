// #region Sharepoint Workspace - AY
// MSAL configuration adapted for SharePoint delegated auth and Entra credentials
import {
  BrowserCacheLocation,
  InteractionType,
  IPublicClientApplication,
  PublicClientApplication,
} from '@azure/msal-browser';
import {
  MsalGuardConfiguration,
  MsalInterceptorConfiguration,
} from '@azure/msal-angular';
import { environment } from '../../environments/environment';

/** Azure login — same credentials as SharePoint environment config. */
function sharePointMsalAuth() {
  const { authority, clientId } = environment;
  return {
    clientId,
    authority,
    redirectUri: typeof window !== 'undefined' ? window.location.origin : '/',
  };
}

function sharePointGraphScopes(): string[] {
  return [...environment.userGraphScopes];
}

export function MSALInstanceFactory(): IPublicClientApplication {
  return new PublicClientApplication({
    auth: sharePointMsalAuth(),
    cache: {
      // Match SharePoint standalone (SharePointUserAuthService uses sessionStorage).
      cacheLocation: BrowserCacheLocation.SessionStorage,
    },
  });
}

export function MSALGuardConfigFactory(): MsalGuardConfiguration {
  return {
    interactionType: InteractionType.Redirect,
    authRequest: {
      scopes: sharePointGraphScopes(),
    },
  };
}

export function MSALInterceptorConfigFactory(): MsalInterceptorConfiguration {
  const scopes = sharePointGraphScopes();
  const protectedResourceMap = new Map<string, Array<string> | null>();

  // Graph API — requires MSAL token.
  protectedResourceMap.set(environment.graphApiEndpoint, scopes);
  protectedResourceMap.set(`${environment.graphApiEndpoint}/*`, scopes);

  // SharePoint / DI API endpoints — handled by their own auth; MSAL must NOT touch these.
  protectedResourceMap.set(`${environment.sharepointApiBaseUrl}*`, null);
  protectedResourceMap.set(`${environment.apiEndpoint}*`, null);

  return {
    interactionType: InteractionType.Redirect,
    protectedResourceMap,
  };
}
