import { NgTemplateOutlet } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { forkJoin } from 'rxjs';

import { CatalogApi, TakeoffApi } from '../../core/services/api.service';
import { errorMessage } from '../../core/interceptors/error.interceptor';
import type { Assembly, Pattern, TakeoffResult, Tile } from '../../core/models/api.models';
import { MoneyPipe, QuantityPipe } from '../../shared/pipes';

/**
 * SPEC 16 quick estimators: floor, wall, backsplash, shower and bathroom.
 *
 * Each one posts to its own endpoint, and every one of those endpoints runs the same takeoff
 * engine the project takeoff uses. A quick calculation and a full estimate therefore agree on
 * identical inputs, which is the whole point of having one engine.
 */
@Component({
  selector: 'te-calculators',
  imports: [
    NgTemplateOutlet,
    ReactiveFormsModule,
    MatTabsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MoneyPipe,
    QuantityPipe,
  ],
  templateUrl: './calculators.component.html',
  styleUrl: './calculators.component.scss',
})
export class CalculatorsComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(TakeoffApi);
  private readonly catalogApi = inject(CatalogApi);

  readonly tiles = signal<Tile[]>([]);
  readonly patterns = signal<Pattern[]>([]);
  readonly assemblies = signal<Assembly[]>([]);

  readonly result = signal<TakeoffResult | null>(null);
  readonly calculating = signal(false);
  readonly error = signal<string | null>(null);
  readonly loadingCatalog = signal(true);

  readonly resultColumns = ['description', 'quantity', 'purchase', 'unitPrice', 'total'];

  readonly floorForm = this.fb.nonNullable.group({
    lengthFeet: [12, [Validators.required, Validators.min(0.01)]],
    widthFeet: [15, [Validators.required, Validators.min(0.01)]],
    trimLinearFeet: [0],
    tileId: [null as string | null],
    patternId: [null as string | null],
    assemblyId: [null as string | null],
    wasteOverridePercentage: [null as number | null],
  });

  readonly wallForm = this.fb.nonNullable.group({
    lengthFeet: [10, [Validators.required, Validators.min(0.01)]],
    heightFeet: [8, [Validators.required, Validators.min(0.01)]],
    doorWidthFeet: [0],
    doorHeightFeet: [0],
    tileId: [null as string | null],
    patternId: [null as string | null],
    assemblyId: [null as string | null],
    wasteOverridePercentage: [null as number | null],
  });

  readonly backsplashForm = this.fb.nonNullable.group({
    lengthFeet: [12, [Validators.required, Validators.min(0.01)]],
    heightFeet: [1.5, [Validators.required, Validators.min(0.01)]],
    tileId: [null as string | null],
    patternId: [null as string | null],
    assemblyId: [null as string | null],
    wasteOverridePercentage: [null as number | null],
  });

  readonly showerForm = this.fb.nonNullable.group({
    widthFeet: [3, [Validators.required, Validators.min(0.01)]],
    depthFeet: [5, [Validators.required, Validators.min(0.01)]],
    wallHeightFeet: [8, [Validators.required, Validators.min(0.01)]],
    floorTileId: [null as string | null],
    wallTileId: [null as string | null],
    floorAssemblyId: [null as string | null],
    wallAssemblyId: [null as string | null],
    patternId: [null as string | null],
    wasteOverridePercentage: [null as number | null],
    includeNiche: [true],
    nicheWidthFeet: [2],
    nicheHeightFeet: [1.25],
    includeBench: [false],
    benchWidthFeet: [3],
    benchDepthFeet: [1.5],
    doorWidthFeet: [2.5],
    doorHeightFeet: [7],
  });

  readonly bathroomForm = this.fb.nonNullable.group({
    floorLengthFeet: [8, [Validators.required, Validators.min(0.01)]],
    floorWidthFeet: [10, [Validators.required, Validators.min(0.01)]],
    floorTileId: [null as string | null],
    floorAssemblyId: [null as string | null],
    wallLengthFeet: [0],
    wallHeightFeet: [0],
    wallTileId: [null as string | null],
    wallAssemblyId: [null as string | null],
    backsplashLengthFeet: [6],
    backsplashHeightFeet: [1.5],
    backsplashTileId: [null as string | null],
    backsplashAssemblyId: [null as string | null],
    patternId: [null as string | null],
    wasteOverridePercentage: [null as number | null],
    includeShower: [true],
    showerWidthFeet: [3],
    showerDepthFeet: [5],
    showerWallHeightFeet: [8],
    showerFloorTileId: [null as string | null],
    showerWallTileId: [null as string | null],
    showerFloorAssemblyId: [null as string | null],
    showerWallAssemblyId: [null as string | null],
  });

  constructor() {
    forkJoin({
      tiles: this.catalogApi.listTiles({ pageSize: 200, active: true }),
      patterns: this.catalogApi.listPatterns(),
      assemblies: this.catalogApi.listAssemblies(),
    }).subscribe({
      next: (data) => {
        this.tiles.set(data.tiles.items);
        this.patterns.set(data.patterns);
        this.assemblies.set(data.assemblies);
        this.loadingCatalog.set(false);
      },
      error: () => this.loadingCatalog.set(false),
    });
  }

  calculateFloor(): void {
    if (this.floorForm.invalid) {
      this.floorForm.markAllAsTouched();
      return;
    }

    const value = this.floorForm.getRawValue();
    this.run(this.api.floor({ ...value, openings: [] }));
  }

  calculateWall(): void {
    if (this.wallForm.invalid) {
      this.wallForm.markAllAsTouched();
      return;
    }

    const value = this.wallForm.getRawValue();

    this.run(
      this.api.wall({
        lengthFeet: value.lengthFeet,
        heightFeet: value.heightFeet,
        tileId: value.tileId,
        patternId: value.patternId,
        assemblyId: value.assemblyId,
        wasteOverridePercentage: value.wasteOverridePercentage,
        openings:
          value.doorWidthFeet > 0 && value.doorHeightFeet > 0
            ? [
                {
                  name: 'Door',
                  type: 'Door',
                  widthFeet: value.doorWidthFeet,
                  heightFeet: value.doorHeightFeet,
                  quantity: 1,
                  addsArea: false,
                },
              ]
            : [],
      }),
    );
  }

  calculateBacksplash(): void {
    if (this.backsplashForm.invalid) {
      this.backsplashForm.markAllAsTouched();
      return;
    }

    const value = this.backsplashForm.getRawValue();
    this.run(this.api.backsplash({ ...value, openings: [] }));
  }

  calculateShower(): void {
    if (this.showerForm.invalid) {
      this.showerForm.markAllAsTouched();
      return;
    }

    this.run(this.api.shower(this.showerForm.getRawValue()));
  }

  calculateBathroom(): void {
    if (this.bathroomForm.invalid) {
      this.bathroomForm.markAllAsTouched();
      return;
    }

    const value = this.bathroomForm.getRawValue();

    this.run(
      this.api.bathroom({
        floorLengthFeet: value.floorLengthFeet,
        floorWidthFeet: value.floorWidthFeet,
        floorTileId: value.floorTileId,
        floorAssemblyId: value.floorAssemblyId,
        wallLengthFeet: value.wallLengthFeet,
        wallHeightFeet: value.wallHeightFeet,
        wallTileId: value.wallTileId,
        wallAssemblyId: value.wallAssemblyId,
        backsplashLengthFeet: value.backsplashLengthFeet,
        backsplashHeightFeet: value.backsplashHeightFeet,
        backsplashTileId: value.backsplashTileId,
        backsplashAssemblyId: value.backsplashAssemblyId,
        patternId: value.patternId,
        wasteOverridePercentage: value.wasteOverridePercentage,
        shower: value.includeShower
          ? {
              widthFeet: value.showerWidthFeet,
              depthFeet: value.showerDepthFeet,
              wallHeightFeet: value.showerWallHeightFeet,
              floorTileId: value.showerFloorTileId,
              wallTileId: value.showerWallTileId,
              floorAssemblyId: value.showerFloorAssemblyId,
              wallAssemblyId: value.showerWallAssemblyId,
              patternId: value.patternId,
              wasteOverridePercentage: value.wasteOverridePercentage,
              includeNiche: true,
              nicheWidthFeet: 2,
              nicheHeightFeet: 1.25,
              includeBench: false,
              benchWidthFeet: 3,
              benchDepthFeet: 1.5,
              doorWidthFeet: 2.5,
              doorHeightFeet: 7,
            }
          : null,
      }),
    );
  }

  private run(request$: ReturnType<TakeoffApi['floor']>): void {
    this.calculating.set(true);
    this.error.set(null);
    this.result.set(null);

    request$.subscribe({
      next: (result) => {
        this.result.set(result);
        this.calculating.set(false);
      },
      error: (err: unknown) => {
        this.calculating.set(false);
        this.error.set(errorMessage(err, 'That could not be calculated. Check the measurements.'));
      },
    });
  }

  /** Clears the result when switching calculators, so stale numbers are never left on screen. */
  onTabChange(): void {
    this.result.set(null);
    this.error.set(null);
  }
}
