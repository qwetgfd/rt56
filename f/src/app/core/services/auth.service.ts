import { Inject, Injectable } from '@angular/core';
import { Subject, ReplaySubject, filter, takeUntil, BehaviorSubject } from 'rxjs';
import { UserDetails, } from '../models/userDetails';
import { Router } from '@angular/router';
import { MSAL_GUARD_CONFIG, MsalGuardConfiguration, MsalService, MsalBroadcastService,} from '@azure/msal-angular';
import { AccountInfo, AuthenticationResult, InteractionStatus, InteractionType, PopupRequest, RedirectRequest, } from '@azure/msal-browser';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { APIResponse } from '../models/apiResponse';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  loggedIn = false;
  private readonly _destroying$ = new Subject<void>();
  private currentUserSource = new ReplaySubject<UserDetails | null>(1);
  currentUser$ = this.currentUserSource.asObservable();

  private baseUrl = environment.apiEndpoint;
  
  constructor(
    @Inject(MSAL_GUARD_CONFIG) private msalGuardConfig: MsalGuardConfiguration,
    private authService: MsalService,
    private router: Router,
    private msalBroadcastService: MsalBroadcastService,
    private http: HttpClient
  ) { }


  setHttpsRequestHeaders(apiVersion: string): HttpHeaders {
    let objHttpHeaders = new HttpHeaders();
    objHttpHeaders = objHttpHeaders.append(
      'Authorization',
      `Bearer ${localStorage.getItem('DIApiToken')}`
    );
    objHttpHeaders = objHttpHeaders.set(
      'Content-Type',
      'application/json; charset=utf-8'
    );
    objHttpHeaders = objHttpHeaders.set('Accept', 'application/json');
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-version', apiVersion);
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-sg', `${sessionStorage.getItem("GUID")}`);
    return objHttpHeaders;
  }

  updateLoggedInStatus() {
    this.msalBroadcastService.inProgress$
      .pipe(
        filter(
          (status: InteractionStatus) => status === InteractionStatus.None
        ),
        takeUntil(this._destroying$)
      )
      .subscribe(() => {
        this.setLoggedIn();
        //this.checkAndSetActiveAccount();
      });
  }

  private setLoggedIn() {
    this.loggedIn = this.authService.instance.getAllAccounts().length > 0;
    if (!this.loggedIn) {
      //this.router.navigate(['/login']);
      //window.location.reload();
    }
  }

  private checkAndSetActiveAccount() {
    /**
     * If no active account set but there are accounts signed in, sets first account to active account
     * To use active account set here, subscribe to inProgress$ first in your component
     * Note: Basic usage demonstrated. Your app may require more complicated account selection logic
     */
    let activeAccount = this.authService.instance.getActiveAccount();
    if (
      !activeAccount &&
      this.authService.instance.getAllAccounts().length > 0
    ) {
      let accounts = this.authService.instance.getAllAccounts();
      this.authService.instance.setActiveAccount(accounts[0]);
      var Upn = accounts[0].username;
      sessionStorage.setItem('Upn', Upn);
      var username = accounts[0].username.split('@'); //quijano.61@teleperformance.com
      sessionStorage.setItem('username', username[0]); //quijano.61
      var name = accounts[0].name!;
      sessionStorage.setItem('FullName', name); //wilbert quijano
      window.location.reload();
    }
  }

  login() {
    if (this.msalGuardConfig.authRequest) {
      this.authService.loginRedirect({
        ...this.msalGuardConfig.authRequest,
      } as RedirectRequest);
    } else {
      this.authService.loginRedirect();
    }
    // if (this.msalGuardConfig.interactionType === InteractionType.Popup) {
    //     this.loginWithPopup();
    // } else {
    //     this.loginWithRedirect();
    // }
  }

  getActiveAccount(): AccountInfo | null {
    return this.authService.instance.getActiveAccount();
  }

  getAccountHasAccessCreateGlobalRule() {

    let headers: HttpHeaders = this.setHttpsRequestHeaders('4.1');
    let url = this.baseUrl + 'api/ProcessConfiguration/GetGlobalRuleCreationAccess';
    return this.http.get<APIResponse<boolean>>(url, {
      headers: headers,
    });

  }


  private loginWithPopup() {
    if (this.msalGuardConfig.authRequest) {
      this.authService
        .loginPopup({ ...this.msalGuardConfig.authRequest } as PopupRequest)
        .subscribe((response: AuthenticationResult) => {
          this.authService.instance.setActiveAccount(response.account);
        });
    } else {
      this.authService
        .loginPopup()
        .subscribe((response: AuthenticationResult) => {
          this.authService.instance.setActiveAccount(response.account);
        });
    }
  }

  private loginWithRedirect() {
    if (this.msalGuardConfig.authRequest) {
      this.authService.loginRedirect({
        ...this.msalGuardConfig.authRequest,
      } as RedirectRequest);
    } else {
      this.authService.loginRedirect();
    }
  }


  logout() {
    sessionStorage.clear();
    localStorage.clear();
    this.authService.logoutRedirect();
  }

  destroy() {
    this._destroying$.next(undefined);
    this._destroying$.complete();
  }
}
