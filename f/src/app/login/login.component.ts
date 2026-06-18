import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { AuthService } from '../core/services/auth.service';
import { ActivatedRoute, Router } from '@angular/router';
import { LoginService } from '../core/services/login.service';
import { environment } from '../environments/environment';
import { MsalService } from '@azure/msal-angular';

@Component({
    selector: 'app-login',
    templateUrl: './login.component.html',
    styleUrl: './login.component.css',
    standalone: false
})
export class LoginComponent implements OnInit {
  @Input() loggedIn: boolean = false;
  @Output() loginClicked = new EventEmitter<void>();
  @Output() logoutClicked = new EventEmitter<void>();

  isLogin: boolean = false;
  year: any;
  returnUrl: string = '';
  environment = environment;

  constructor(
    public authService: AuthService,
    private msalService: MsalService,
    private router: Router,
    private activatedRouter: ActivatedRoute,
    private loginService: LoginService
  ) {}

  ngOnInit(): void {
    const datey = new Date();
    this.year = datey.getUTCFullYear();
    this.loginService.currentUser$.pipe();
    if (this.loginService.hasAppSession()) {
      this.loggedIn = true;
      this.router.navigate(['/mainlayout/dashboard']);
      return;
    }
    const msalAccount =
      this.msalService.instance.getActiveAccount() ??
      this.msalService.instance.getAllAccounts()[0];
    if (msalAccount) {
      this.loginService.applyMsalAccount(msalAccount);
      this.authService.loggedIn = true;
      this.loggedIn = true;
      this.router.navigate(['/mainlayout/dashboard']);
    }
  }

  login() {
    this.msalService.instance.loginRedirect({
      scopes: [...environment.userGraphScopes],
    });
  }

  guestLogin() {
    this.loginService.guestLogin();
    this.authService.loggedIn = true;
    this.router.navigate(['/mainlayout/dashboard']);
  }

  logout() {
    this.logoutClicked.emit();
    this.authService.logout();
    sessionStorage.clear();    
    //console.log("login.component.ts logout()");
  }
}
