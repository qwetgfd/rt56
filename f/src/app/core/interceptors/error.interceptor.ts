import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../services/auth.service';
import { LoginService } from '../services/login.service';
import { TokenService } from '../services/token.service';

// export const errorInterceptor: HttpInterceptorFn = (req, next) => {
//   return next(req);
// };

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {

  private isHandling401: boolean = false;
  private isRefreshing = false;
  private refreshSubject = new BehaviorSubject<string | null>(null);
  constructor(
    private router: Router,
    private toastr: ToastrService,
    private authservice: AuthService,
    private loginService: LoginService,
    private tokenService: TokenService
  ) { }

  // #region Sharepoint Workspace - AY
  private isIngestionConsoleApiRequest(url: string): boolean {
    return /\/api\/applications(\/|$|\?)/i.test(url)
      || /\/api\/workspace(\/|$|\?)/i.test(url)
      || /\/api\/auth\//i.test(url);
  }

  intercept(
    request: HttpRequest<unknown>,
    next: HttpHandler
  ): Observable<HttpEvent<unknown>> {
    if (
      request.url.startsWith('https://graph.microsoft.com/')
      || request.url.includes('/api/Account/authenticate')
      || this.isIngestionConsoleApiRequest(request.url)
    ) {
      return next.handle(request);
    }
    //wait for the token to be ready before handling the request
    return this.tokenService.tokenReady$.pipe(
      filter(token => token !== null),
      take(1), // Ensure we only take the first emitted token
      switchMap(token => {
        const authReq = request.clone({
          setHeaders: { Authorization: `Bearer ${token}` }
        });
        return next.handle(authReq).pipe(
          catchError((error: HttpErrorResponse) => {

            // ------------------------
            // Handle 400 errors 
            // ------------------------
            if (error.status === 400) {
              if (error.error.errors) {
                throw error.error;
              } else {
                this.toastr.error(error.error.message, error.status.toString());
              }
            }

            // ------------------------
            // Handle 401 errors 
            // ------------------------
            if (error) {
              if (error.status === 401) {
                if (!this.isHandling401) { //prevent loops
                  this.isHandling401 = true;
                  try {


                    // ------------------------
                    //  CASE 1: TOKEN REVOKED
                    // ------------------------

                    if (error.headers.get('X-Token-Revoked') === 'true') {
                      // sessionStorage.clear();
                      // this.toastr.info(
                      //   'Already logged in! You have an active session on another device...'
                      // );
                      // this.authservice.logout();
                      // return throwError(() => new Error('Session revoked'));
                      const newToken = this.tokenService.getToken();// localStorage.getItem('DIApiToken');
                      if (newToken) {
                        //retry the failed request with the new token
                        const retryReq = request.clone({
                          setHeaders: { Authorization: `Bearer ${newToken}` }
                        });
                        return next.handle(retryReq);
                      }
                    } 
                    // ------------------------
                    //  CASE 2: TOKEN EXPIRED
                    // ------------------------
                    else {
                      //this.router.navigate(['login']);
                      const tokenTime = new Date(sessionStorage.getItem('tokenTime') || '');
                      const currentTime = new Date();
                      const timeDiff = currentTime.getTime() - tokenTime.getTime();
                      const minutes = timeDiff / (1000 * 60);
                      if (minutes > 120) {
                        sessionStorage.clear();
                        this.toastr.info(
                          'You have been logged out. Redirecting to Login.'
                        );
                        this.authservice.login();
                      } else {
                        const newToken = this.tokenService.getToken();
                        if (newToken) {
                          //retry the failed request with the new token
                          const retryReq = request.clone({
                            setHeaders: { Authorization: `Bearer ${newToken}` }
                          });
                          return next.handle(retryReq);
                        }
                      }
                    }
                  } finally {
                    // Release the flag after a short window so we don’t spam
                    setTimeout(() => (this.isHandling401 = false), 3000);
                  }
                }

                // sessionStorage.clear();
                // this.toastr.error('You have been logged out. Redirecting to Login.')
                // this.authservice.login();
              }

              // ------------------------
              // 500 Handling
              // ------------------------
              if (error.status === 500) {
                this.router.navigate(['/SomethingWentWrong']);
              }
            }
            return throwError(() => new Error(error.message));
          })
        );
      })
    );
  }

}
