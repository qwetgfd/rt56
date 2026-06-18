import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { EibComponent } from './eib.component';
import { CustomEIBViewComponent } from './custom-eib-view/custom-eib-view.component';
import { EibDownloadComponent } from './eib-download/eib-download.component';

const routes: Routes = [
  { path: '', component: EibComponent },
  { path: 'custom-eib-view', component: CustomEIBViewComponent },
  { path: 'eib-download', component: EibDownloadComponent },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class EibRoutingModule {}
