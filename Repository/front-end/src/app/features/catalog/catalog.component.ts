import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';

import { CatalogApi } from '../../core/services/api.service';
import { errorMessage } from '../../core/interceptors/error.interceptor';
import type {
  Assembly,
  LaborRate,
  Material,
  Pattern,
  SaveLaborRateRequest,
  SaveMaterialRequest,
  SaveTileRequest,
  SaveWasteRuleRequest,
  Tile,
  WasteRule,
} from '../../core/models/api.models';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog.component';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MoneyPipe, PercentagePipe, QuantityPipe, StatusLabelPipe } from '../../shared/pipes';

/**
 * SPEC 10 to SPEC 13 catalog. Tiles, materials, patterns, waste rules, labor rates and
 * assemblies, each on its own tab.
 *
 * Editing happens inline in a panel rather than a dialog, because catalog work is repetitive:
 * a contractor pricing a new supplier list wants to add ten tiles without ten round trips
 * through a modal.
 */
@Component({
  selector: 'te-catalog',
  imports: [
    ReactiveFormsModule,
    MatTabsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatDialogModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    HasPermissionDirective,
    MoneyPipe,
    PercentagePipe,
    QuantityPipe,
    StatusLabelPipe,
  ],
  templateUrl: './catalog.component.html',
  styleUrl: './catalog.component.scss',
})
export class CatalogComponent {
  private readonly api = inject(CatalogApi);
  private readonly fb = inject(FormBuilder);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly tiles = signal<Tile[]>([]);
  readonly materials = signal<Material[]>([]);
  readonly patterns = signal<Pattern[]>([]);
  readonly wasteRules = signal<WasteRule[]>([]);
  readonly laborRates = signal<LaborRate[]>([]);
  readonly assemblies = signal<Assembly[]>([]);

  readonly loading = signal(true);
  readonly saving = signal(false);

  /** Which editor panel is open, and the id being edited (null means a new row). */
  readonly editorKind = signal<'tile' | 'material' | 'pattern' | 'wasteRule' | 'laborRate' | null>(null);
  readonly editingId = signal<string | null>(null);

  readonly tileColumns = ['sku', 'product', 'size', 'coverage', 'cost', 'price', 'active', 'actions'];
  readonly materialColumns = ['sku', 'name', 'category', 'unit', 'coverage', 'cost', 'price', 'active', 'actions'];
  readonly patternColumns = ['name', 'waste', 'active', 'actions'];
  readonly wasteRuleColumns = ['name', 'matches', 'waste', 'priority', 'active', 'actions'];
  readonly laborColumns = ['name', 'trade', 'method', 'rate', 'active', 'actions'];

  readonly tileForm = this.fb.nonNullable.group({
    sku: ['', Validators.required],
    productName: ['', Validators.required],
    brand: [''],
    collection: [''],
    materialType: ['Porcelain'],
    lengthInches: [12, [Validators.required, Validators.min(0.01)]],
    widthInches: [12, [Validators.required, Validators.min(0.01)]],
    thicknessInches: [null as number | null],
    tilesPerBox: [10, [Validators.required, Validators.min(1)]],
    sqFtPerBoxOverride: [null as number | null],
    costPerSqFt: [0, Validators.min(0)],
    sellingPricePerSqFt: [0, Validators.min(0)],
    finish: [''],
    color: [''],
    description: [''],
    active: [true],
  });

  readonly materialForm = this.fb.nonNullable.group({
    sku: ['', Validators.required],
    name: ['', Validators.required],
    category: ['Thinset'],
    unit: ['BAG', Validators.required],
    coverage: [null as number | null],
    cost: [0, Validators.min(0)],
    sellingPrice: [0, Validators.min(0)],
    description: [''],
    active: [true],
  });

  readonly patternForm = this.fb.nonNullable.group({
    name: ['', Validators.required],
    description: [''],
    defaultWastePercentage: [10, [Validators.required, Validators.min(0)]],
    active: [true],
    sortOrder: [0],
  });

  readonly wasteRuleForm = this.fb.nonNullable.group({
    name: ['', Validators.required],
    patternId: [null as string | null],
    surfaceType: [null as string | null],
    tileMaterialType: [null as string | null],
    roomType: [null as string | null],
    wastePercentage: [10, [Validators.required, Validators.min(0)]],
    priority: [10],
    active: [true],
  });

  readonly laborRateForm = this.fb.nonNullable.group({
    name: ['', Validators.required],
    trade: ['Tile Setting', Validators.required],
    unit: ['SF', Validators.required],
    calculationMethod: ['UnitRate' as 'UnitRate' | 'Productivity'],
    rate: [0, Validators.min(0)],
    productivity: [null as number | null],
    description: [''],
    active: [true],
  });

  constructor() {
    this.loadAll();
  }

