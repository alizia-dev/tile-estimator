/** Local development configuration. `environment.prod.ts` replaces this at build time. */
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5199/api',
} as const;
