import type { Routes } from '@angular/router';

import { anonymousGuard, authGuard, permissionGuard } from './core/guards/auth.guard';

/**
 * Every feature is lazy-loaded (SPEC 21), so the first paint only carries the shell and the
 * page being opened.
 *
 * The permission guards here decide what the app shows. The API enforces the same permissions
 * independently, so routing is never the thing keeping data safe.
 */
export const routes: Routes = [
  {
    path: 'auth',
    canActivate: [anonymousGuard],
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.authRoutes),
  },

  // The customer-facing quote page. No account, no token, no shell.
  {
    path: 'quote/:token',
    loadComponent: () =>
      import('./features/public-quote/public-quote.component').then((m) => m.PublicQuoteComponent),
  },

  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./shared/shell/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },

      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'customers',
        canActivate: [permissionGuard('customer.read')],
        loadChildren: () =>
          import('./features/customers/customers.routes').then((m) => m.customerRoutes),
      },
      {
        path: 'projects',
        canActivate: [permissionGuard('project.read')],
        loadChildren: () => import('./features/projects/projects.routes').then((m) => m.projectRoutes),
      },
      {
        path: 'estimates',
        canActivate: [permissionGuard('estimate.read')],
        loadChildren: () =>
          import('./features/estimates/estimates.routes').then((m) => m.estimateRoutes),
      },
      {
        path: 'quotes',
        canActivate: [permissionGuard('quote.read')],
        loadChildren: () => import('./features/quotes/quotes.routes').then((m) => m.quoteRoutes),
      },
      {
        path: 'catalog',
        canActivate: [permissionGuard('catalog.read')],
        loadChildren: () => import('./features/catalog/catalog.routes').then((m) => m.catalogRoutes),
      },
      {
        path: 'calculators',
        canActivate: [permissionGuard('estimate.read')],
        loadComponent: () =>
          import('./features/calculators/calculators.component').then((m) => m.CalculatorsComponent),
      },
      {
        path: 'reports',
        canActivate: [permissionGuard('reports.read')],
        loadComponent: () =>
          import('./features/reports/reports.component').then((m) => m.ReportsComponent),
      },
      {
        path: 'settings',
        canActivate: [permissionGuard('settings.read', 'users.read')],
        loadChildren: () => import('./features/settings/settings.routes').then((m) => m.settingsRoutes),
      },
    ],
  },

  { path: '**', redirectTo: '' },
];
