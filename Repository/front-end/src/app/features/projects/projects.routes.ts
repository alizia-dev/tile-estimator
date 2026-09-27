import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/auth.guard';

export const projectRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./project-list.component').then((m) => m.ProjectListComponent),
  },
  {
    path: 'new',
    canActivate: [permissionGuard('project.create')],
    loadComponent: () => import('./project-edit.component').then((m) => m.ProjectEditComponent),
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard('project.update')],
    loadComponent: () => import('./project-edit.component').then((m) => m.ProjectEditComponent),
  },
  {
    path: ':id',
    loadComponent: () => import('./project-detail.component').then((m) => m.ProjectDetailComponent),
  },
];
