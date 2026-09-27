import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/auth.guard';

export const settingsRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./settings.component').then((m) => m.SettingsComponent),
  },
  {
    path: 'profile',
    loadComponent: () => import('./profile.component').then((m) => m.ProfileComponent),
  },
  {
    path: 'audit',
    canActivate: [permissionGuard('audit.read')],
    loadComponent: () => import('./audit-log.component').then((m) => m.AuditLogComponent),
  },
];
