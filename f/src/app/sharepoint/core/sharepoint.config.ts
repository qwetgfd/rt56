import { EnvironmentProviders, InjectionToken, makeEnvironmentProviders } from '@angular/core';
import { environment } from '../../environments/environment';

export interface SharepointEnvironment {
  production: boolean;
  apiBaseUrl: string;
  apiVersion: string;
  ownerKey: string;
  defaultLibraryName: string;
  tenantName: string;
  clientId: string;
  authority: string;
  hostName: string;
  siteName: string;
  libraryName: string;
  userGraphScopes: string[];
}

export type SharepointUserConfig = Pick<
  SharepointEnvironment,
  'tenantName' | 'clientId' | 'authority' | 'hostName' | 'siteName' | 'libraryName' | 'userGraphScopes'
>;

export const SHAREPOINT_ENV = new InjectionToken<SharepointEnvironment>('SHAREPOINT_ENV');

export function provideSharepoint(config: SharepointEnvironment = environment): EnvironmentProviders {
  return makeEnvironmentProviders([{ provide: SHAREPOINT_ENV, useValue: config }]);
}
