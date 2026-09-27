import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import type { AuthResponse, CurrentUser } from '../models/api.models';

function buildUser(overrides: Partial<CurrentUser> = {}): CurrentUser {
  return {
    id: 'user-1',
    email: 'sam@example.com',
    firstName: 'Sam',
    lastName: 'Torres',
    emailConfirmed: true,
    activeOrganizationId: 'org-1',
    activeOrganizationName: 'Torres Tile Co',
    roleName: 'Owner',
    permissions: ['estimate.read', 'estimate.update', 'quote.send'],
    organizations: [
      { organizationId: 'org-1', organizationName: 'Torres Tile Co', roleName: 'Owner' },
    ],
    ...overrides,
  };
}

function buildAuthResponse(user = buildUser()): AuthResponse {
  return {
    accessToken: 'access-token',
    accessTokenExpiresAt: new Date(Date.now() + 900_000).toISOString(),
    refreshToken: 'refresh-token',
    refreshTokenExpiresAt: new Date(Date.now() + 86_400_000).toISOString(),
    user,
  };
}

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  it('starts signed out', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.user()).toBeNull();
  });

  it('stores the session after a successful sign-in', () => {
    service.login({ email: 'sam@example.com', password: 'Str0ngPassw0rd!' }).subscribe();

    http.expectOne(`${environment.apiBaseUrl}/auth/login`).flush(buildAuthResponse());

    expect(service.isAuthenticated()).toBe(true);
    expect(service.getAccessToken()).toBe('access-token');
    expect(service.getRefreshToken()).toBe('refresh-token');
    expect(service.activeOrganizationId()).toBe('org-1');
  });

  it('exposes the permissions the server returned', () => {
    service.login({ email: 'sam@example.com', password: 'x' }).subscribe();
    http.expectOne(`${environment.apiBaseUrl}/auth/login`).flush(buildAuthResponse());

    expect(service.has('estimate.update')).toBe(true);
    expect(service.has('catalog.manage')).toBe(false);
    expect(service.hasAny('catalog.manage', 'quote.send')).toBe(true);
    expect(service.hasAny('audit.read', 'users.manage')).toBe(false);
  });

  it('builds a display name and initials for the shell', () => {
    service.login({ email: 'sam@example.com', password: 'x' }).subscribe();
    http.expectOne(`${environment.apiBaseUrl}/auth/login`).flush(buildAuthResponse());

    expect(service.displayName()).toBe('Sam Torres');
    expect(service.initials()).toBe('ST');
  });

  it('clears every trace of the session when it is dropped', () => {
    service.login({ email: 'sam@example.com', password: 'x' }).subscribe();
    http.expectOne(`${environment.apiBaseUrl}/auth/login`).flush(buildAuthResponse());

    service.clearSession();

    expect(service.isAuthenticated()).toBe(false);
    expect(service.user()).toBeNull();
    expect(service.getAccessToken()).toBeNull();
    expect(service.getRefreshToken()).toBeNull();
    expect(localStorage.getItem('te.accessToken')).toBeNull();
  });

  it('sends the stored refresh token when refreshing', () => {
    service.login({ email: 'sam@example.com', password: 'x' }).subscribe();
    http.expectOne(`${environment.apiBaseUrl}/auth/login`).flush(buildAuthResponse());

    service.refresh().subscribe();

    const request = http.expectOne(`${environment.apiBaseUrl}/auth/refresh`);
    expect(request.request.body).toMatchObject({
      refreshToken: 'refresh-token',
      organizationId: 'org-1',
    });

    // A refresh rotates the token, so the new one must replace the old.
    request.flush(
      buildAuthResponse(buildUser()) as AuthResponse & { refreshToken: string },
    );
  });

  it('registration signs the new owner straight in', () => {
    service
      .register({
        firstName: 'Sam',
        lastName: 'Torres',
        email: 'sam@example.com',
        password: 'Str0ngPassw0rd!',
        confirmPassword: 'Str0ngPassw0rd!',
        companyName: 'Torres Tile Co',
        acceptTerms: true,
      })
      .subscribe();

    http.expectOne(`${environment.apiBaseUrl}/auth/register`).flush(buildAuthResponse());

    expect(service.isAuthenticated()).toBe(true);
    expect(service.user()?.roleName).toBe('Owner');
  });

  it('has no permissions at all when signed out', () => {
    expect(service.has('estimate.read')).toBe(false);
    expect(service.permissions().size).toBe(0);
  });
});