  private loadAll(): void {
    this.loading.set(true);

    this.api.listTiles({ pageSize: 200 }).subscribe((r) => this.tiles.set(r.items));
    this.api.listMaterials({ pageSize: 200 }).subscribe((r) => this.materials.set(r.items));
    this.api.listPatterns().subscribe((r) => this.patterns.set(r));
    this.api.listWasteRules().subscribe((r) => this.wasteRules.set(r));
    this.api.listLaborRates().subscribe((r) => this.laborRates.set(r));
    this.api.listAssemblies().subscribe({
      next: (r) => {
        this.assemblies.set(r);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  closeEditor(): void {
    this.editorKind.set(null);
    this.editingId.set(null);
  }

  // --- Tiles ------------------------------------------------------------------------------------

  newTile(): void {
    this.tileForm.reset({
      sku: '', productName: '', brand: '', collection: '', materialType: 'Porcelain',
      lengthInches: 12, widthInches: 12, thicknessInches: null, tilesPerBox: 10,
      sqFtPerBoxOverride: null, costPerSqFt: 0, sellingPricePerSqFt: 0,
      finish: '', color: '', description: '', active: true,
    });
    this.editingId.set(null);
    this.editorKind.set('tile');
  }

  editTile(tile: Tile): void {
    this.tileForm.reset({
      sku: tile.sku,
      productName: tile.productName,
      brand: tile.brand ?? '',
      collection: tile.collection ?? '',
      materialType: tile.materialType,
      lengthInches: tile.lengthInches,
      widthInches: tile.widthInches,
      thicknessInches: tile.thicknessInches,
      tilesPerBox: tile.tilesPerBox,
      sqFtPerBoxOverride: null,
      costPerSqFt: tile.costPerSqFt,
      sellingPricePerSqFt: tile.sellingPricePerSqFt,
      finish: tile.finish ?? '',
      color: tile.color ?? '',
      description: tile.description ?? '',
      active: tile.active,
    });
    this.editingId.set(tile.id);
    this.editorKind.set('tile');
  }

  saveTile(): void {
    if (this.tileForm.invalid) {
      this.tileForm.markAllAsTouched();
      return;
    }

    const request = this.tileForm.getRawValue() as SaveTileRequest;
    const id = this.editingId();

    this.persist(
      id ? this.api.updateTile(id, request) : this.api.createTile(request),
      () => this.api.listTiles({ pageSize: 200 }).subscribe((r) => this.tiles.set(r.items)),
    );
  }

  deleteTile(tile: Tile): void {
    this.confirmDelete(
      `Remove ${tile.productName}?`,
      'Estimates already created keep their own price snapshot and are unaffected.',
      () =>
        this.api.deleteTile(tile.id).subscribe({
          next: () => this.api.listTiles({ pageSize: 200 }).subscribe((r) => this.tiles.set(r.items)),
          error: (err: unknown) => this.showError(err),
        }),
    );
  }

  // --- Materials ---------------------------------------------------------------------------------

  newMaterial(): void {
    this.materialForm.reset({
      sku: '', name: '', category: 'Thinset', unit: 'BAG', coverage: null,
      cost: 0, sellingPrice: 0, description: '', active: true,
    });
    this.editingId.set(null);
    this.editorKind.set('material');
  }

  editMaterial(material: Material): void {
    this.materialForm.reset({
      sku: material.sku,
      name: material.name,
      category: material.category,
      unit: material.unit,
      coverage: material.coverage,
      cost: material.cost,
      sellingPrice: material.sellingPrice,
      description: material.description ?? '',
      active: material.active,
    });
    this.editingId.set(material.id);
    this.editorKind.set('material');
  }

  saveMaterial(): void {
    if (this.materialForm.invalid) {
      this.materialForm.markAllAsTouched();
      return;
    }

    const request = this.materialForm.getRawValue() as SaveMaterialRequest;
    const id = this.editingId();

    this.persist(
      id ? this.api.updateMaterial(id, request) : this.api.createMaterial(request),
      () => this.api.listMaterials({ pageSize: 200 }).subscribe((r) => this.materials.set(r.items)),
    );
  }

  deleteMaterial(material: Material): void {
    this.confirmDelete(
      `Remove ${material.name}?`,
      'If an assembly uses it, deactivate it instead.',
      () =>
        this.api.deleteMaterial(material.id).subscribe({
          next: () =>
            this.api.listMaterials({ pageSize: 200 }).subscribe((r) => this.materials.set(r.items)),
          error: (err: unknown) => this.showError(err),
        }),
    );
  }

  // --- Patterns -----------------------------------------------------------------------------------

  newPattern(): void {
    this.patternForm.reset({
      name: '', description: '', defaultWastePercentage: 10, active: true,
      sortOrder: this.patterns().length + 1,
    });
    this.editingId.set(null);
    this.editorKind.set('pattern');
  }

  editPattern(pattern: Pattern): void {
    this.patternForm.reset({
      name: pattern.name,
      description: pattern.description ?? '',
      defaultWastePercentage: pattern.defaultWastePercentage,
      active: pattern.active,
      sortOrder: pattern.sortOrder,
    });
    this.editingId.set(pattern.id);
    this.editorKind.set('pattern');
  }

  savePattern(): void {
    if (this.patternForm.invalid) {
      this.patternForm.markAllAsTouched();
      return;
    }

    const request = this.patternForm.getRawValue();
    const id = this.editingId();

    this.persist(
      id ? this.api.updatePattern(id, request) : this.api.createPattern(request),
      () => this.api.listPatterns().subscribe((r) => this.patterns.set(r)),
    );
  }

  deletePattern(pattern: Pattern): void {
    this.confirmDelete(`Remove the ${pattern.name} pattern?`, 'Surfaces using it keep their stored waste.', () =>
      this.api.deletePattern(pattern.id).subscribe({
        next: () => this.api.listPatterns().subscribe((r) => this.patterns.set(r)),
        error: (err: unknown) => this.showError(err),
      }),
    );
  }

  // --- Waste rules ----------------------------------------------------------------------------------

  newWasteRule(): void {
    this.wasteRuleForm.reset({
      name: '', patternId: null, surfaceType: null, tileMaterialType: null, roomType: null,
      wastePercentage: 10, priority: 10, active: true,
    });
    this.editingId.set(null);
    this.editorKind.set('wasteRule');
  }

  editWasteRule(rule: WasteRule): void {
    this.wasteRuleForm.reset({
      name: rule.name,
      patternId: rule.patternId,
      surfaceType: rule.surfaceType,
      tileMaterialType: rule.tileMaterialType,
      roomType: rule.roomType,
      wastePercentage: rule.wastePercentage,
      priority: rule.priority,
      active: rule.active,
    });
    this.editingId.set(rule.id);
    this.editorKind.set('wasteRule');
  }

  saveWasteRule(): void {
    if (this.wasteRuleForm.invalid) {
      this.wasteRuleForm.markAllAsTouched();
      return;
    }

    const request = this.wasteRuleForm.getRawValue() as SaveWasteRuleRequest;
    const id = this.editingId();

    this.persist(
      id ? this.api.updateWasteRule(id, request) : this.api.createWasteRule(request),
      () => this.api.listWasteRules().subscribe((r) => this.wasteRules.set(r)),
    );
  }

  deleteWasteRule(rule: WasteRule): void {
    this.confirmDelete(`Remove the rule "${rule.name}"?`, 'New calculations will fall back to the pattern default.', () =>
      this.api.deleteWasteRule(rule.id).subscribe({
        next: () => this.api.listWasteRules().subscribe((r) => this.wasteRules.set(r)),
        error: (err: unknown) => this.showError(err),
      }),
    );
  }

  // --- Labor rates -----------------------------------------------------------------------------------

  newLaborRate(): void {
    this.laborRateForm.reset({
      name: '', trade: 'Tile Setting', unit: 'SF', calculationMethod: 'UnitRate',
      rate: 0, productivity: null, description: '', active: true,
    });
    this.editingId.set(null);
    this.editorKind.set('laborRate');
  }

  editLaborRate(rate: LaborRate): void {
    this.laborRateForm.reset({
      name: rate.name,
      trade: rate.trade,
      unit: rate.unit,
      calculationMethod: rate.calculationMethod,
      rate: rate.rate,
      productivity: rate.productivity,
      description: rate.description ?? '',
      active: rate.active,
    });
    this.editingId.set(rate.id);
    this.editorKind.set('laborRate');
  }

  saveLaborRate(): void {
    if (this.laborRateForm.invalid) {
      this.laborRateForm.markAllAsTouched();
      return;
    }

    const request = this.laborRateForm.getRawValue() as SaveLaborRateRequest;
    const id = this.editingId();

    this.persist(
      id ? this.api.updateLaborRate(id, request) : this.api.createLaborRate(request),
      () => this.api.listLaborRates().subscribe((r) => this.laborRates.set(r)),
    );
  }

  deleteLaborRate(rate: LaborRate): void {
    this.confirmDelete(`Remove "${rate.name}"?`, 'If an assembly uses it, deactivate it instead.', () =>
      this.api.deleteLaborRate(rate.id).subscribe({
        next: () => this.api.listLaborRates().subscribe((r) => this.laborRates.set(r)),
        error: (err: unknown) => this.showError(err),
      }),
    );
  }

  /** How a waste rule's conditions read in the table. */
  ruleMatches(rule: WasteRule): string {
    const parts = [
      rule.patternName,
      rule.surfaceType,
      rule.tileMaterialType,
      rule.roomType,
    ].filter(Boolean);

    return parts.length > 0 ? parts.join(' · ') : 'Any surface';
  }

  // --- Shared helpers -----------------------------------------------------------------------------------

  private persist(request$: { subscribe: (o: object) => void }, reload: () => void): void {
    this.saving.set(true);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.closeEditor();
        reload();
        this.snackBar.open('Saved.', 'Dismiss', { duration: 3000 });
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.showError(err);
      },
    });
  }

  private confirmDelete(title: string, message: string, action: () => void): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: { title, message, confirmLabel: 'Remove', destructive: true },
      })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) action();
      });
  }

  private showError(err: unknown): void {
    this.snackBar.open(errorMessage(err, 'That could not be saved.'), 'Dismiss', { duration: 8000 });
  }
}
