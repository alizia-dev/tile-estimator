import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/auth.guard';

export const customerRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./customer-list.component').then((m) => m.CustomerListComponent),
  },
  {
    path: 'new',
    canActivate: [permissionGuard('customer.manage')],
    loadComponent: () => import('./customer-edit.component').then((m) => m.CustomerEditComponent),
  },
  {
    path: ':id',
    canActivate: [permissionGuard('customer.manage')],
    loadComponent: () => import('./customer-edit.component').then((m) => m.CustomerEditComponent),
  },
];
