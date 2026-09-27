import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { QuoteApi } from '../../core/services/api.service';
import { errorMessage } from '../../core/interceptors/error.interceptor';
import type { Quote } from '../../core/models/api.models';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog.component';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MoneyPipe, QuantityPipe, ShortDatePipe, StatusClassPipe, StatusLabelPipe } from '../../shared/pipes';

@Component({
  selector: 'te-quote-detail',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatFormFieldModule,
    MatInputModule,
    MatDialogModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    HasPermissionDirective,
    MoneyPipe,
    QuantityPipe,
    ShortDatePipe,
    StatusClassPipe,
    StatusLabelPipe,
  ],
  templateUrl: './quote-detail.component.html',
  styleUrl: './quote-detail.component.scss',
})
export class QuoteDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(QuoteApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly fb = inject(FormBuilder);

  readonly quoteId = this.route.snapshot.paramMap.get('id')!;

  readonly quote = signal<Quote | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly showSendPanel = signal(false);

  readonly columns = ['description', 'room', 'quantity', 'unitPrice', 'totalPrice'];

  readonly sendForm = this.fb.nonNullable.group({
    recipients: ['', Validators.required],
    message: [''],
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);

    this.api.get(this.quoteId).subscribe({
      next: (quote) => {
        this.quote.set(quote);
        this.sendForm.controls.recipients.setValue(quote.customerEmail ?? '');
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        void this.router.navigate(['/quotes']);
      },
    });
  }

  /** Opens the PDF in a new tab. The blob is revoked once the tab has had a chance to load it. */
  previewPdf(): void {
    this.busy.set(true);

    this.api.downloadPdf(this.quoteId).subscribe({
      next: (blob) => {
        this.busy.set(false);
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank');
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: () => {
        this.busy.set(false);
        this.snackBar.open('The PDF could not be generated.', 'Dismiss', { duration: 6000 });
      },
    });
  }

  downloadPdf(): void {
    const quote = this.quote();
    if (!quote) return;

    this.busy.set(true);

    this.api.downloadPdf(this.quoteId).subscribe({
      next: (blob) => {
        this.busy.set(false);
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `Quote-${quote.quoteNumber}.pdf`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => {
        this.busy.set(false);
        this.snackBar.open('The PDF could not be downloaded.', 'Dismiss', { duration: 6000 });
      },
    });
  }

  send(): void {
    if (this.sendForm.invalid) {
      this.sendForm.markAllAsTouched();
      return;
    }

    const value = this.sendForm.getRawValue();
    const recipients = value.recipients
      .split(/[,;]/)
      .map((email) => email.trim())
      .filter(Boolean);

    if (recipients.length === 0) {
      this.snackBar.open('Add at least one recipient.', 'Dismiss', { duration: 4000 });
      return;
    }

    this.busy.set(true);

    this.api.send(this.quoteId, recipients, value.message || undefined).subscribe({
      next: (quote) => {
        this.quote.set(quote);
        this.busy.set(false);
        this.showSendPanel.set(false);
        this.snackBar.open(
          `Quote sent to ${recipients.length} recipient${recipients.length === 1 ? '' : 's'}. ` +
            'The customer link has been refreshed.',
          'Dismiss',
          { duration: 6000 },
        );
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.snackBar.open(errorMessage(err, 'The quote could not be sent.'), 'Dismiss', {
          duration: 8000,
        });
      },
    });
  }

  /** Records a decision the customer gave by phone rather than through the link. */
  recordDecision(decision: 'Accepted' | 'Rejected'): void {
    const name = window.prompt(
      `Who confirmed this ${decision === 'Accepted' ? 'acceptance' : 'rejection'}?`,
      this.quote()?.customerDisplayName ?? '',
    );

    if (!name) return;

    const comments = window.prompt('Any notes about the decision? (optional)') ?? undefined;

    this.busy.set(true);

    this.api
      .recordDecision(this.quoteId, { decision, customerName: name, comments })
      .subscribe({
        next: (quote) => {
          this.quote.set(quote);
          this.busy.set(false);
          this.snackBar.open('Decision recorded.', 'Dismiss', { duration: 4000 });
        },
        error: (err: unknown) => {
          this.busy.set(false);
          this.snackBar.open(errorMessage(err, 'The decision could not be recorded.'), 'Dismiss', {
            duration: 7000,
          });
        },
      });
  }

  cancel(): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Cancel this quote?',
          message: 'The customer link will stop working and the quote will be marked cancelled.',
          confirmLabel: 'Cancel quote',
          cancelLabel: 'Keep it',
          destructive: true,
        },
      })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) return;

        this.busy.set(true);
        this.api.cancel(this.quoteId).subscribe({
          next: (quote) => {
            this.quote.set(quote);
            this.busy.set(false);
          },
          error: (err: unknown) => {
            this.busy.set(false);
            this.snackBar.open(errorMessage(err, 'The quote could not be cancelled.'), 'Dismiss', {
              duration: 7000,
            });
          },
        });
      });
  }

  canSend(quote: Quote): boolean {
    return quote.status === 'Draft' || quote.status === 'Sent' || quote.status === 'Viewed';
  }

  canRecordDecision(quote: Quote): boolean {
    return quote.status === 'Sent' || quote.status === 'Viewed';
  }
}
