import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProfilingComponent } from './profiling.component';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../shared/shared.module';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DatePicker } from 'primeng/datepicker';
import { PaginatorModule } from 'primeng/paginator';
import { ProfilingDetailsComponent } from './profiling-details/profiling-details.component';

const routes: Routes = [
  {
    path: '', component: ProfilingComponent
  }
]

@NgModule({
  declarations: [ProfilingComponent, ProfilingDetailsComponent],
  imports: [
    CommonModule,
    SharedModule,
    TableModule,
    ButtonModule,
    DatePicker,
    PaginatorModule,
    RouterModule.forChild(routes)
  ]
})
export class ProfilingModule { }
