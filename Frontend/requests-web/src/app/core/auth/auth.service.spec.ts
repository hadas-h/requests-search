import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { environment } from '../../../environments/environment';
import { AuthResult } from './auth.models';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  const result: AuthResult = {
    token: 'jwt-token',
    user: { id: 7, username: 'admin', isAdministrator: true },
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('stores token and user on login', () => {
    service.login('admin', 'Admin123!').subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/login`);
    expect(req.request.method).toBe('POST');
    req.flush(result);

    expect(service.token()).toBe('jwt-token');
    expect(service.user()).toEqual(result.user);
    expect(service.isAuthenticated()).toBeTrue();
    expect(localStorage.getItem('auth.token')).toBe('jwt-token');
  });

  it('clears token and user on logout', () => {
    service.login('admin', 'Admin123!').subscribe();
    httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/login`).flush(result);

    service.logout();

    expect(service.token()).toBeNull();
    expect(service.user()).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
    expect(localStorage.getItem('auth.token')).toBeNull();
  });
});
