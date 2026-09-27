import {
  HttpErrorResponse,
  type HttpEvent,
  type HttpHandlerFn,
  type HttpInterceptorFn,
  type HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, catchError, filter, switchMap, take, throwError } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { environment } from '../../../environments/environment';

/**
 * Tracks an in-flight refresh. While it holds null, a refresh is running and other requests
 * wait on it rather than each firing their own — otherwise a page that loads six panels at once
 * would rotate the refresh token six times and invalidate its own session.
 */
const refreshedToken$ = new BehaviorSubject<string | null>(null);
let refreshInProgress = false;

/** Endpoints that must never carry a token or trigger a refresh. */
const ANONYMOUS_PATHS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/forgot-password',
  '/auth/reset-password', '/auth/verify-email', '/auth/resend-verification', '/public/'];

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!request.url.startsWith(environment.apiBaseUrl) || isAnonymous(request.url)) {
    return next(request);
  }

  return next(withCredentials(request, auth)).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      if (!auth.getRefreshToken()) {
        auth.clearSession();
        void router.navigate(['/auth/login']);
        return throwError(() => error);
      }

      return handleUnauthorized(request, next, auth, router);
    }),
  );
};

function handleUnauthorized(
  request: HttpRequest<unknown>,
  next: HttpHandlerFn,
  auth: AuthService,
  router: Router,
): Observable<HttpEvent<unknown>> {
  if (refreshInProgress) {
    // Queue behind the refresh that is already running, then retry with the new token.
    return refreshedToken$.pipe(
      filter((token): token is string => token !== null),
      take(1),
      switchMap(() => next(withCredentials(request, auth))),
    );
  }

  refreshInProgress = true;
  refreshedToken$.next(null);

  return auth.refresh().pipe(
    switchMap((response) => {
      refreshInProgress = false;
      refreshedToken$.next(response.accessToken);
      return next(withCredentials(request, auth));
    }),
    catchError((refreshError: unknown) => {
      refreshInProgress = false;
      auth.clearSession();
      void router.navigate(['/auth/login']);
      return throwError(() => refreshError);
    }),
  );
}

function withCredentials(
  request: HttpRequest<unknown>,
  auth: AuthService,
): HttpRequest<unknown> {
  const token = auth.getAccessToken();
  const organizationId = auth.getOrganizationId();

  const headers: Record<string, string> = {};

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  // Only a hint for multi-organization users. The server re-verifies membership regardless.
  if (organizationId) {
    headers['X-Organization-Id'] = organizationId;
  }

  return Object.keys(headers).length > 0 ? request.clone({ setHeaders: headers }) : request;
}

function isAnonymous(url: string): boolean {
  return ANONYMOUS_PATHS.some((path) => url.includes(path));
}
