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

import { QuoteApi } from '../../core/services/api.service';
import type { QuoteSummary } from '../../core/models/api.models';
import { MoneyPipe, ShortDatePipe, StatusClassPipe, StatusLabelPipe } from '../../shared/pipes';

@Component({
  selector: 'te-quote-list',
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
          <h1 class="te-page__title">Quotes</h1>
          <p class="te-page__subtitle">{{ total() }} in your organization</p>
        </div>
      </header>

      <div class="te-card" style="margin-bottom: 1rem">
        <div class="te-form-grid te-form-grid--narrow">
          <mat-form-field appearance="outline">
            <mat-label>Search</mat-label>
            <input matInput [formControl]="search" placeholder="Quote number or customer" />
            <mat-icon matSuffix>search</mat-icon>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Status</mat-label>
            <mat-select [formControl]="status">
              <mat-option [value]="''">All</mat-option>
              <mat-option value="Draft">Draft</mat-option>
              <mat-option value="Sent">Sent</mat-option>
              <mat-option value="Viewed">Viewed</mat-option>
              <mat-option value="Accepted">Accepted</mat-option>
              <mat-option value="Rejected">Rejected</mat-option>
              <mat-option value="Expired">Expired</mat-option>
              <mat-option value="Cancelled">Cancelled</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
      </div>

      @if (loading()) {
        <div style="display: flex; justify-content: center; padding: 3rem">
          <mat-spinner diameter="36" />
        </div>
      } @else if (quotes().length === 0) {
        <div class="te-card te-empty-state">
          <mat-icon class="te-empty-state__icon">request_quote</mat-icon>
          <p class="te-empty-state__title">No quotes found</p>
          <p class="te-empty-state__text">
            A quote is created from a finalized estimate and captures its prices as a snapshot.
          </p>
          <a mat-flat-button color="primary" routerLink="/estimates">Go to estimates</a>
        </div>
      } @else {
        <div class="te-table-wrapper">
          <table mat-table [dataSource]="quotes()" class="te-table">
            <ng-container matColumnDef="number">
              <th mat-header-cell *matHeaderCellDef>Quote</th>
              <td mat-cell *matCellDef="let quote">
                <a [routerLink]="[quote.id]" style="font-weight: 600">{{ quote.quoteNumber }}</a>
              </td>
            </ng-container>

            <ng-container matColumnDef="customer">
              <th mat-header-cell *matHeaderCellDef>Customer</th>
              <td mat-cell *matCellDef="let quote">{{ quote.customerDisplayName }}</td>
            </ng-container>

            <ng-container matColumnDef="project">
              <th mat-header-cell *matHeaderCellDef>Project</th>
              <td mat-cell *matCellDef="let quote">{{ quote.projectName }}</td>
            </ng-container>

            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let quote">
                <span class="te-status" [class]="quote.status | teStatusClass">
                  {{ quote.status | teStatusLabel }}
                </span>
              </td>
            </ng-container>

            <ng-container matColumnDef="total">
              <th mat-header-cell *matHeaderCellDef class="te-numeric">Total</th>
              <td mat-cell *matCellDef="let quote" class="te-numeric te-money" style="font-weight: 600">
                {{ quote.grandTotal | teMoney: quote.currency }}
              </td>
            </ng-container>

            <ng-container matColumnDef="dates">
              <th mat-header-cell *matHeaderCellDef>Dates</th>
              <td mat-cell *matCellDef="let quote">
                <div>{{ quote.quoteDate | teDate }}</div>
                <div class="te-muted" style="font-size: 0.75rem">
                  expires {{ quote.expiresAt | teDate }}
                </div>
              </td>
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
export class QuoteListComponent {
  private readonly api = inject(QuoteApi);

  readonly columns = ['number', 'customer', 'project', 'status', 'total', 'dates'];

  readonly quotes = signal<QuoteSummary[]>([]);
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
          this.quotes.set(result.items);
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
