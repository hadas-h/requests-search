import { HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';

/**
 * Attaches `Authorization: Bearer <token>` to API requests when a token is present. Requests to
 * other origins (e.g. i18n assets) and unauthenticated requests are passed through untouched.
 */
@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  private readonly auth = inject(AuthService);

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const token = this.auth.token();
    if (!token || !req.url.startsWith(environment.apiBaseUrl)) {
      return next.handle(req);
    }

    return next.handle(
      req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }),
    );
  }
}
