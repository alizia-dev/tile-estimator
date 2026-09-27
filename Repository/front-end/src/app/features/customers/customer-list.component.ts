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

import { CustomerApi } from '../../core/services/api.service';
import type { Customer } from '../../core/models/api.models';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { ShortDatePipe, StatusClassPipe, StatusLabelPipe } from '../../shared/pipes';

@Component({
  selector: 'te-customer-list',
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
    ShortDatePipe,
    StatusClassPipe,
    StatusLabelPipe,
  ],
  template: `
    <div class="te-page">
      <header class="te-page__header">
        <div>
          <h1 class="te-page__title">Customers</h1>
          <p class="te-page__subtitle">{{ total() }} in your organization</p>
        </div>
        <div class="te-page__actions">
          <a mat-flat-button color="primary" routerLink="new" *teHasPermission="'customer.manage'">
            <mat-icon>person_add</mat-icon>
            New customer
          </a>
        </div>
      </header>

      <div class="te-card" style="margin-bottom: 1rem">
        <div class="te-form-grid te-form-grid--narrow">
          <mat-form-field appearance="outline">
            <mat-label>Search</mat-label>
            <input matInput [formControl]="search" placeholder="Name, company, email or number" />
            <mat-icon matSuffix>search</mat-icon>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Status</mat-label>
            <mat-select [formControl]="status">
              <mat-option [value]="''">All</mat-option>
              <mat-option value="Active">Active</mat-option>
              <mat-option value="Prospect">Prospect</mat-option>
              <mat-option value="Inactive">Inactive</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Type</mat-label>
            <mat-select [formControl]="type">
              <mat-option [value]="''">All</mat-option>
              <mat-option value="Residential">Residential</mat-option>
              <mat-option value="Commercial">Commercial</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
      </div>

      @if (loading()) {
        <div style="display: flex; justify-content: center; padding: 3rem">
          <mat-spinner diameter="36" />
        </div>
      } @else if (customers().length === 0) {
        <div class="te-card te-empty-state">
          <mat-icon class="te-empty-state__icon">people_outline</mat-icon>
          <p class="te-empty-state__title">No customers found</p>
          <p class="te-empty-state__text">
            Customers are who your projects and quotes belong to. Add your first one to get going.
          </p>
          <a mat-flat-button color="primary" routerLink="new" *teHasPermission="'customer.manage'">
            Add a customer
          </a>
        </div>
      } @else {
        <div class="te-table-wrapper">
          <table mat-table [dataSource]="customers()" class="te-table">
            <ng-container matColumnDef="customerNumber">
              <th mat-header-cell *matHeaderCellDef>Number</th>
              <td mat-cell *matCellDef="let customer">{{ customer.customerNumber }}</td>
            </ng-container>

            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Name</th>
              <td mat-cell *matCellDef="let customer">
                <a [routerLink]="[customer.id]" style="font-weight: 600">
                  {{ customer.displayName }}
                </a>
              </td>
            </ng-container>

            <ng-container matColumnDef="type">
              <th mat-header-cell *matHeaderCellDef>Type</th>
              <td mat-cell *matCellDef="let customer">{{ customer.type }}</td>
            </ng-container>

            <ng-container matColumnDef="contact">
              <th mat-header-cell *matHeaderCellDef>Contact</th>
              <td mat-cell *matCellDef="let customer">
                <div>{{ customer.email || '—' }}</div>
                <div class="te-muted" style="font-size: 0.75rem">{{ customer.phone || '' }}</div>
              </td>
            </ng-container>

            <ng-container matColumnDef="projects">
              <th mat-header-cell *matHeaderCellDef class="te-numeric">Projects</th>
              <td mat-cell *matCellDef="let customer" class="te-numeric">
                {{ customer.projectCount }}
              </td>
            </ng-container>

            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let customer">
                <span class="te-status" [class]="customer.status | teStatusClass">
                  {{ customer.status | teStatusLabel }}
                </span>
              </td>
            </ng-container>

            <ng-container matColumnDef="createdAt">
              <th mat-header-cell *matHeaderCellDef>Added</th>
              <td mat-cell *matCellDef="let customer">{{ customer.createdAt | teDate }}</td>
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
export class CustomerListComponent {
  private readonly api = inject(CustomerApi);

  readonly columns = ['customerNumber', 'name', 'type', 'contact', 'projects', 'status', 'createdAt'];

  readonly customers = signal<Customer[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly loading = signal(true);

  readonly search = new FormControl('', { nonNullable: true });
  readonly status = new FormControl('', { nonNullable: true });
  readonly type = new FormControl('', { nonNullable: true });

  constructor() {
    // Debounced so a search does not fire a request per keystroke.
    this.search.valueChanges.pipe(debounceTime(300), distinctUntilChanged()).subscribe(() => {
      this.page.set(1);
      this.load();
    });

    this.status.valueChanges.subscribe(() => {
      this.page.set(1);
      this.load();
    });

    this.type.valueChanges.subscribe(() => {
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
        type: this.type.value || undefined,
      })
      .subscribe({
        next: (result) => {
          this.customers.set(result.items);
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
