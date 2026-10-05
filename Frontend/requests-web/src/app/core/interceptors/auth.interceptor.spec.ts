import { TestBed } from '@angular/core/testing';
import {
  HTTP_INTERCEPTORS,
  HttpClient,
  provideHttpClient,
  withInterceptorsFromDi,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { environment } from '../../../environments/environment';
import { AuthResult } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';
import { AuthInterceptor } from './auth.interceptor';

describe('AuthInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: AuthService;

  const authResult: AuthResult = {
    token: 'jwt-token',
    user: { id: 1, username: 'admin', isAdministrator: true },
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
        { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  function signIn(): void {
    auth.login('admin', 'Admin123!').subscribe();
    httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/login`).flush(authResult);
  }

  it('attaches the Authorization header to API requests when a token is set', () => {
    signIn();

    http.get(`${environment.apiBaseUrl}/api/requests`).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/requests`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-token');
    req.flush({});
  });

  it('does not attach the Authorization header when no token is set', () => {
    http.get(`${environment.apiBaseUrl}/api/requests`).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/requests`);
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('does not attach the Authorization header to non-API requests', () => {
    signIn();

    http.get('assets/i18n/en.json').subscribe();

    const req = httpMock.expectOne('assets/i18n/en.json');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });
});
