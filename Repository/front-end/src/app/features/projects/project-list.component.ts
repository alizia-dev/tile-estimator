import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, type PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';

import { ProjectApi } from '../../core/services/api.service';
import type { Project } from '../../core/models/api.models';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MoneyPipe, ShortDatePipe, StatusClassPipe, StatusLabelPipe } from '../../shared/pipes';

const PROJECT_STATUSES = [
  'Draft', 'Estimating', 'EstimateReady', 'Quoted', 'Accepted',
  'Scheduled', 'InProgress', 'Completed', 'Cancelled', 'Closed',
] as const;

@Component({
  selector: 'te-project-list',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    HasPermissionDirective,
    MoneyPipe,
    ShortDatePipe,
    StatusClassPipe,
    StatusLabelPipe,
  ],
  template: `
    <div class="te-page">
      <header class="te-page__header">
        <div>
          <h1 class="te-page__title">Projects</h1>
          <p class="te-page__subtitle">{{ total() }} in your organization</p>
        </div>
        <div class="te-page__actions">
          <a mat-flat-button color="primary" routerLink="new" *teHasPermission="'project.create'">
            <mat-icon>add</mat-icon>
            New project
          </a>
        </div>
      </header>

      <div class="te-card" style="margin-bottom: 1rem">
        <div class="te-form-grid te-form-grid--narrow">
          <mat-form-field appearance="outline">
            <mat-label>Search</mat-label>
            <input matInput [formControl]="search" placeholder="Project name or number" />
            <mat-icon matSuffix>search</mat-icon>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Status</mat-label>
            <mat-select [formControl]="status">
              <mat-option [value]="''">All</mat-option>
              @for (s of statuses; track s) {
                <mat-option [value]="s">{{ s | teStatusLabel }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>
      </div>

      @if (loading()) {
        <div style="display: flex; justify-content: center; padding: 3rem">
          <mat-spinner diameter="36" />
        </div>
      } @else if (projects().length === 0) {
        <div class="te-card te-empty-state">
          <mat-icon class="te-empty-state__icon">folder_open</mat-icon>
          <p class="te-empty-state__title">No projects found</p>
          <p class="te-empty-state__text">
            A project holds the rooms and surfaces you take off, then the estimate and quote.
          </p>
          <a mat-flat-button color="primary" routerLink="new" *teHasPermission="'project.create'">
            Start a project
          </a>
        </div>
      } @else {
        <div class="te-table-wrapper">
          <table mat-table [dataSource]="projects()" class="te-table">
            <ng-container matColumnDef="projectNumber">
              <th mat-header-cell *matHeaderCellDef>Number</th>
              <td mat-cell *matCellDef="let project">{{ project.projectNumber }}</td>
            </ng-container>

            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Project</th>
              <td mat-cell *matCellDef="let project">
                <a [routerLink]="[project.id]" style="font-weight: 600">{{ project.name }}</a>
                <div class="te-muted" style="font-size: 0.75rem">
                  {{ project.roomCount }} room{{ project.roomCount === 1 ? '' : 's' }}
                </div>
              </td>
            </ng-container>

            <ng-container matColumnDef="customer">
              <th mat-header-cell *matHeaderCellDef>Customer</th>
              <td mat-cell *matCellDef="let project">{{ project.customerName }}</td>
            </ng-container>

            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let project">
                <span class="te-status" [class]="project.status | teStatusClass">
                  {{ project.status | teStatusLabel }}
                </span>
              </td>
            </ng-container>

            <ng-container matColumnDef="estimate">
              <th mat-header-cell *matHeaderCellDef class="te-numeric">Latest estimate</th>
              <td mat-cell *matCellDef="let project" class="te-numeric">
                @if (project.latestEstimateTotal !== null) {
                  <span class="te-money" style="font-weight: 600">
                    {{ project.latestEstimateTotal | teMoney }}
                  </span>
                  <div class="te-muted" style="font-size: 0.75rem">
                    {{ project.latestEstimateNumber }}
                  </div>
                } @else {
                  <span class="te-muted">—</span>
                }
              </td>
            </ng-container>

            <ng-container matColumnDef="createdAt">
              <th mat-header-cell *matHeaderCellDef>Created</th>
              <td mat-cell *matCellDef="let project">{{ project.createdAt | teDate }}</td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        </div>

        <mat-paginator
          [length]="total()"
          [pageSize]="pageSize()"
          [pageIndex]="page() - 1"
          [pageSizeOptions]="[10, 25, 50, 100]"
          (page)="onPage($event)"
        />
      }
    </div>
  `,
})
export class ProjectListComponent {
  private readonly api = inject(ProjectApi);

  readonly statuses = PROJECT_STATUSES;
  readonly columns = ['projectNumber', 'name', 'customer', 'status', 'estimate', 'createdAt'];

  readonly projects = signal<Project[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly loading = signal(true);

  readonly search = new FormControl('', { nonNullable: true });
  readonly status = new FormControl('', { nonNullable: true });

  constructor() {
    this.search.valueChanges.pipe(debounceTime(300), distinctUntilChanged()).subscribe(() => {
      this.page.set(1);
      this.load();
    });

    this.status.valueChanges.subscribe(() => {
      this.page.set(1);
      this.load();
    });

    this.load();
  }

  load(): void {
    this.loading.set(true);

    this.api
      .list({
        page: this.page(),
        pageSize: this.pageSize(),
        search: this.search.value || undefined,
        status: this.status.value || undefined,
      })
      .subscribe({
        next: (result) => {
          this.projects.set(result.items);
          this.total.set(result.totalCount);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  onPage(event: PageEvent): void {
    this.page.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
    this.load();
  }
}
