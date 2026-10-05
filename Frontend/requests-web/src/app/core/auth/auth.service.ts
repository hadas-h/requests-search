import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthResult, AuthUser } from './auth.models';

const TOKEN_KEY = 'auth.token';
const USER_KEY = 'auth.user';

/**
 * Signal-based authentication state. Holds the JWT and the signed-in user, persists them to
 * localStorage so a refresh keeps the session, and exposes login/register/logout. The token is
 * attached to API requests by the auth interceptor.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api/auth`;

  private readonly _token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  private readonly _user = signal<AuthUser | null>(readStoredUser());

  readonly token = this._token.asReadonly();
  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => this._token() !== null);

  register(username: string, password: string): Observable<AuthResult> {
    return this.http
      .post<AuthResult>(`${this.base}/register`, { username, password })
      .pipe(tap((result) => this.persist(result)));
  }

  login(username: string, password: string): Observable<AuthResult> {
    return this.http
      .post<AuthResult>(`${this.base}/login`, { username, password })
      .pipe(tap((result) => this.persist(result)));
  }

  logout(): void {
    this._token.set(null);
    this._user.set(null);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }

  private persist(result: AuthResult): void {
    this._token.set(result.token);
    this._user.set(result.user);
    localStorage.setItem(TOKEN_KEY, result.token);
    localStorage.setItem(USER_KEY, JSON.stringify(result.user));
  }
}

function readStoredUser(): AuthUser | null {
  const raw = localStorage.getItem(USER_KEY);
  if (!raw) {
    return null;
  }
  try {
    return JSON.parse(raw) as AuthUser;
  } catch {
    return null;
  }
}
