import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { EstimateApi, QuoteApi } from '../../core/services/api.service';
import { errorMessage } from '../../core/interceptors/error.interceptor';
import type { Estimate, EstimateLine, EstimateSummary } from '../../core/models/api.models';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog.component';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MoneyPipe, PercentagePipe, QuantityPipe, ShortDatePipe, StatusClassPipe, StatusLabelPipe } from '../../shared/pipes';

/**
 * SPEC 15 estimate page: a spreadsheet-style grid with a totals panel.
 *
 * Nothing on this page does arithmetic. Editing a line or changing the pricing strategy sends
 * the change to the server, which re-runs Cost then Pricing and returns the new totals. That is
 * what keeps the screen and the PDF in agreement.
 */
@Component({
  selector: 'te-estimate-detail',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDialogModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    HasPermissionDirective,
    MoneyPipe,
    PercentagePipe,
    QuantityPipe,
    ShortDatePipe,
    StatusClassPipe,
    StatusLabelPipe,
  ],
  templateUrl: './estimate-detail.component.html',
  styleUrl: './estimate-detail.component.scss',
})
export class EstimateDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(EstimateApi);
  private readonly quoteApi = inject(QuoteApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly fb = inject(FormBuilder);

  readonly estimateId = this.route.snapshot.paramMap.get('id')!;

  readonly estimate = signal<Estimate | null>(null);
  readonly versions = signal<EstimateSummary[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);

  /** The line currently being edited inline, if any. */
  readonly editingLineId = signal<string | null>(null);

  readonly columns = [
    'description', 'room', 'category', 'quantity', 'unit',
    'unitCost', 'unitPrice', 'waste', 'totalCost', 'totalPrice', 'actions',
  ];

  readonly isEditable = computed(() => this.estimate()?.isEditable ?? false);

  readonly pricingForm = this.fb.nonNullable.group({
    pricingStrategy: ['Markup'],
    markupPercentage: [0],
    marginPercentage: [0],
    fixedMarkupAmount: [0],
    overheadPercentage: [0],
    discountType: ['None'],
    discountValue: [0],
    taxRatePercentage: [0],
    taxBasis: ['MaterialsAndLabor'],
    discountBeforeTax: [true],
  });

  readonly lineForm = this.fb.nonNullable.group({
    description: ['', Validators.required],
    category: ['Other'],
    quantity: [1, [Validators.required, Validators.min(0)]],
    unit: ['EA', Validators.required],
    unitCost: [0, Validators.min(0)],
    unitPrice: [0, Validators.min(0)],
  });

  readonly editForm = this.fb.nonNullable.group({
    description: ['', Validators.required],
    quantity: [0, Validators.min(0)],
    unit: ['EA'],
    unitCost: [0, Validators.min(0)],
    unitPrice: [0, Validators.min(0)],
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);

    this.api.get(this.estimateId).subscribe({
      next: (estimate) => {
        this.apply(estimate);
        this.loading.set(false);
        this.api.versions(this.estimateId).subscribe((versions) => this.versions.set(versions));
      },
      error: () => {
        this.loading.set(false);
        void this.router.navigate(['/estimates']);
      },
    });
  }

  private apply(estimate: Estimate): void {
    this.estimate.set(estimate);
    this.editingLineId.set(null);

    this.pricingForm.patchValue(
      {
        pricingStrategy: estimate.pricingStrategy,
        markupPercentage: estimate.markupPercentage,
        marginPercentage: estimate.marginPercentage,
        fixedMarkupAmount: estimate.fixedMarkupAmount,
        overheadPercentage: estimate.overheadPercentage,
        discountType: estimate.discountType,
        discountValue: estimate.discountValue,
        taxRatePercentage: estimate.taxRatePercentage,
        taxBasis: estimate.taxBasis,
        discountBeforeTax: estimate.discountBeforeTax,
      },
      { emitEvent: false },
    );

    if (estimate.isEditable) {
      this.pricingForm.enable({ emitEvent: false });
    } else {
      this.pricingForm.disable({ emitEvent: false });
    }
  }

  private run(request$: ReturnType<EstimateApi['calculate']>, successMessage?: string): void {
    this.busy.set(true);

    request$.subscribe({
      next: (estimate) => {
        this.apply(estimate);
        this.busy.set(false);
        if (successMessage) {
          this.snackBar.open(successMessage, 'Dismiss', { duration: 3500 });
        }
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.snackBar.open(errorMessage(err, 'That change could not be saved.'), 'Dismiss', {
          duration: 8000,
        });
        // A 409 means someone else saved first: reload so the estimator sees their numbers.
        this.load();
      },
    });
  }

  // --- Pricing -----------------------------------------------------------------------------

  applyPricing(): void {
    const estimate = this.estimate();
    if (!estimate) return;

    const value = this.pricingForm.getRawValue();

    this.run(
      this.api.updatePricing(this.estimateId, {
        pricingStrategy: value.pricingStrategy as Estimate['pricingStrategy'],
        markupPercentage: value.markupPercentage,
        marginPercentage: value.marginPercentage,
        fixedMarkupAmount: value.fixedMarkupAmount,
        overheadPercentage: value.overheadPercentage,
        discountType: value.discountType as Estimate['discountType'],
        discountValue: value.discountValue,
        taxRatePercentage: value.taxRatePercentage,
        taxBasis: value.taxBasis as Estimate['taxBasis'],
        discountBeforeTax: value.discountBeforeTax,
        // Sent back so a stale edit is refused rather than silently overwriting.
        rowVersion: estimate.rowVersion,
      }),
      'Pricing updated.',
    );
  }

  recalculate(): void {
    this.run(this.api.calculate(this.estimateId), 'Totals recalculated.');
  }

  rebuild(): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Rebuild from the project?',
          message:
            'Every line will be replaced with a fresh expansion of the project rooms and surfaces. '
            + 'Lines you added by hand and any price overrides will be lost.',
          confirmLabel: 'Rebuild lines',
          destructive: true,
        },
      })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) this.run(this.api.rebuild(this.estimateId), 'Lines rebuilt from the project.');
      });
  }

  // --- Lines -------------------------------------------------------------------------------

  addLine(): void {
    if (this.lineForm.invalid) {
      this.lineForm.markAllAsTouched();
      return;
    }

    const value = this.lineForm.getRawValue();

    this.busy.set(true);
    this.api.addLine(this.estimateId, value).subscribe({
      next: (estimate) => {
        this.apply(estimate);
        this.busy.set(false);
        this.lineForm.reset({
          description: '',
          category: 'Other',
          quantity: 1,
          unit: 'EA',
          unitCost: 0,
          unitPrice: 0,
        });
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.snackBar.open(errorMessage(err, 'The line could not be added.'), 'Dismiss', {
          duration: 7000,
        });
      },
    });
  }

  startEdit(line: EstimateLine): void {
    this.editingLineId.set(line.id);
    this.editForm.setValue({
      description: line.description,
      quantity: line.purchaseQuantity > 0 ? line.purchaseQuantity : line.quantity,
      unit: line.unit,
      unitCost: line.unitCost,
      unitPrice: line.unitPrice,
    });
  }

  cancelEdit(): void {
    this.editingLineId.set(null);
  }

  saveEdit(line: EstimateLine): void {
    if (this.editForm.invalid) return;

    const value = this.editForm.getRawValue();

    this.run(
      this.api.updateLine(this.estimateId, line.id, {
        category: line.category,
        description: value.description,
        quantity: value.quantity,
        unit: value.unit,
        unitCost: value.unitCost,
        unitPrice: value.unitPrice,
        roomId: line.roomId,
        notes: line.notes,
      }),
    );
  }

  duplicateLine(line: EstimateLine): void {
    this.run(this.api.duplicateLine(this.estimateId, line.id), 'Line duplicated.');
  }

  deleteLine(line: EstimateLine): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Delete this line?',
          message: `"${line.description}" will be removed and the totals recalculated.`,
          confirmLabel: 'Delete line',
          destructive: true,
        },
      })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) this.run(this.api.deleteLine(this.estimateId, line.id));
      });
  }

  overridePrice(line: EstimateLine): void {
    const input = window.prompt(
      `Override the unit price for "${line.description}".\nCalculated price: ${line.unitPrice}`,
      String(line.unitPrice),
    );

    if (input === null) return;

    const price = Number(input);
    if (Number.isNaN(price) || price < 0) {
      this.snackBar.open('Enter a price of zero or more.', 'Dismiss', { duration: 4000 });
      return;
    }

    const reason = window.prompt('Why is this price being overridden? (optional)') ?? undefined;
    this.run(this.api.overridePrice(this.estimateId, line.id, price, reason), 'Price overridden.');
  }

  clearOverride(line: EstimateLine): void {
    this.run(this.api.clearOverride(this.estimateId, line.id), 'Calculated price restored.');
  }

  // --- Lifecycle -----------------------------------------------------------------------------

  finalize(): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Finalize this estimate?',
          message:
            'A finalized estimate cannot be edited. To change it afterwards you create a new '
            + 'version, and the current one is kept as history.',
          confirmLabel: 'Finalize',
        },
      })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) this.run(this.api.finalize(this.estimateId), 'Estimate finalized.');
      });
  }

  newVersion(): void {
    this.busy.set(true);

    this.api.newVersion(this.estimateId).subscribe({
      next: (estimate) => {
        this.busy.set(false);
        void this.router.navigate(['/estimates', estimate.id]);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.snackBar.open(errorMessage(err, 'A new version could not be created.'), 'Dismiss', {
          duration: 7000,
        });
      },
    });
  }

  createQuote(): void {
    this.busy.set(true);

    this.quoteApi.create(this.estimateId).subscribe({
      next: (quote) => {
        this.busy.set(false);
        void this.router.navigate(['/quotes', quote.id]);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.snackBar.open(
          errorMessage(err, 'The quote could not be created. Finalize the estimate first.'),
          'Dismiss',
          { duration: 8000 },
        );
      },
    });
  }
}
