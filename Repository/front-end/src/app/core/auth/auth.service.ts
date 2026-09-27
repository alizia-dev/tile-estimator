import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import type {
  AuthResponse,
  CurrentUser,
  LoginRequest,
  RegisterRequest,
} from '../models/api.models';

const ACCESS_TOKEN_KEY = 'te.accessToken';
const REFRESH_TOKEN_KEY = 'te.refreshToken';
const ORGANIZATION_KEY = 'te.organizationId';

/**
 * Holds the signed-in session.
 *
 * State is exposed as Signals so components read it synchronously without subscribing. Tokens
 * live in localStorage: it survives a refresh, which is what a contractor expects from a tool
 * they keep open all day. The access token is deliberately short-lived, and every permission
 * decision is re-checked on the server, so what is stored here only ever hides UI.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly baseUrl = `${environment.apiBaseUrl}/auth`;

  private readonly currentUser = signal<CurrentUser | null>(null);
  private readonly accessToken = signal<string | null>(readStorage(ACCESS_TOKEN_KEY));

  readonly user = this.currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this.accessToken() !== null);
  readonly permissions = computed(() => new Set(this.currentUser()?.permissions ?? []));
  readonly activeOrganizationId = computed(() => this.currentUser()?.activeOrganizationId ?? null);
  readonly organizations = computed(() => this.currentUser()?.organizations ?? []);

  readonly displayName = computed(() => {
    const user = this.currentUser();
    return user ? `${user.firstName} ${user.lastName}`.trim() : '';
  });

  readonly initials = computed(() => {
    const user = this.currentUser();
    if (!user) return '';
    return `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase();
  });

  /** True when the signed-in user holds this permission in the active organization. */
  has(permission: string): boolean {
    return this.permissions().has(permission);
  }

  hasAny(...permissions: string[]): boolean {
    const held = this.permissions();
    return permissions.some((p) => held.has(p));
  }

  getAccessToken(): string | null {
    return this.accessToken();
  }

  getRefreshToken(): string | null {
    return readStorage(REFRESH_TOKEN_KEY);
  }

  /** The organization the client is asking to act in. The server still verifies membership. */
  getOrganizationId(): string | null {
    return readStorage(ORGANIZATION_KEY);
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/register`, request)
      .pipe(tap((response) => this.applySession(response)));
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/login`, request)
      .pipe(tap((response) => this.applySession(response)));
  }

  /** Exchanges the refresh token for a fresh pair. Called by the HTTP interceptor on a 401. */
  refresh(): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/refresh`, {
        refreshToken: this.getRefreshToken() ?? '',
        organizationId: this.getOrganizationId(),
      })
      .pipe(tap((response) => this.applySession(response)));
  }

  loadCurrentUser(): Observable<CurrentUser> {
    return this.http
      .get<CurrentUser>(`${this.baseUrl}/me`)
      .pipe(tap((user) => this.currentUser.set(user)));
  }

  /** Switches the active organization and reloads the profile with its permissions. */
  switchOrganization(organizationId: string): Observable<CurrentUser> {
    writeStorage(ORGANIZATION_KEY, organizationId);
    return this.loadCurrentUser();
  }

  logout(): void {
    const refreshToken = this.getRefreshToken();

    // Clear locally first, so the session is gone even if the request fails.
    this.clearSession();

    this.http.post(`${this.baseUrl}/logout`, { refreshToken }).subscribe({
      next: () => void this.router.navigate(['/auth/login']),
      error: () => void this.router.navigate(['/auth/login']),
    });
  }

  /** Drops the session without calling the API. Used when a refresh has already failed. */
  clearSession(): void {
    this.accessToken.set(null);
    this.currentUser.set(null);
    removeStorage(ACCESS_TOKEN_KEY);
    removeStorage(REFRESH_TOKEN_KEY);
    removeStorage(ORGANIZATION_KEY);
  }

  private applySession(response: AuthResponse): void {
    this.accessToken.set(response.accessToken);
    this.currentUser.set(response.user);

    writeStorage(ACCESS_TOKEN_KEY, response.accessToken);
    writeStorage(REFRESH_TOKEN_KEY, response.refreshToken);

    if (response.user.activeOrganizationId) {
      writeStorage(ORGANIZATION_KEY, response.user.activeOrganizationId);
    }
  }
}

// Storage can throw in private-browsing modes, so every access is guarded.

function readStorage(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeStorage(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // A session that cannot be persisted still works for this tab.
  }
}

function removeStorage(key: string): void {
  try {
    localStorage.removeItem(key);
  } catch {
    // Nothing to do.
  }
}
