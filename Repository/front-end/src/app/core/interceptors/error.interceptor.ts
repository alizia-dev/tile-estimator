import { HttpErrorResponse, type HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, throwError } from 'rxjs';

import type { ProblemDetails } from '../models/api.models';

/**
 * Turns an RFC 7807 ProblemDetails response into a message a contractor can act on, and shows
 * it once. Validation errors (400) are re-thrown untouched so the form that caused them can
 * attach them to the right fields instead of popping a toast.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const snackBar = inject(MatSnackBar);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        const problem = extractProblem(error);

        // 400 is handled by the form; 401 by the auth interceptor.
        if (error.status !== 400 && error.status !== 401) {
          snackBar.open(describe(error, problem), 'Dismiss', {
            duration: 7000,
            panelClass: ['te-snackbar-error'],
          });
        }
      }

      return throwError(() => error);
    }),
  );
};

function extractProblem(error: HttpErrorResponse): ProblemDetails | null {
  const body: unknown = error.error;
  return body && typeof body === 'object' ? (body as ProblemDetails) : null;
}

function describe(error: HttpErrorResponse, problem: ProblemDetails | null): string {
  if (error.status === 0) {
    return 'Cannot reach the server. Check your connection and try again.';
  }

  if (error.status === 403) {
    return problem?.detail ?? 'You do not have permission to do that.';
  }

  if (error.status === 404) {
    return problem?.detail ?? 'That item could not be found.';
  }

  if (error.status === 409) {
    return problem?.detail ?? 'Someone else changed this while you were working. Reload and try again.';
  }

  if (error.status === 429) {
    return 'Too many requests. Wait a moment and try again.';
  }

  const detail = problem?.detail ?? problem?.title;

  // A trace id makes a 500 actually reportable to support.
  return problem?.traceId && error.status >= 500
    ? `${detail ?? 'Something went wrong.'} (reference ${problem.traceId})`
    : (detail ?? 'Something went wrong.');
}

/** Pulls per-field validation errors out of a 400 so a form can display them inline. */
export function fieldErrors(error: unknown): Record<string, string[]> {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) {
    return {};
  }

  const problem = error.error as ProblemDetails | undefined;
  const errors = problem?.errors ?? {};

  // The server camel-cases some keys and PascalCases others depending on the source; normalise.
  const normalised: Record<string, string[]> = {};
  for (const [key, messages] of Object.entries(errors)) {
    normalised[key.charAt(0).toLowerCase() + key.slice(1)] = messages;
  }
  return normalised;
}

/** The single human-readable message for an error, for use outside the interceptor. */
export function errorMessage(error: unknown, fallback = 'Something went wrong.'): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }

  const problem = extractProblem(error);
  const fields = fieldErrors(error);
  const firstField = Object.values(fields)[0]?.[0];

  return firstField ?? problem?.detail ?? problem?.title ?? fallback;
}
