import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest,
  HttpResponse,
} from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { isGuestSession } from '../guest/guest.util';
import { resolveGuestMock } from '../guest/guest-mock.data';

// #region Sharepoint Workspace - AY
// Mocks API responses for guest login during local dev
// #endregion
@Injectable()
export class GuestInterceptor implements HttpInterceptor {
  // #region Sharepoint Workspace - AY
  /** SharePoint plugin APIs always hit backend/DB (never guest mocks). */
  private isIngestionConsoleApiRequest(url: string): boolean {
    return /\/api\/applications(\/|$|\?)/i.test(url)
      || /\/api\/workspace(\/|$|\?)/i.test(url)
      || /\/api\/auth\//i.test(url);
  }
  // #endregion

  // #region Sharepoint Workspace - AY
  private isProcessConfigurationApiRequest(url: string): boolean {
    return /\/api\/ProcessConfiguration\//i.test(url);
  }
  // #endregion

  intercept(
    req: HttpRequest<unknown>,
    next: HttpHandler
  ): Observable<HttpEvent<unknown>> {
    if (!isGuestSession()) {
      return next.handle(req);
    }

    // #region Sharepoint Workspace - AY
    if (this.isIngestionConsoleApiRequest(req.url)) {
      return next.handle(req);
    }
    // #endregion

    const mock = resolveGuestMock(req);

    if (mock === 'GRAPH_PHOTO') {
      return throwError(
        () =>
          new HttpErrorResponse({
            status: 404,
            statusText: 'Not Found',
            url: req.url,
          })
      );
    }

    if (mock !== null) {
      return of(
        new HttpResponse({
          status: 200,
          body: mock,
        })
      );
    }

    // #region Sharepoint Workspace - AY
    if (this.isProcessConfigurationApiRequest(req.url)) {
      return next.handle(req);
    }
    // #endregion

    return next.handle(req);
  }
}
