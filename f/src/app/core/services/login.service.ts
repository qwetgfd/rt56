import { Injectable } from '@angular/core';
import { AccountInfo } from '@azure/msal-browser';
import { environment } from '../../environments/environment';
import { map, of, ReplaySubject } from 'rxjs';
import {
  AzureUserGroup,
  AzureUserGroupId,
  SecurityGroup,
  UserDetails,
} from '../models/userDetails';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Router } from '@angular/router';
import { APIResponse } from '../models/apiResponse';
import { TokenService } from './token.service';

import { GUEST_TOKEN } from '../guest/guest.util';

@Injectable({
  providedIn: 'root',
})
export class LoginService {
  private baseUrl = environment.apiEndpoint;
  private currentUserSource = new ReplaySubject<UserDetails | null>(1);
  currentUser$ = this.currentUserSource.asObservable();
  userGroupIds: AzureUserGroupId[];
  NTIDencrypt: any;
  result: any;
  bytes: any;
  i: any;
  wa: any;
  j: any;

  constructor(
    private _http: HttpClient,
    private router: Router,
    private tokenService: TokenService
  ) {}

  /**
   * Local environment to be deleted later -AY — guest bypass token when not production.
   */
  private resolveAppApiToken(msalAccessToken?: string): string {
    if (environment.production && msalAccessToken?.trim()) {
      return msalAccessToken;
    }
    return GUEST_TOKEN;
  }

  /** Shared session bootstrap — guest login and MSAL login both use this. */
  private bootstrapAppSession(params: {
    upn: string;
    username: string;
    fullName: string;
    email: string;
    userId: string;
    guid: string;
    groupName: string;
    jobTitle: string;
    apiToken: string;
    isGuest: boolean;
  }): void {
    if (params.isGuest) {
      sessionStorage.setItem('isGuestLogin', 'true');
    } else {
      sessionStorage.removeItem('isGuestLogin');
    }
    sessionStorage.setItem('upn', params.upn);
    sessionStorage.setItem('username', params.username);
    sessionStorage.setItem('userFullName', params.fullName);
    sessionStorage.setItem('FullName', params.fullName);
    sessionStorage.setItem('emailID', params.email);
    sessionStorage.setItem('fullUPN', params.email);
    sessionStorage.setItem('userId', params.userId);
    sessionStorage.setItem('Upn', params.email);
    sessionStorage.setItem('GUID', params.guid);
    sessionStorage.setItem('UserDefaultGroup', params.groupName);
    sessionStorage.setItem('JobTitle', params.jobTitle);
    sessionStorage.setItem('tokenTime', new Date().toString());

    localStorage.setItem('DIApiToken', params.apiToken);
    this.tokenService.setToken(params.apiToken);

    environment.userId = params.username;
    environment.userFullName = params.fullName;
    environment.isAdmin = 'true';

    const nameParts = params.fullName.split(' ').filter(Boolean);
    this.currentUserSource.next({
      token: params.apiToken,
      userFullName: params.fullName,
      FirstName: nameParts[0] ?? params.fullName,
      LastName: nameParts.slice(1).join(' ') || '',
      displayName: params.fullName,
      UPN: params.email,
      email: params.email,
      Image: '',
      employeeId: params.userId,
      isAdmin: true,
    } as UserDetails);
  }

  /** Apply session after Azure AD (MSAL) sign-in. */
  applyMsalAccount(account: AccountInfo, msalAccessToken?: string): void {
    const email = account.username?.trim() || 'dev.user@local.dev';
    const username = email.includes('@') ? email.split('@')[0] : email;
    const displayName = account.name?.trim() || username;
    const apiToken = this.resolveAppApiToken(msalAccessToken);

    this.bootstrapAppSession({
      upn: username,
      username,
      fullName: displayName,
      email,
      userId: username,
      guid: sessionStorage.getItem('GUID') || crypto.randomUUID(),
      groupName: 'Product Development',
      jobTitle: 'Developer',
      apiToken,
      // Local environment to be deleted later -AY
      isGuest: !environment.production,
    });
  }

  hasAppSession(): boolean {
    return !!sessionStorage.getItem('upn')?.trim();
  }

  /** Dev-only bypass: skip Azure login and enter the app as a guest user. */
  guestLogin(): void {
    this.bootstrapAppSession({
      upn: 'guest',
      username: 'guest',
      fullName: 'Guest User',
      email: 'guest@local.dev',
      userId: 'guest',
      guid: '00000000-0000-0000-0000-000000000001',
      groupName: 'Product Development',
      jobTitle: 'Guest',
      apiToken: GUEST_TOKEN,
      isGuest: true,
    });
  }

