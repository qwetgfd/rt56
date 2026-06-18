import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { ProfilingComponent } from './profiling.component';
import { ProfilingDetailsComponent } from './profiling-details/profiling-details.component';

const routes: Routes = [
  { path: '', component: ProfilingComponent },
  { path: ':id', component: ProfilingDetailsComponent },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class ProfilingRoutingModule {}
