import { Component, OnInit, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MODULE_BRANDING, SP_HOME_AUTH, SP_HOME_PAGE } from '../core/sharepoint.messages';
import { SharepointIconComponent } from '../core/sharepoint.ui';
import { SharePointUserAuthService } from '../services/sharepoint-user.service';
import { parseApiError } from '../core/sharepoint.utils';

@Component({
  selector: 'app-sharepoint-home',
  standalone: true,
  imports: [CommonModule, SharepointIconComponent],
  templateUrl: './sharepoint-home.component.html',
  styleUrls: ['./sharepoint-home.component.css', '../sharepoint.styles.css'],
})
export class SharepointHomeComponent implements OnInit {
  @Output() openWorkspace = new EventEmitter<void>();
  @Output() openApplications = new EventEmitter<void>();

  readonly auth = inject(SharePointUserAuthService);
  readonly branding = MODULE_BRANDING;
  readonly authMsg = SP_HOME_AUTH;
  readonly page = SP_HOME_PAGE;
  authError = '';
  signingIn = false;

  async ngOnInit(): Promise<void> {
    if (!this.auth.isConfigured) return;
    try {
      const redirectError = this.auth.parseRedirectErrorFromUrl();
      if (redirectError) {
        this.authError = redirectError;
        this.auth.clearRedirectHashFromUrl();
        return;
      }
      await this.auth.initialize();
      this.auth.clearRedirectHashFromUrl();
    } catch (e) {
      this.authError = parseApiError(e, SP_HOME_AUTH.signInIncomplete);
    }
  }

  async signIn(): Promise<void> {
    this.authError = '';
    if (!this.auth.isConfigured) {
      this.authError = SP_HOME_AUTH.notConfigured;
      return;
    }
    this.signingIn = true;
    try {
      await this.auth.signInRedirect();
    } catch (e) {
      this.authError = parseApiError(e, SP_HOME_AUTH.signInFailed);
      this.signingIn = false;
    }
  }

  signOut(): void { this.authError = ''; void this.auth.signOut(); }
}
