import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
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
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import {
  CatalogApi,
  EstimateApi,
  ProjectApi,
  QuoteApi,
  TakeoffApi,
} from '../../core/services/api.service';
import { errorMessage } from '../../core/interceptors/error.interceptor';
import type {
  Assembly,
  EstimateSummary,
  Pattern,
  Project,
  ProjectNote,
  QuoteSummary,
  Room,
  Surface,
  TakeoffResult,
  Tile,
} from '../../core/models/api.models';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog.component';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MoneyPipe, PercentagePipe, QuantityPipe, ShortDatePipe, StatusClassPipe, StatusLabelPipe } from '../../shared/pipes';
import { OpeningDialogComponent } from './opening-dialog.component';
import { SurfaceDialogComponent } from './surface-dialog.component';

const PROJECT_STATUSES = [
  'Draft', 'Estimating', 'EstimateReady', 'Quoted', 'Accepted',
  'Scheduled', 'InProgress', 'Completed', 'Cancelled', 'Closed',
] as const;

/**
 * SPEC 9 project page with its tabs: Overview, Rooms, Takeoff, Estimates, Quotes, Notes.
 *
 * The Takeoff tab is the one that matters most during data entry: it calls the server's engine
 * and shows the quantities together with the engine's own explanation of each number, so an
 * estimator can check the arithmetic rather than trust it.
 */
