import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';

import { DashboardApi } from '../../core/services/api.service';
import type { Dashboard } from '../../core/models/api.models';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MoneyPipe, PercentagePipe, ShortDatePipe, StatusClassPipe, StatusLabelPipe } from '../../shared/pipes';

/**
 * SPEC 7 dashboard: the numbers a contractor wants first thing in the morning, then the
 * pipeline, then what they were last working on.
 *
 * Every figure here is computed by the server. The page only formats what it is given.
 */
@Component({
  selector: 'te-dashboard',
  imports: [
    RouterLink,
    MatIconModule,
    MatButtonModule,
    MatTableModule,
    MatProgressSpinnerModule,
    HasPermissionDirective,
    MoneyPipe,
    PercentagePipe,
    ShortDatePipe,
    StatusClassPipe,
    StatusLabelPipe,
  ],
  styleUrl: './dashboard.component.scss',
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent {
  private readonly api = inject(DashboardApi);

  readonly data = signal<Dashboard | null>(null);
  readonly loading = signal(true);
  readonly failed = signal(false);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);

    this.api.get().subscribe({
      next: (dashboard) => {
        this.data.set(dashboard);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.failed.set(true);
      },
    });
  }

  /** Width of a pipeline bar, relative to the busiest stage. */
  barWidth(count: number): string {
    const stages = this.data()?.quotePipeline ?? [];
    const max = Math.max(1, ...stages.map((s) => s.count));
    return `${Math.round((count / max) * 100)}%`;
  }
}
