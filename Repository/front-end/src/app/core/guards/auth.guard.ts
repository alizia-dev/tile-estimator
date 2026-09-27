import { inject } from '@angular/core';
import { Router, type CanActivateFn } from '@angular/router';
import { catchError, map, of } from 'rxjs';

import { AuthService } from '../auth/auth.service';

/**
 * Blocks a route until there is a session. If a token is present but the profile has not been
 * loaded yet (a page refresh, for example), the profile is fetched first so permission guards
 * further down the chain have something to check.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/auth/login'], {
      queryParams: { returnUrl: state.url },
    });
  }

  if (auth.user()) {
    return true;
  }

  return auth.loadCurrentUser().pipe(
    map(() => true),
    catchError(() => {
      auth.clearSession();
      return of(router.createUrlTree(['/auth/login'], { queryParams: { returnUrl: state.url } }));
    }),
  );
};

/** Keeps a signed-in user away from the login and register pages. */
export const anonymousGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.isAuthenticated() ? router.createUrlTree(['/dashboard']) : true;
};

/**
 * Requires one of the given permissions.
 *
 * This only decides what the app shows. The server enforces the same permission on every
 * endpoint, so bypassing this guard gains nothing but a broken-looking page.
 */
export function permissionGuard(...permissions: string[]): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);

    return auth.hasAny(...permissions) ? true : router.createUrlTree(['/dashboard']);
  };
}
