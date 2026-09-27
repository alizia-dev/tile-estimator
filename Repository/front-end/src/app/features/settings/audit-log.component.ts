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
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';

import { OrganizationApi } from '../../core/services/api.service';
import type { AuditLogEntry } from '../../core/models/api.models';
import { ShortDatePipe, StatusLabelPipe } from '../../shared/pipes';

const ACTIONS = [
  'Create', 'Update', 'Delete', 'Login', 'Logout', 'LoginFailed',
  'Finalize', 'Send', 'Approve', 'Reject', 'PermissionChange', 'PriceChange',
] as const;

/**
 * SPEC 18 audit log viewer.
 *
 * The server redacts anything that looks like a password, token or secret before writing an
 * entry, so what is shown here is safe to read and to keep.
 */
@Component({
  selector: 'te-audit-log',
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
    MatTooltipModule,
    MatProgressSpinnerModule,
    ShortDatePipe,
    StatusLabelPipe,
  ],
  template: `
    <div class="te-page">
      <header class="te-page__header">
        <div>
          <h1 class="te-page__title">Audit log</h1>
          <p class="te-page__subtitle">
            {{ total() }} recorded events. Passwords, tokens and secrets are never written here.
          </p>
        </div>
        <div class="te-page__actions">
          <a mat-stroked-button routerLink="/settings">Back to settings</a>
        </div>
      </header>

      <div class="te-card" style="margin-bottom: 1rem">
        <div class="te-form-grid te-form-grid--narrow">
          <mat-form-field appearance="outline">
            <mat-label>Action</mat-label>
            <mat-select [formControl]="action">
              <mat-option [value]="''">All</mat-option>
              @for (a of actions; track a) {
                <mat-option [value]="a">{{ a | teStatusLabel }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Entity</mat-label>
            <mat-select [formControl]="entityName">
              <mat-option [value]="''">All</mat-option>
              <mat-option value="Estimate">Estimate</mat-option>
              <mat-option value="Quote">Quote</mat-option>
              <mat-option value="Project">Project</mat-option>
              <mat-option value="Customer">Customer</mat-option>
              <mat-option value="Tile">Tile</mat-option>
              <mat-option value="Material">Material</mat-option>
              <mat-option value="LaborRate">Labor rate</mat-option>
              <mat-option value="User">User</mat-option>
              <mat-option value="OrganizationMember">Membership</mat-option>
            </mat-select>
          </mat-form-field>
        </div>
      </div>

      @if (loading()) {
        <div style="display: flex; justify-content: center; padding: 3rem">
          <mat-spinner diameter="36" />
        </div>
      } @else if (entries().length === 0) {
        <div class="te-card te-empty-state">
          <mat-icon class="te-empty-state__icon">fact_check</mat-icon>
          <p class="te-empty-state__title">Nothing recorded yet</p>
          <p class="te-empty-state__text">Actions appear here as people use the system.</p>
        </div>
      } @else {
        <div class="te-table-wrapper">
          <table mat-table [dataSource]="entries()" class="te-table">
            <ng-container matColumnDef="when">
              <th mat-header-cell *matHeaderCellDef>When</th>
              <td mat-cell *matCellDef="let entry">{{ entry.createdAt | teDate: true }}</td>
            </ng-container>

            <ng-container matColumnDef="who">
              <th mat-header-cell *matHeaderCellDef>Who</th>
              <td mat-cell *matCellDef="let entry">
                {{ entry.userEmail || 'System' }}
                @if (entry.ipAddress) {
                  <div class="te-muted" style="font-size: 0.75rem">{{ entry.ipAddress }}</div>
                }
              </td>
            </ng-container>

            <ng-container matColumnDef="action">
              <th mat-header-cell *matHeaderCellDef>Action</th>
              <td mat-cell *matCellDef="let entry">
                <span class="te-status" [class]="actionClass(entry.action)">
                  {{ entry.action | teStatusLabel }}
                </span>
              </td>
            </ng-container>

            <ng-container matColumnDef="entity">
              <th mat-header-cell *matHeaderCellDef>Entity</th>
              <td mat-cell *matCellDef="let entry">{{ entry.entityName | teStatusLabel }}</td>
            </ng-container>

            <ng-container matColumnDef="summary">
              <th mat-header-cell *matHeaderCellDef>What happened</th>
              <td mat-cell *matCellDef="let entry">
                {{ entry.summary }}
                @if (entry.oldValues || entry.newValues) {
                  <button
                    mat-icon-button
                    (click)="toggleDetail(entry.id)"
                    [matTooltip]="expanded() === entry.id ? 'Hide values' : 'Show values'"
                  >
                    <mat-icon>{{ expanded() === entry.id ? 'expand_less' : 'expand_more' }}</mat-icon>
                  </button>

                  @if (expanded() === entry.id) {
                    <div class="te-audit-values">
                      @if (entry.oldValues) {
                        <div><strong>Before:</strong> <code>{{ entry.oldValues }}</code></div>
                      }
                      @if (entry.newValues) {
                        <div><strong>After:</strong> <code>{{ entry.newValues }}</code></div>
                      }
                    </div>
                  }
                }
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
          [pageSizeOptions]="[25, 50, 100]"
          (page)="onPage($event)"
        />
      }
    </div>
  `,
  styles: `
    .te-audit-values {
      margin-top: 0.5rem;
      padding: 0.5rem 0.75rem;
      border-radius: 6px;
      background: var(--mat-sys-surface-container-high);
      font-size: 0.75rem;
      line-height: 1.6;
      word-break: break-word;

      code {
        font-family: ui-monospace, Consolas, monospace;
      }
    }
  `,
})
export class AuditLogComponent {
  private readonly api = inject(OrganizationApi);

  readonly actions = ACTIONS;
  readonly columns = ['when', 'who', 'action', 'entity', 'summary'];

  readonly entries = signal<AuditLogEntry[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(50);
  readonly loading = signal(true);
  readonly expanded = signal<string | null>(null);

  readonly action = new FormControl('', { nonNullable: true });
  readonly entityName = new FormControl('', { nonNullable: true });

  constructor() {
    this.action.valueChanges.subscribe(() => {
      this.page.set(1);
      this.load();
    });

    this.entityName.valueChanges.subscribe(() => {
      this.page.set(1);
      this.load();
    });

    this.load();
  }

  load(): void {
    this.loading.set(true);

    this.api
      .auditLogs({
        page: this.page(),
        pageSize: this.pageSize(),
        action: this.action.value || undefined,
        entityName: this.entityName.value || undefined,
      })
      .subscribe({
        next: (result) => {
          this.entries.set(result.items);
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

  toggleDetail(id: string): void {
    this.expanded.update((current) => (current === id ? null : id));
  }

  actionClass(action: string): string {
    switch (action) {
      case 'Delete':
      case 'Reject':
      case 'LoginFailed':
        return 'te-status--danger';
      case 'Create':
      case 'Approve':
      case 'Finalize':
        return 'te-status--success';
      case 'PriceChange':
      case 'PermissionChange':
        return 'te-status--warn';
      case 'Send':
      case 'Login':
        return 'te-status--info';
      default:
        return 'te-status--neutral';
    }
  }
}
