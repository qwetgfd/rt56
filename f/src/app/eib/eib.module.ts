import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { EibComponent } from './eib.component';
import { SharedModule } from '../shared/shared.module';
import { NgApexchartsModule } from 'ng-apexcharts';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DatePicker } from 'primeng/datepicker';
import { PaginatorModule } from 'primeng/paginator';
import { EibDownloadComponent } from './eib-download/eib-download.component';

const routes: Routes = [
  {
    path: '', component: EibComponent
  }
]

@NgModule({
  declarations: [EibComponent, EibDownloadComponent],
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
export class EibModule { }
