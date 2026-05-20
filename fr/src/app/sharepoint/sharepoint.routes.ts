import { Routes } from '@angular/router';
import { SharepointComponent } from './sharepoint.component';

/** Lazy-load or import into the host router, e.g. `{ path: 'sharepoint', children: SHAREPOINT_ROUTES }`. */
export const SHAREPOINT_ROUTES: Routes = [
  { path: '', component: SharepointComponent },
];
