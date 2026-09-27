import type { Routes } from '@angular/router';

export const quoteRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./quote-list.component').then((m) => m.QuoteListComponent),
  },
  {
    path: ':id',
    loadComponent: () => import('./quote-detail.component').then((m) => m.QuoteDetailComponent),
  },
];
