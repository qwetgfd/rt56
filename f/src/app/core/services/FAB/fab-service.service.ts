// fab.service.ts
import { Injectable } from '@angular/core';
import { CampaignUserAccess } from '../../models/userDetails';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { APIResponse } from '../../models/apiResponse';
import { environment } from '../../../environments/environment';
import { BehaviorSubject, Observable, of, ReplaySubject } from 'rxjs';
import { catchError, map, shareReplay, tap } from 'rxjs/operators';

@Injectable({ providedIn: 'root' })
export class FabService {
  private baseUrl = environment.apiEndpoint;

  constructor(private _http: HttpClient) { }

  private _FABUserAccountInfo: CampaignUserAccess[] = [];
  private _userAccount$?: Observable<CampaignUserAccess[]>;


  private _isFabUserSubject = new BehaviorSubject<boolean>(false);
  readonly isFabUser$ = this._isFabUserSubject.asObservable();


  // Emits true once when FAB user is loaded (even if empty or failed gracefully)
  private _fabReady$ = new ReplaySubject<boolean>(1);
  readonly fabReady$ = this._fabReady$.asObservable();

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
    objHttpHeaders = objHttpHeaders.set('x-tpdi-api-sg', `${sessionStorage.getItem('GUID')}`);
    return objHttpHeaders;
  }

  /** Loads once and caches; also flips fabReady$ when done */
  loadUserAccount$(): Observable<CampaignUserAccess[]> {
    if (!this._userAccount$) {
      const encodedUpn = encodeURIComponent(sessionStorage.getItem('fullUPN') || '');
      const headers = this.setHttpsRequestHeaders('4.1');

      this._userAccount$ = this._http
        .get<APIResponse<CampaignUserAccess[]>>(
          `${this.baseUrl}api/ProcessConfiguration/GetCampaignUserAccessInfo?UPN=${encodedUpn}`,
          { headers }
        )
        .pipe(
          map((res) => (res && res.responseCode === 200 ? res.result ?? [] : [])),
          tap((list) => {
            this._FABUserAccountInfo = list;
            this._isFabUserSubject.next(list.length > 0); // update boolean
            this._fabReady$.next(true); // signal ready after load (success)
          }),
          catchError((err) => {
            console.error('loadUserAccount$ error', err);
            this._FABUserAccountInfo = [];
            this._isFabUserSubject.next(false);           // ensure false on error
            this._fabReady$.next(true); // still signal "ready" to release waiters
            return of([]);
          }),
          shareReplay({ bufferSize: 1, refCount: true })
        );
    }
    return this._userAccount$;
  }

  /** Synchronous getters (valid after fabReady$ emits) */
  get FABUserAccount(): CampaignUserAccess[] {
    return this._FABUserAccountInfo;
  }

  /** Reactive boolean if you prefer */
  get isFABUser$(): Observable<boolean> {
    return this.loadUserAccount$().pipe(
      map((list) => list.length > 0),
      shareReplay({ bufferSize: 1, refCount: true })
    );
  }


  // Synchronous boolean getter backed by BehaviorSubject
  get isFabUserValue(): boolean {
    return this._isFabUserSubject.value;
  }

}