  authenticate() {
    const httpHeader = {
      headers: new HttpHeaders({
        ClientId: environment.appId,
        ClientSecret: environment.clientKey,
        'x-tpdi-api-version': '1.0',
        'userId': sessionStorage.getItem('upn'),
      }),
    };
    return this._http.get(
      this.baseUrl + 'api/Account/authenticate',
      httpHeader
    );
  }
  setCurrentUser(): Promise<boolean> {
    return new Promise((res, rej) => {
    this._http
      .get<any>(environment.graphApiEndpoint + '?$select=employeeId')
      .subscribe({
        next: (response) => {
          sessionStorage.setItem('userId', response.employeeId);
        },
        error: (error) => {
          console.log(error);
        },
      });
    this._http.get<any>(environment.graphApiEndpoint).subscribe({
      next: (response) => {
        if (response) {
          sessionStorage.setItem('username', response.displayName);
          sessionStorage.setItem('userFullName', response.displayName);
          sessionStorage.setItem('emailID', response.mail);
          sessionStorage.setItem(
            'userId',
            response.userPrincipalName?.split('@')[0]
          );
          sessionStorage.setItem(
            'upn',
            response.userPrincipalName?.split('@')[0]
          );
          sessionStorage.setItem('fullUPN',response.userPrincipalName);
          sessionStorage.setItem('JobTitle', response?.jobTitle);
          res(true);
        }else res(false);
      },
      error: (error) => {
        res(false);
        console.log(error);
      },
    });
  });
  }
  onUserLogin() {
    this.authenticate().subscribe((res: any) => {
      if (res.responseCode == 200 && res.responseMessage[0] == 'Success') {

        console.log(res)


        const token = res.result.token;
        //this.setDefaultUserGroup(token);
        localStorage.setItem('DIAPItokDIApiTokenen', token);
        const time = new Date();      
        sessionStorage.setItem('tokenTime', time.toString());
        return true;
      }else return false;
    });
  }
  getToken(data: any) {
    let url = this.baseUrl + 'api/Account/GetToken';

    let objHttpHeaders = new HttpHeaders();
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-version', '1.0');
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-sg', '1.0');
    return this._http.post(url, data, { headers: objHttpHeaders });
  }

  getUserGroup(token: string) {
    const httpHeader2 = {
      headers: new HttpHeaders({
        Authorization: `Bearer ${token}`,
        'x-tpdi-api-version': '2.0',
        'Content-Type': 'application/json',
      }),
    };
    return this._http.get<APIResponse<SecurityGroup[]>>(
      `${
        this.baseUrl
      }api/ProcessConfiguration/SecurityGroups?loginId=${sessionStorage.getItem(
        'upn'
      )}`,
      httpHeader2
    );
  }

  userLogin(data: any) {
    let url = this.baseUrl + 'api/Account/GetToken';

    return this._http.post(url, data, undefined);
  }

  userLogout(LoginId:string){
    let url = this.baseUrl + 'api/ProcessConfiguration/UpdateLogin';

    const httpHeader2 = {
      headers: new HttpHeaders({
        Authorization: `Bearer ${localStorage.getItem('DIApiToken')}`,
        'x-tpdi-api-version': '3.0',
        'Content-Type': 'application/json',
      }),
    };

    return this._http.post(url, {'LoginId': LoginId}, httpHeader2).subscribe();
  }

  loadCurrentUser(token: string | null) {
    if (token === null) {
      this.currentUserSource.next(null);
      return of(null);
    }

    let headers = new HttpHeaders();
    headers = headers.set('Authorization', `Bearer ${token}`);
    headers = headers.set('x-tpdi-api-version', '1.0');
    //this.NTIDencrypt = this.encrypt(environment.userId);

    let lobData: any = {
      ntid: environment.userId,
    };
    sessionStorage.getItem('Upn');
    return this._http
      .post(this.baseUrl + 'api/Account/GetUserDetail', lobData, { headers })
      .pipe(
        map((user: any) => {
          if (user) {
            const userDetail: UserDetails = {
              userFullName: sessionStorage.getItem('FullName'),
              email: sessionStorage.getItem('Upn'),
              isAdmin: user.result.userDetail.isAdmin,
            } as UserDetails;

            environment.userId = sessionStorage.getItem('username');
            environment.userFullName = sessionStorage.getItem('FullName');
            environment.isAdmin = userDetail.isAdmin ? 'true' : 'false';
            this.currentUserSource.next(userDetail);
            return user.result;
          } else {
            return null;
          }
        })
      );
  }
}
