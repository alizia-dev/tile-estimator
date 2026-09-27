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

import { EstimateApi } from '../../core/services/api.service';
import type { EstimateSummary } from '../../core/models/api.models';
import { MoneyPipe, ShortDatePipe, StatusClassPipe, StatusLabelPipe } from '../../shared/pipes';

@Component({
  selector: 'te-estimate-list',
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
    MoneyPipe,
    ShortDatePipe,
    StatusClassPipe,
    StatusLabelPipe,
  ],
  template: `
    <div class="te-page">
      <header class="te-page__header">
        <div>
          <h1 class="te-page__title">Estimates</h1>
          <p class="te-page__subtitle">{{ total() }} in your organization</p>
        </div>
      </header>

      <div class="te-card" style="margin-bottom: 1rem">
        <div class="te-form-grid te-form-grid--narrow">
          <mat-form-field appearance="outline">
            <mat-label>Search</mat-label>
            <input matInput [formControl]="search" placeholder="Estimate number or title" />
            <mat-icon matSuffix>search</mat-icon>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Status</mat-label>
            <mat-select [formControl]="status">
              <mat-option [value]="''">All</mat-option>
              <mat-option value="Draft">Draft</mat-option>
              <mat-option value="InReview">In review</mat-option>
              <mat-option value="Finalized">Finalized</mat-option>
              <mat-option value="Superseded">Superseded</mat-option>
              <mat-option value="Cancelled">Cancelled</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
      </div>

      @if (loading()) {
        <div style="display: flex; justify-content: center; padding: 3rem">
          <mat-spinner diameter="36" />
        </div>
      } @else if (estimates().length === 0) {
        <div class="te-card te-empty-state">
          <mat-icon class="te-empty-state__icon">calculate</mat-icon>
          <p class="te-empty-state__title">No estimates found</p>
          <p class="te-empty-state__text">
            Estimates are created from a project, once it has rooms and surfaces.
          </p>
          <a mat-flat-button color="primary" routerLink="/projects">Go to projects</a>
        </div>
      } @else {
        <div class="te-table-wrapper">
          <table mat-table [dataSource]="estimates()" class="te-table">
            <ng-container matColumnDef="number">
              <th mat-header-cell *matHeaderCellDef>Estimate</th>
              <td mat-cell *matCellDef="let estimate">
                <a [routerLink]="[estimate.id]" style="font-weight: 600">
                  {{ estimate.displayNumber }}
                </a>
              </td>
            </ng-container>

            <ng-container matColumnDef="project">
              <th mat-header-cell *matHeaderCellDef>Project</th>
              <td mat-cell *matCellDef="let estimate">{{ estimate.projectName }}</td>
            </ng-container>

            <ng-container matColumnDef="customer">
              <th mat-header-cell *matHeaderCellDef>Customer</th>
              <td mat-cell *matCellDef="let estimate">{{ estimate.customerName }}</td>
            </ng-container>

            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let estimate">
                <span class="te-status" [class]="estimate.status | teStatusClass">
                  {{ estimate.status | teStatusLabel }}
                </span>
              </td>
            </ng-container>

            <ng-container matColumnDef="total">
              <th mat-header-cell *matHeaderCellDef class="te-numeric">Total</th>
              <td mat-cell *matCellDef="let estimate" class="te-numeric te-money" style="font-weight: 600">
                {{ estimate.grandTotal | teMoney: estimate.currency }}
              </td>
            </ng-container>

            <ng-container matColumnDef="createdAt">
              <th mat-header-cell *matHeaderCellDef>Created</th>
              <td mat-cell *matCellDef="let estimate">{{ estimate.createdAt | teDate }}</td>
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
export class EstimateListComponent {
  private readonly api = inject(EstimateApi);

  readonly columns = ['number', 'project', 'customer', 'status', 'total', 'createdAt'];

  readonly estimates = signal<EstimateSummary[]>([]);
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
          this.estimates.set(result.items);
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
