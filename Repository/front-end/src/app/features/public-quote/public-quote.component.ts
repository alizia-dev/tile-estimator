import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute } from '@angular/router';

import { PublicQuoteApi } from '../../core/services/api.service';
import { errorMessage } from '../../core/interceptors/error.interceptor';
import type { PublicQuote, PublicQuoteDecisionRequest } from '../../core/models/api.models';
import { MoneyPipe, QuantityPipe, ShortDatePipe } from '../../shared/pipes';

type Decision = 'Accepted' | 'Rejected' | 'ChangesRequested';

/**
 * SPEC 17 customer-facing quote page.
 *
 * Unauthenticated: the customer has only the link that was emailed to them. It carries no
 * account, no shell navigation and no way to reach anything else, and the data it shows has
 * already been narrowed server-side to price-only fields.
 *
 * Mobile-first, because a customer usually opens this on a phone.
 */
@Component({
  selector: 'te-public-quote',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MoneyPipe,
    QuantityPipe,
    ShortDatePipe,
  ],
  templateUrl: './public-quote.component.html',
  styleUrl: './public-quote.component.scss',
})
export class PublicQuoteComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(PublicQuoteApi);
  private readonly fb = inject(FormBuilder);

  private readonly token = this.route.snapshot.paramMap.get('token') ?? '';

  readonly quote = signal<PublicQuote | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  /** Which response the customer has chosen, before they confirm it. */
  readonly pendingDecision = signal<Decision | null>(null);

  readonly responseForm = this.fb.nonNullable.group({
    customerName: ['', Validators.required],
    customerEmail: ['', Validators.email],
    comments: [''],
  });

  constructor() {
    this.api.get(this.token).subscribe({
      next: (quote) => {
        this.quote.set(quote);
        this.responseForm.controls.customerName.setValue(quote.customerName);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.notFound.set(true);
      },
    });
  }

  choose(decision: Decision): void {
    this.pendingDecision.set(decision);
    this.error.set(null);

    // Only a rejection or a change request really needs an explanation.
    const comments = this.responseForm.controls.comments;
    if (decision === 'ChangesRequested') {
      comments.addValidators(Validators.required);
    } else {
      comments.removeValidators(Validators.required);
    }
    comments.updateValueAndValidity();
  }

  submit(): void {
    const decision = this.pendingDecision();
    if (!decision) return;

    if (this.responseForm.invalid) {
      this.responseForm.markAllAsTouched();
      return;
    }

    const value = this.responseForm.getRawValue();

    const request: PublicQuoteDecisionRequest = {
      decision,
      customerName: value.customerName,
      customerEmail: value.customerEmail || null,
      comments: value.comments || null,
    };

    this.submitting.set(true);
    this.error.set(null);

    this.api.respond(this.token, request).subscribe({
      next: (quote) => {
        this.quote.set(quote);
        this.submitting.set(false);
        this.pendingDecision.set(null);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.error.set(errorMessage(err, 'Your response could not be recorded. Please try again.'));
      },
    });
  }

  downloadPdf(): void {
    const quote = this.quote();
    if (!quote) return;

    this.api.downloadPdf(this.token).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `Quote-${quote.quoteNumber}.pdf`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.error.set('The PDF could not be downloaded.'),
    });
  }

  decisionLabel(decision: Decision): string {
    switch (decision) {
      case 'Accepted':
        return 'Accept this quote';
      case 'Rejected':
        return 'Decline this quote';
      case 'ChangesRequested':
        return 'Request changes';
    }
  }
}