@Component({
  selector: 'te-project-detail',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatTabsModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTableModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDialogModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    HasPermissionDirective,
    MoneyPipe,
    PercentagePipe,
    QuantityPipe,
    ShortDatePipe,
    StatusClassPipe,
    StatusLabelPipe,
  ],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss',
})
export class ProjectDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ProjectApi);
  private readonly catalogApi = inject(CatalogApi);
  private readonly takeoffApi = inject(TakeoffApi);
  private readonly estimateApi = inject(EstimateApi);
  private readonly quoteApi = inject(QuoteApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly fb = inject(FormBuilder);

  readonly projectId = this.route.snapshot.paramMap.get('id')!;
  readonly statuses = PROJECT_STATUSES;

  readonly project = signal<Project | null>(null);
  readonly rooms = signal<Room[]>([]);
  readonly notes = signal<ProjectNote[]>([]);
  readonly estimates = signal<EstimateSummary[]>([]);
  readonly quotes = signal<QuoteSummary[]>([]);
  readonly takeoff = signal<TakeoffResult | null>(null);

  readonly tiles = signal<Tile[]>([]);
  readonly patterns = signal<Pattern[]>([]);
  readonly assemblies = signal<Assembly[]>([]);

  readonly loading = signal(true);
  readonly takeoffLoading = signal(false);
  readonly takeoffError = signal<string | null>(null);
  readonly creatingEstimate = signal(false);

  readonly noteForm = this.fb.nonNullable.group({
    body: ['', Validators.required],
  });

  readonly roomForm = this.fb.nonNullable.group({
    name: ['', Validators.required],
    type: ['Bathroom'],
  });

  readonly surfaceCount = computed(() =>
    this.rooms().reduce((total, room) => total + room.surfaces.length, 0),
  );

  readonly takeoffColumns = ['description', 'quantity', 'purchase', 'unitPrice', 'total'];

  constructor() {
    this.loadAll();
  }

  private loadAll(): void {
    this.loading.set(true);

    forkJoin({
      project: this.api.get(this.projectId),
      rooms: this.api.listRooms(this.projectId),
      notes: this.api.listNotes(this.projectId),
      estimates: this.estimateApi.list({ projectId: this.projectId, pageSize: 50 }),
      quotes: this.quoteApi.list({ projectId: this.projectId, pageSize: 50 }),
      tiles: this.catalogApi.listTiles({ pageSize: 200, active: true }),
      patterns: this.catalogApi.listPatterns(),
      assemblies: this.catalogApi.listAssemblies(),
    }).subscribe({
      next: (result) => {
        this.project.set(result.project);
        this.rooms.set(result.rooms);
        this.notes.set(result.notes);
        this.estimates.set(result.estimates.items);
        this.quotes.set(result.quotes.items);
        this.tiles.set(result.tiles.items);
        this.patterns.set(result.patterns);
        this.assemblies.set(result.assemblies);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        void this.router.navigate(['/projects']);
      },
    });
  }

  reloadRooms(): void {
    this.api.listRooms(this.projectId).subscribe((rooms) => {
      this.rooms.set(rooms);
      // Any structural change invalidates the takeoff on screen.
      this.takeoff.set(null);
    });
  }

  changeStatus(status: string): void {
    this.api.updateStatus(this.projectId, status).subscribe({
      next: (project) => {
        this.project.set(project);
        this.snackBar.open(`Status changed to ${status}.`, 'Dismiss', { duration: 3000 });
      },
    });
  }

  // --- Rooms ----------------------------------------------------------------------------------

  addRoom(): void {
    if (this.roomForm.invalid) {
      this.roomForm.markAllAsTouched();
      return;
    }

    const value = this.roomForm.getRawValue();

    this.api
      .createRoom(this.projectId, {
        name: value.name,
        type: value.type,
        customTypeName: value.type === 'Custom' ? value.name : null,
        sortOrder: this.rooms().length + 1,
      })
      .subscribe({
        next: () => {
          this.roomForm.reset({ name: '', type: 'Bathroom' });
          this.reloadRooms();
        },
        error: (err: unknown) =>
          this.snackBar.open(errorMessage(err, 'The room could not be added.'), 'Dismiss', {
            duration: 6000,
          }),
      });
  }

  deleteRoom(room: Room): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: `Delete ${room.name}?`,
          message:
            'The room and every surface and opening on it will be removed. Estimates already created keep their lines.',
          confirmLabel: 'Delete room',
          destructive: true,
        },
      })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) return;

        this.api.deleteRoom(this.projectId, room.id).subscribe({
          next: () => this.reloadRooms(),
        });
      });
  }

  // --- Surfaces --------------------------------------------------------------------------------

  addSurface(room: Room): void {
    this.openSurfaceDialog(room, null);
  }

  editSurface(room: Room, surface: Surface): void {
    this.openSurfaceDialog(room, surface);
  }

  private openSurfaceDialog(room: Room, surface: Surface | null): void {
    this.dialog
      .open(SurfaceDialogComponent, {
        width: '48rem',
        maxWidth: '95vw',
        data: {
          projectId: this.projectId,
          room,
          surface,
          tiles: this.tiles(),
          patterns: this.patterns(),
          assemblies: this.assemblies(),
        },
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.reloadRooms();
      });
  }

  deleteSurface(room: Room, surface: Surface): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: `Delete ${surface.name}?`,
          message: 'This surface and its openings will be removed from the project.',
          confirmLabel: 'Delete surface',
          destructive: true,
        },
      })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) return;

        this.api.deleteSurface(this.projectId, room.id, surface.id).subscribe({
          next: () => this.reloadRooms(),
        });
      });
  }

  addOpening(surface: Surface): void {
    this.dialog
      .open(OpeningDialogComponent, {
        width: '32rem',
        data: { projectId: this.projectId, surface, opening: null },
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.reloadRooms();
      });
  }

  deleteOpening(surface: Surface, openingId: string): void {
    this.api.deleteOpening(this.projectId, surface.id, openingId).subscribe({
      next: () => this.reloadRooms(),
    });
  }

  // --- Takeoff ----------------------------------------------------------------------------------

  runTakeoff(): void {
    this.takeoffLoading.set(true);
    this.takeoffError.set(null);

    this.takeoffApi.calculateProject(this.projectId).subscribe({
      next: (result) => {
        this.takeoff.set(result);
        this.takeoffLoading.set(false);
      },
      error: (err: unknown) => {
        this.takeoffLoading.set(false);
        this.takeoffError.set(
          errorMessage(err, 'The takeoff could not be calculated. Check the room measurements.'),
        );
      },
    });
  }

  // --- Estimates ----------------------------------------------------------------------------------

  createEstimate(): void {
    this.creatingEstimate.set(true);

    this.estimateApi.create(this.projectId).subscribe({
      next: (estimate) => {
        this.creatingEstimate.set(false);
        void this.router.navigate(['/estimates', estimate.id]);
      },
      error: (err: unknown) => {
        this.creatingEstimate.set(false);
        this.snackBar.open(
          errorMessage(err, 'The estimate could not be created. Add rooms and surfaces first.'),
          'Dismiss',
          { duration: 8000 },
        );
      },
    });
  }

  // --- Notes ------------------------------------------------------------------------------------

  addNote(): void {
    if (this.noteForm.invalid) return;

    this.api.addNote(this.projectId, this.noteForm.getRawValue().body).subscribe({
      next: (note) => {
        this.notes.update((notes) => [note, ...notes]);
        this.noteForm.reset({ body: '' });
      },
    });
  }

  /** How a surface's measurements read on the card, e.g. `12' x 15'` or `14' x 8' high`. */
  surfaceDimensions(surface: Surface): string {
    if (surface.areaOverrideSquareFeet) {
      return `${surface.areaOverrideSquareFeet} SF (entered directly)`;
    }

    if (surface.widthFeet) {
      return `${surface.lengthFeet}' x ${surface.widthFeet}'`;
    }

    if (surface.heightFeet) {
      return `${surface.lengthFeet}' x ${surface.heightFeet}' high`;
    }

    return `${surface.lengthFeet}'`;
  }
}
