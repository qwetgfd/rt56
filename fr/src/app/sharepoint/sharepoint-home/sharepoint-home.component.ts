import { Component, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SharepointIconComponent } from '../core/sharepoint-icon.component';

@Component({
  selector: 'app-sharepoint-home',
  standalone: true,
  imports: [CommonModule, SharepointIconComponent],
  templateUrl: './sharepoint-home.component.html',
  styleUrls: ['./sharepoint-home.component.css', '../sharepoint.styles.css'],
})
export class SharepointHomeComponent {
  @Output() openWorkspace = new EventEmitter<void>();
  @Output() openApplications = new EventEmitter<void>();
}
