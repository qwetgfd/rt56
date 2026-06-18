import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SharepointHomeComponent } from './sharepoint-home/sharepoint-home.component';
import { SharepointApplicationsComponent } from './sharepoint-applications/sharepoint-applications.component';
import { SharepointWorkspaceComponent } from './sharepoint-workspace/sharepoint-workspace.component';
import { ApplicationDto } from './core/sharepoint.types';
import { fireAndForget } from './core/sharepoint.utils';
import { SharePointApiService } from './services/sharepoint-api.service';
import { SharePointUserAuthService } from './services/sharepoint-user.service';

type SpView = 'home' | 'applications' | 'workspace';

@Component({
  selector: 'app-sharepoint',
  standalone: true,
  imports: [CommonModule, SharepointHomeComponent, SharepointApplicationsComponent, SharepointWorkspaceComponent],
  templateUrl: './sharepoint.component.html',
  styleUrls: ['./sharepoint-tokens.css', './sharepoint.component.css', './sharepoint.styles.css'],
})
export class SharepointComponent implements OnInit {
  private readonly auth = inject(SharePointUserAuthService);
  private readonly api = inject(SharePointApiService);

  view: SpView = 'home';
  pendingApplicationId: string | null = null;

  async ngOnInit(): Promise<void> {
    if (this.auth.isConfigured) await this.auth.initialize();
  }

  goHome(): void { this.view = 'home'; this.pendingApplicationId = null; }
  goApplications(): void { this.view = 'applications'; this.pendingApplicationId = null; }
  goWorkspace(): void { this.view = 'workspace'; }

  onUseApplication(app: ApplicationDto): void {
    this.pendingApplicationId = app.applicationId;
    this.view = 'workspace';
    const account = this.auth.account();
    fireAndForget(this.api.recordApplicationUsage(app.applicationId, {
      displayName: app.displayName,
      usedByUpn: account?.username,
      usedByDisplayName: account?.name,
    }));
  }

  onLaunchApplicationConsumed(): void { this.pendingApplicationId = null; }
}
