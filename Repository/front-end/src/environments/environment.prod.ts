/** Production configuration. The API origin is set at deploy time. */
export const environment = {
  production: true,
  apiBaseUrl: '/api',
} as const;
