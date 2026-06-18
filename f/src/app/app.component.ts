import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MsalBroadcastService, MsalService } from '@azure/msal-angular';
import { AccountInfo, InteractionStatus } from '@azure/msal-browser';
import { filter } from 'rxjs/operators';
import { environment } from './environments/environment';
import { LoginService } from './core/services/login.service';
import { SharePointUserAuthService } from './sharepoint/services/sharepoint-user.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
  standalone: false,
})
export class AppComponent implements OnInit {
  title = 'DataIngestion';

  constructor(
    private msalService: MsalService,
    private msalBroadcastService: MsalBroadcastService,
    private loginService: LoginService,
    private sharePointAuth: SharePointUserAuthService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.msalService.initialize().subscribe(() => {
      this.msalService.handleRedirectObservable().subscribe((result) => {
        if (result?.account) {
          this.msalService.instance.setActiveAccount(result.account);
          void this.completeAzureLogin(result.account);
        }
      });

      this.msalBroadcastService.inProgress$
        .pipe(filter((status: InteractionStatus) => status === InteractionStatus.None))
        .subscribe(() => {
          const account =
            this.msalService.instance.getActiveAccount() ??
            this.msalService.instance.getAllAccounts()[0];
          if (account && !this.loginService.hasAppSession()) {
            this.msalService.instance.setActiveAccount(account);
            void this.completeAzureLogin(account);
          }
        });
    });
  }

  private async completeAzureLogin(account: AccountInfo): Promise<void> {
    let graphToken: string | undefined;
    try {
      const scopes = [...environment.userGraphScopes];
      graphToken = (
        await this.msalService.instance.acquireTokenSilent({ scopes, account })
      ).accessToken;
    } catch (err) {
      console.warn('MSAL silent token failed; using local dev session bootstrap.', err);
    }

    // #region Sharepoint Workspace - AY
    this.loginService.applyMsalAccount(account, graphToken);

    if (this.sharePointAuth.isConfigured) {
      try {
        await this.sharePointAuth.syncFromMsalAccount(account);
      } catch (err) {
        console.warn('SharePoint auth sync skipped.', err);
      }
    }
    // #endregion

    if (this.router.url === '/login' || this.router.url === '/') {
      await this.router.navigate(['/mainlayout/dashboard']);
    }
  }
}
