import { EnvironmentProviders, InjectionToken, makeEnvironmentProviders } from '@angular/core';

export interface SharepointEnvironment {
  production: boolean;
  apiBaseUrl: string;
  apiVersion: string;
  ownerKey: string;
  defaultLibraryName: string;
}

export const DEFAULT_SHAREPOINT_ENVIRONMENT: SharepointEnvironment = {
  production: false,
  apiBaseUrl: 'https://localhost:54846/api/',
  apiVersion: '1.0',
  ownerKey: 'system',
  defaultLibraryName: 'Documents',
};

export const SHAREPOINT_ENV = new InjectionToken<SharepointEnvironment>('SHAREPOINT_ENV');

export function provideSharepoint(config?: Partial<SharepointEnvironment>): EnvironmentProviders {
  return makeEnvironmentProviders([
    { provide: SHAREPOINT_ENV, useValue: { ...DEFAULT_SHAREPOINT_ENVIRONMENT, ...config } },
  ]);
}
