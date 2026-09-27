import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { toSignal } from '@angular/core/rxjs-interop';

import { ProjectApi } from '../../core/services/api.service';
import { errorMessage } from '../../core/interceptors/error.interceptor';
import type {
  Assembly,
  Pattern,
  Room,
  Surface,
  SurfaceType,
  Tile,
} from '../../core/models/api.models';

export interface SurfaceDialogData {
  readonly projectId: string;
  readonly room: Room;
  readonly surface: Surface | null;
  readonly tiles: Tile[];
  readonly patterns: Pattern[];
  readonly assemblies: Assembly[];
}

/** Surfaces measured across the plan rather than up a wall. */
const FLOOR_TYPES: readonly SurfaceType[] = ['Floor', 'ShowerFloor'];

@Component({
  selector: 'te-surface-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.surface ? 'Edit surface' : 'Add surface' }}</h2>

    <mat-dialog-content>
      <form [formGroup]="form" id="surface-form" (ngSubmit)="save()">
        <div class="te-form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Surface name</mat-label>
            <input matInput formControlName="name" placeholder="e.g. Bathroom floor" />
            @if (form.controls.name.touched && form.controls.name.invalid) {
              <mat-error>Give the surface a name.</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Surface type</mat-label>
            <mat-select formControlName="type">
              <mat-option value="Floor">Floor</mat-option>
              <mat-option value="Wall">Wall</mat-option>
              <mat-option value="ShowerFloor">Shower floor</mat-option>
              <mat-option value="ShowerWall">Shower wall</mat-option>
              <mat-option value="Backsplash">Backsplash</mat-option>
              <mat-option value="Custom">Custom</mat-option>
            </mat-select>
          </mat-form-field>
        </div>

        <h3 class="te-dialog-section">Measurements</h3>
        <p class="te-muted te-dialog-hint">
          {{
            isFloorType()
              ? 'A floor is measured across the plan: length by width.'
              : 'A wall is measured along its run and up its height. Enter one surface per wall run, or add the runs together.'
          }}
        </p>

        <div class="te-form-grid te-form-grid--narrow">
          <mat-form-field appearance="outline">
            <mat-label>Length (ft)</mat-label>
            <input matInput type="number" step="0.01" min="0.01" formControlName="lengthFeet" />
            @if (form.controls.lengthFeet.touched && form.controls.lengthFeet.invalid) {
              <mat-error>Length must be greater than zero.</mat-error>
            }
          </mat-form-field>

          @if (isFloorType()) {
            <mat-form-field appearance="outline">
              <mat-label>Width (ft)</mat-label>
              <input matInput type="number" step="0.01" min="0.01" formControlName="widthFeet" />
              @if (form.controls.widthFeet.touched && form.controls.widthFeet.invalid) {
                <mat-error>A floor needs a width.</mat-error>
              }
            </mat-form-field>
          } @else {
            <mat-form-field appearance="outline">
              <mat-label>Height (ft)</mat-label>
              <input matInput type="number" step="0.01" min="0.01" formControlName="heightFeet" />
              @if (form.controls.heightFeet.touched && form.controls.heightFeet.invalid) {
                <mat-error>A wall needs a height.</mat-error>
              }
            </mat-form-field>
          }

          <mat-form-field appearance="outline">
            <mat-label>Trim / edge (LF)</mat-label>
            <input matInput type="number" step="0.01" min="0" formControlName="trimLinearFeet" />
            <mat-hint>Bullnose and edge trim are bought by the foot.</mat-hint>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Area override (SF)</mat-label>
            <input matInput type="number" step="0.01" min="0.01" formControlName="areaOverrideSquareFeet" />
            <mat-hint>For irregular shapes. Replaces the calculation above.</mat-hint>
          </mat-form-field>
        </div>

        @if (previewArea() !== null) {
          <div class="te-preview">
            <mat-icon class="te-inline-icon">straighten</mat-icon>
            Gross area <strong>{{ previewArea() }} SF</strong>
            <span class="te-muted">
              — openings and waste are applied by the server when the takeoff runs.
            </span>
          </div>
        }

        <h3 class="te-dialog-section">What goes on it</h3>

        <div class="te-form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Tile</mat-label>
            <mat-select formControlName="tileId">
              <mat-option [value]="null">None yet</mat-option>
              @for (tile of data.tiles; track tile.id) {
                <mat-option [value]="tile.id">
                  {{ tile.productName }} ({{ tile.sku }}) — {{ tile.sqFtPerBox }} SF/box
                </mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Pattern</mat-label>
            <mat-select formControlName="patternId">
              <mat-option [value]="null">None</mat-option>
              @for (pattern of data.patterns; track pattern.id) {
                <mat-option [value]="pattern.id">
                  {{ pattern.name }} — {{ pattern.defaultWastePercentage }}% default waste
                </mat-option>
              }
            </mat-select>
            <mat-hint>Sets the starting waste percentage.</mat-hint>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Assembly</mat-label>
            <mat-select formControlName="assemblyId">
              <mat-option [value]="null">None</mat-option>
              @for (assembly of matchingAssemblies(); track assembly.id) {
                <mat-option [value]="assembly.id">{{ assembly.name }}</mat-option>
              }
            </mat-select>
            <mat-hint>Expands into thinset, grout, labor and the rest.</mat-hint>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Waste override (%)</mat-label>
            <input matInput type="number" step="0.1" min="0" formControlName="wasteOverridePercentage" />
            <mat-hint>Leave blank to use the pattern default or a matching waste rule.</mat-hint>
          </mat-form-field>

          <mat-form-field appearance="outline" class="te-field-full">
            <mat-label>Notes</mat-label>
            <input matInput formControlName="notes" />
          </mat-form-field>
        </div>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-stroked-button (click)="dialogRef.close(false)">Cancel</button>
      <button mat-flat-button color="primary" type="submit" form="surface-form" [disabled]="saving()">
        @if (saving()) {
          <mat-spinner diameter="20" />
        } @else {
          {{ data.surface ? 'Save surface' : 'Add surface' }}
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .te-dialog-section {
      margin: 1.25rem 0 0.25rem;
      font-size: 0.875rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      opacity: 0.7;
    }

    .te-dialog-hint {
      margin: 0 0 0.75rem;
      font-size: 0.8125rem;
    }

    .te-preview {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 0.375rem;
      padding: 0.625rem 0.875rem;
      border-radius: 8px;
      background: var(--mat-sys-surface-container-high);
      font-size: 0.875rem;
    }
  `,
})
export class SurfaceDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ProjectApi);
  private readonly snackBar = inject(MatSnackBar);

  readonly dialogRef = inject(MatDialogRef<SurfaceDialogComponent, boolean>);
  readonly data = inject<SurfaceDialogData>(MAT_DIALOG_DATA);

  readonly saving = signal(false);

  readonly form = this.fb.nonNullable.group({
    name: [this.data.surface?.name ?? '', Validators.required],
    type: [(this.data.surface?.type ?? 'Floor') as SurfaceType],
    lengthFeet: [this.data.surface?.lengthFeet ?? null as number | null, [Validators.required, Validators.min(0.01)]],
    widthFeet: [this.data.surface?.widthFeet ?? null as number | null],
    heightFeet: [this.data.surface?.heightFeet ?? null as number | null],
    areaOverrideSquareFeet: [this.data.surface?.areaOverrideSquareFeet ?? null as number | null],
    trimLinearFeet: [this.data.surface?.trimLinearFeet ?? 0],
    tileId: [this.data.surface?.tileId ?? null as string | null],
    patternId: [this.data.surface?.patternId ?? null as string | null],
    assemblyId: [this.data.surface?.assemblyId ?? null as string | null],
    wasteOverridePercentage: [this.data.surface?.wasteOverridePercentage ?? null as number | null],
    notes: [this.data.surface?.notes ?? ''],
  });

  private readonly formValue = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  });

  readonly isFloorType = computed(() => {
    const type = this.formValue()?.type ?? 'Floor';
    return FLOOR_TYPES.includes(type as SurfaceType);
  });

  /**
   * A local preview of the gross area so the estimator sees the shape of the number as they
   * type. This is length x width only: net area, waste and every quantity that follows come
   * from the server's engine, which stays the single source of truth.
   */
  readonly previewArea = computed(() => {
    const value = this.formValue();
    if (!value) return null;

    if (value.areaOverrideSquareFeet) {
      return round(value.areaOverrideSquareFeet);
    }

    const length = value.lengthFeet;
    const second = this.isFloorType() ? value.widthFeet : value.heightFeet;

    return length && second ? round(length * second) : null;
  });

  readonly matchingAssemblies = computed(() => {
    const type = this.formValue()?.type;
    // Show assemblies meant for this surface type first, but never hide the others: a
    // contractor may deliberately use a wall system on a custom surface.
    return [...this.data.assemblies].sort((a, b) => {
      const aMatch = a.appliesToSurfaceType === type ? 0 : 1;
      const bMatch = b.appliesToSurfaceType === type ? 0 : 1;
      return aMatch - bMatch || a.name.localeCompare(b.name);
    });
  });

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const floor = this.isFloorType();

    // Only send the dimension that matters for this surface type, so a leftover height on a
    // floor cannot confuse the server's validation.
    const request = {
      name: value.name,
      type: value.type,
      lengthFeet: value.lengthFeet!,
      widthFeet: floor ? value.widthFeet : null,
      heightFeet: floor ? null : value.heightFeet,
      areaOverrideSquareFeet: value.areaOverrideSquareFeet || null,
      trimLinearFeet: value.trimLinearFeet ?? 0,
      tileId: value.tileId,
      patternId: value.patternId,
      assemblyId: value.assemblyId,
      wasteOverridePercentage: value.wasteOverridePercentage,
      notes: value.notes || null,
      sortOrder: this.data.surface?.sortOrder ?? this.data.room.surfaces.length + 1,
    };

    this.saving.set(true);

    const request$ = this.data.surface
      ? this.api.updateSurface(this.data.projectId, this.data.room.id, this.data.surface.id, request)
      : this.api.createSurface(this.data.projectId, this.data.room.id, request);

    request$.subscribe({
      next: () => this.dialogRef.close(true),
      error: (err: unknown) => {
        this.saving.set(false);
        this.snackBar.open(errorMessage(err, 'The surface could not be saved.'), 'Dismiss', {
          duration: 7000,
        });
      },
    });
  }
}

function round(value: number): number {
  return Math.round(value * 10000) / 10000;
}
