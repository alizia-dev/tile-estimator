import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';

import { ProjectApi } from '../../core/services/api.service';
import { errorMessage } from '../../core/interceptors/error.interceptor';
import type { Opening, Surface } from '../../core/models/api.models';

export interface OpeningDialogData {
  readonly projectId: string;
  readonly surface: Surface;
  readonly opening: Opening | null;
}

/** Openings that are tiled rather than cut around, so their area is added, not deducted. */
const ADDS_AREA_BY_DEFAULT = new Set(['Niche', 'Bench']);

@Component({
  selector: 'te-opening-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.opening ? 'Edit opening' : 'Add opening' }}</h2>

    <mat-dialog-content>
      <p class="te-muted" style="margin-top: 0; font-size: 0.8125rem">
        On <strong>{{ data.surface.name }}</strong>. Doors and windows are deducted from the area;
        niches and benches are tiled, so they are added to it.
      </p>

      <form [formGroup]="form" id="opening-form" (ngSubmit)="save()">
        <div class="te-form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Name</mat-label>
            <input matInput formControlName="name" />
            @if (form.controls.name.touched && form.controls.name.invalid) {
              <mat-error>Give the opening a name.</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Type</mat-label>
            <mat-select formControlName="type">
              <mat-option value="Door">Door</mat-option>
              <mat-option value="Window">Window</mat-option>
              <mat-option value="Niche">Niche</mat-option>
              <mat-option value="Bench">Bench</mat-option>
              <mat-option value="Cabinet">Cabinet / vanity</mat-option>
              <mat-option value="Fixture">Fixture</mat-option>
              <mat-option value="Other">Other</mat-option>
            </mat-select>
          </mat-form-field>
        </div>

        <div class="te-form-grid te-form-grid--narrow">
          <mat-form-field appearance="outline">
            <mat-label>Width (ft)</mat-label>
            <input matInput type="number" step="0.01" min="0.01" formControlName="widthFeet" />
            @if (form.controls.widthFeet.touched && form.controls.widthFeet.invalid) {
              <mat-error>Width must be greater than zero.</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Height (ft)</mat-label>
            <input matInput type="number" step="0.01" min="0.01" formControlName="heightFeet" />
            @if (form.controls.heightFeet.touched && form.controls.heightFeet.invalid) {
              <mat-error>Height must be greater than zero.</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>How many</mat-label>
            <input matInput type="number" step="1" min="1" formControlName="quantity" />
            <mat-hint>One row can cover several identical openings.</mat-hint>
          </mat-form-field>
        </div>

        <mat-checkbox formControlName="addsArea">
          This area is tiled (added rather than deducted)
        </mat-checkbox>

        @if (totalArea() !== null) {
          <p style="margin-top: 1rem; font-size: 0.9375rem">
            <mat-icon class="te-inline-icon">straighten</mat-icon>
            {{ form.controls.addsArea.value ? 'Adds' : 'Deducts' }}
            <strong>{{ totalArea() }} SF</strong>
          </p>
        }
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-stroked-button (click)="dialogRef.close(false)">Cancel</button>
      <button mat-flat-button color="primary" type="submit" form="opening-form" [disabled]="saving()">
        @if (saving()) {
          <mat-spinner diameter="20" />
        } @else {
          {{ data.opening ? 'Save' : 'Add opening' }}
        }
      </button>
    </mat-dialog-actions>
  `,
})
export class OpeningDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ProjectApi);
  private readonly snackBar = inject(MatSnackBar);

  readonly dialogRef = inject(MatDialogRef<OpeningDialogComponent, boolean>);
  readonly data = inject<OpeningDialogData>(MAT_DIALOG_DATA);

  readonly saving = signal(false);

  readonly form = this.fb.nonNullable.group({
    name: [this.data.opening?.name ?? 'Door', Validators.required],
    type: [this.data.opening?.type ?? 'Door'],
    widthFeet: [this.data.opening?.widthFeet ?? null as number | null, [Validators.required, Validators.min(0.01)]],
    heightFeet: [this.data.opening?.heightFeet ?? null as number | null, [Validators.required, Validators.min(0.01)]],
    quantity: [this.data.opening?.quantity ?? 1, [Validators.required, Validators.min(1)]],
    addsArea: [this.data.opening?.addsArea ?? false],
  });

  private readonly formValue = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  });

  readonly totalArea = computed(() => {
    const value = this.formValue();
    if (!value?.widthFeet || !value.heightFeet) return null;
    return Math.round(value.widthFeet * value.heightFeet * (value.quantity ?? 1) * 10000) / 10000;
  });

  constructor() {
    // Picking a niche or bench flips the default to "added", because that is almost always
    // what the estimator means; they can still override it.
    this.form.controls.type.valueChanges.subscribe((type) => {
      if (this.data.opening) return;

      this.form.controls.addsArea.setValue(ADDS_AREA_BY_DEFAULT.has(type));

      if (!this.form.controls.name.dirty) {
        this.form.controls.name.setValue(type);
      }
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();

    const request = {
      name: value.name,
      type: value.type,
      widthFeet: value.widthFeet!,
      heightFeet: value.heightFeet!,
      quantity: value.quantity,
      addsArea: value.addsArea,
    };

    this.saving.set(true);

    const request$ = this.data.opening
      ? this.api.updateOpening(this.data.projectId, this.data.surface.id, this.data.opening.id, request)
      : this.api.createOpening(this.data.projectId, this.data.surface.id, request);

    request$.subscribe({
      next: () => this.dialogRef.close(true),
      error: (err: unknown) => {
        this.saving.set(false);
        this.snackBar.open(errorMessage(err, 'The opening could not be saved.'), 'Dismiss', {
          duration: 7000,
        });
      },
    });
  }
}
