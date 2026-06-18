import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { NotificationComponent } from './components/notification/notification.component';
import { DecimalFourPlacesDirective, NospacesDirective, NumberOnlyDirective } from './directives/nospaces.directive';
import { NgSelectModule } from '@ng-select/ng-select';

import { MatExpansionModule } from '@angular/material/expansion';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { IsFabUserPipe, KeysPipe } from './CustomPipes/pipes';
import { ErrorBoxComponent } from './components/error-box/error-box.component';

@NgModule({
  declarations: [NotificationComponent, NospacesDirective, KeysPipe,IsFabUserPipe,NumberOnlyDirective, DecimalFourPlacesDirective, ErrorBoxComponent],
  imports: [CommonModule, FormsModule, ReactiveFormsModule, NgSelectModule],
  exports: [
    KeysPipe,
    IsFabUserPipe,
    NospacesDirective,
    FormsModule,
    ReactiveFormsModule,
    NgSelectModule,
    NumberOnlyDirective,
    ErrorBoxComponent,
    DecimalFourPlacesDirective,
    MatExpansionModule,
    MatListModule,
    MatIconModule
  ],
})
export class SharedModule {}
