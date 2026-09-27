import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { DashboardApi, EstimateApi, QuoteApi } from '../../core/services/api.service';
import type { Dashboard, EstimateSummary, QuoteSummary } from '../../core/models/api.models';
import { MoneyPipe, PercentagePipe, StatusClassPipe, StatusLabelPipe } from '../../shared/pipes';

/**
 * Reporting overview (SPEC 21).
 *
 * The per-estimate documents (material takeoff, cost breakdown, purchase list) are produced
 * from an estimate itself, where the figures and their working are already on screen. This page
 * summarises the organization and links through to them rather than duplicating the numbers.
 */
@Component({
  selector: 'te-reports',
  imports: [
    RouterLink,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MoneyPipe,
    PercentagePipe,
    StatusClassPipe,
    StatusLabelPipe,
  ],
  templateUrl: './reports.component.html',
  styles: `
    .te-kpi-label {
      display: block;
      font-size: 0.6875rem;
      font-weight: 600;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      opacity: 0.6;
      margin-bottom: 0.125rem;
    }
  `,
})
export class ReportsComponent {
  private readonly dashboardApi = inject(DashboardApi);
  private readonly estimateApi = inject(EstimateApi);
  private readonly quoteApi = inject(QuoteApi);

  readonly dashboard = signal<Dashboard | null>(null);
  readonly estimates = signal<EstimateSummary[]>([]);
  readonly quotes = signal<QuoteSummary[]>([]);
  readonly loading = signal(true);

  readonly quoteColumns = ['number', 'customer', 'status', 'total'];
  readonly estimateColumns = ['number', 'project', 'status', 'total'];

  constructor() {
    forkJoin({
      dashboard: this.dashboardApi.get(),
      estimates: this.estimateApi.list({ pageSize: 20 }),
      quotes: this.quoteApi.list({ pageSize: 20 }),
    }).subscribe({
      next: (data) => {
        this.dashboard.set(data.dashboard);
        this.estimates.set(data.estimates.items);
        this.quotes.set(data.quotes.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
