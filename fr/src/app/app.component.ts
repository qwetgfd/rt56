import { Component } from '@angular/core';
import { SharepointComponent } from './sharepoint/sharepoint.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [SharepointComponent],
  template: '<app-sharepoint />',
})
export class AppComponent {}
