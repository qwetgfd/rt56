import { Injectable } from '@angular/core';
import { MsalService } from '@azure/msal-angular';
import { Observable, of, throwError } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';
import { isGuestSession } from '../guest/guest.util';
const TOKEN_KEY = 'access_token';
const EXPIRY_KEY = 'token_expiry';

@Injectable({
  providedIn: 'root'
})
export class GraphApiTokenService {

  constructor(private msalService : MsalService) { }

    /**
   * Get the access token from localStorage or generate a new one if expired.
   * @param scopes The scopes required for the token.
   * @returns An Observable of the access token.
   */
  getAccessToken(scopes: string[]) : Observable<string>{
    if (isGuestSession()) {
      return of('guest-graph-token');
    }

    const token = localStorage.getItem(TOKEN_KEY);
    const expiry = localStorage.getItem(EXPIRY_KEY);

    if(token && expiry && new Date().getTime() < +expiry){
      return of(token);
    }

    // Token is expired or not available, generate a new one
    return this.generateNewToken(scopes);
  }

  /**
   * Generate a new token and save it to localStorage.
   * @param scopes The scopes required for the token.
   * @returns An Observable of the new access token.
   */
  private generateNewToken(scopes: string[]): Observable<string> {
    const account = this.msalService.instance.getAllAccounts()[0];
    if (!account) {
      return throwError(() => new Error('No account found. User might not be logged in.'));
    }

    return this.msalService.acquireTokenSilent({ account, scopes }).pipe(
      tap((response) => {
        const expiresIn = response.expiresOn
          ? Math.floor((response.expiresOn.getTime() - new Date().getTime()) / 1000)
          : 3600; // Default to 1 hour if not provided
        this.saveTokenToStorage(response.accessToken, expiresIn);
      }),
      map((response) => response.accessToken),
      catchError((error) => {
        console.warn('Silent token acquisition failed, falling back to popup:', error);
        return this.msalService.acquireTokenPopup({ scopes }).pipe(
          tap((popupResponse) => {
            const expiresIn = popupResponse.expiresOn
              ? Math.floor((popupResponse.expiresOn.getTime() - new Date().getTime()) / 1000)
              : 3600;
            this.saveTokenToStorage(popupResponse.accessToken, expiresIn);
          }),
          map((popupResponse) => popupResponse.accessToken)
        );
      })
    );
  }

  /**
   * Save the token and its expiry time to localStorage.
   * @param token The access token.
   * @param expiresIn The expiry time in seconds.
   */
  private saveTokenToStorage(token: string, expiresIn: number): void {
    //debugger;
    const expiry = new Date().getTime() + expiresIn * 1000; // Convert seconds to milliseconds
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(EXPIRY_KEY, expiry.toString());
  }
}
