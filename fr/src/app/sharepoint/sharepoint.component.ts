import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SharepointHomeComponent } from './sharepoint-home/sharepoint-home.component';
import { SharepointApplicationsComponent } from './sharepoint-applications/sharepoint-applications.component';
import { SharepointWorkspaceComponent } from './sharepoint-workspace/sharepoint-workspace.component';
import { ApplicationDto } from './core/sharepoint.types';
type SpView = 'home' | 'applications' | 'workspace';

@Component({
  selector: 'app-sharepoint',
  standalone: true,
  imports: [CommonModule, SharepointHomeComponent, SharepointApplicationsComponent, SharepointWorkspaceComponent],
  templateUrl: './sharepoint.component.html',
  styleUrls: ['./sharepoint.component.css', './sharepoint.styles.css'],
})
export class SharepointComponent {
  view: SpView = 'home';
  pendingApplicationId: string | null = null;

  goHome(): void { this.view = 'home'; this.pendingApplicationId = null; }
  goApplications(): void { this.view = 'applications'; this.pendingApplicationId = null; }
  goWorkspace(): void { this.view = 'workspace'; }

  onUseApplication(app: ApplicationDto): void {
    this.pendingApplicationId = app.applicationId;
    this.view = 'workspace';
  }

  onLaunchApplicationConsumed(): void { this.pendingApplicationId = null; }
}
