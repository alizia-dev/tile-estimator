import type { Routes } from '@angular/router';

export const estimateRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./estimate-list.component').then((m) => m.EstimateListComponent),
  },
  {
    path: ':id',
    loadComponent: () => import('./estimate-detail.component').then((m) => m.EstimateDetailComponent),
  },
];
