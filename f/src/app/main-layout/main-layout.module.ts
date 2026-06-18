import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NgbDatepickerModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { NgSelectModule } from '@ng-select/ng-select';
import { MainLayoutComponent } from './main-layout.component';
import { AdGroupComponent } from '../ad-group/ad-group.component';

@NgModule({
  declarations: [MainLayoutComponent, AdGroupComponent],
  imports: [    
    CommonModule,
    FormsModule,
    RouterModule,
    NgSelectModule,
    NgbTooltipModule,
    NgbDatepickerModule
  ],
  exports: [MainLayoutComponent]
})
export class MainLayoutModule { }