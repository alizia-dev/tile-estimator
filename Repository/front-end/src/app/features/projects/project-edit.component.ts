import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { CustomerApi, ProjectApi } from '../../core/services/api.service';
import { errorMessage } from '../../core/interceptors/error.interceptor';
import type { Address, Customer } from '../../core/models/api.models';

@Component({
  selector: 'te-project-edit',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatAutocompleteModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <div class="te-page" style="max-width: 60rem">
      <header class="te-page__header">
        <div>
          <h1 class="te-page__title">{{ isNew() ? 'New project' : 'Edit project' }}</h1>
          @if (projectNumber()) {
            <p class="te-page__subtitle">{{ projectNumber() }}</p>
          }
        </div>
      </header>

      @if (loading()) {
        <div style="display: flex; justify-content: center; padding: 3rem">
          <mat-spinner diameter="36" />
        </div>
      } @else {
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="te-card" style="margin-bottom: 1rem">
            <h2 class="te-card__title">Project</h2>

            <div class="te-form-grid">
              <mat-form-field appearance="outline" class="te-field-full">
                <mat-label>Customer</mat-label>
                <mat-select formControlName="customerId">
                  @for (customer of customers(); track customer.id) {
                    <mat-option [value]="customer.id">
                      {{ customer.displayName }} ({{ customer.customerNumber }})
                    </mat-option>
                  }
                </mat-select>
                @if (form.controls.customerId.touched && form.controls.customerId.invalid) {
                  <mat-error>Every project belongs to a customer.</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline" class="te-field-full">
                <mat-label>Project name</mat-label>
                <input matInput formControlName="name" placeholder="e.g. Ellis master bathroom" />
                @if (form.controls.name.touched && form.controls.name.invalid) {
                  <mat-error>Give the project a name.</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Project type</mat-label>
                <mat-select formControlName="type">
                  <mat-option value="Residential">Residential</mat-option>
                  <mat-option value="Commercial">Commercial</mat-option>
                  <mat-option value="Remodel">Remodel</mat-option>
                  <mat-option value="NewConstruction">New construction</mat-option>
                  <mat-option value="Repair">Repair</mat-option>
                  <mat-option value="Other">Other</mat-option>
                </mat-select>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Start date</mat-label>
                <input matInput [matDatepicker]="startPicker" formControlName="startDate" />
                <mat-datepicker-toggle matIconSuffix [for]="startPicker" />
                <mat-datepicker #startPicker />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Estimated completion</mat-label>
                <input matInput [matDatepicker]="endPicker" formControlName="estimatedCompletionDate" />
                <mat-datepicker-toggle matIconSuffix [for]="endPicker" />
                <mat-datepicker #endPicker />
              </mat-form-field>

              <mat-form-field appearance="outline" class="te-field-full">
                <mat-label>Description</mat-label>
                <textarea matInput formControlName="description" rows="3"></textarea>
              </mat-form-field>
            </div>
          </div>

          <div class="te-card" style="margin-bottom: 1rem">
            <h2 class="te-card__title">Job site address</h2>
            <p class="te-muted" style="margin-top: -0.5rem; font-size: 0.8125rem">
              Leave blank to use the customer's service address.
            </p>

            <div class="te-form-grid" formGroupName="siteAddress">
              <mat-form-field appearance="outline" class="te-field-full">
                <mat-label>Street address</mat-label>
                <input matInput formControlName="line1" />
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>City</mat-label>
                <input matInput formControlName="city" />
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>State</mat-label>
                <input matInput formControlName="state" maxlength="2" style="text-transform: uppercase" />
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>ZIP</mat-label>
                <input matInput formControlName="postalCode" />
              </mat-form-field>
            </div>
          </div>

          <div class="te-form-actions">
            <a mat-stroked-button [routerLink]="cancelLink()">Cancel</a>
            <button mat-flat-button color="primary" type="submit" [disabled]="saving()">
              @if (saving()) {
                <mat-spinner diameter="20" />
              } @else {
                {{ isNew() ? 'Create project' : 'Save changes' }}
              }
            </button>
          </div>
        </form>
      }
    </div>
  `,
})
export class ProjectEditComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ProjectApi);
  private readonly customerApi = inject(CustomerApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly snackBar = inject(MatSnackBar);

  private readonly id = this.route.snapshot.paramMap.get('id');

  readonly isNew = signal(this.id === null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly projectNumber = signal<string | null>(null);
  readonly customers = signal<Customer[]>([]);

  readonly form = this.fb.nonNullable.group({
    customerId: ['', Validators.required],
    name: ['', Validators.required],
    type: ['Residential'],
    description: [''],
    startDate: [null as Date | null],
    estimatedCompletionDate: [null as Date | null],
    siteAddress: this.fb.nonNullable.group({
      line1: [''],
      city: [''],
      state: [''],
      postalCode: [''],
    }),
  });

  constructor() {
    // A contractor's customer list is short enough to load in one page for the picker.
    this.customerApi.list({ pageSize: 200 }).subscribe({
      next: (result) => {
        this.customers.set(result.items);

        // Pre-select when arriving from a customer page.
        const preselect = this.route.snapshot.queryParamMap.get('customerId');
        if (preselect && this.isNew()) {
          this.form.controls.customerId.setValue(preselect);
        }

        if (this.isNew()) {
          this.loading.set(false);
        }
      },
      error: () => this.loading.set(false),
    });

    if (this.id) {
      this.api.get(this.id).subscribe({
        next: (project) => {
          this.projectNumber.set(project.projectNumber);
          this.form.patchValue({
            customerId: project.customerId,
            name: project.name,
            type: project.type,
            description: project.description ?? '',
            startDate: project.startDate ? new Date(project.startDate) : null,
            estimatedCompletionDate: project.estimatedCompletionDate
              ? new Date(project.estimatedCompletionDate)
              : null,
            siteAddress: {
              line1: project.siteAddress?.line1 ?? '',
              city: project.siteAddress?.city ?? '',
              state: project.siteAddress?.state ?? '',
              postalCode: project.siteAddress?.postalCode ?? '',
            },
          });
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          void this.router.navigate(['/projects']);
        },
      });
    }
  }

  cancelLink(): string {
    return this.id ? `/projects/${this.id}` : '/projects';
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const address = value.siteAddress;

    const siteAddress: Address | null =
      address.line1 && address.city && address.state && address.postalCode
        ? {
            line1: address.line1,
            line2: null,
            city: address.city,
            state: address.state.toUpperCase(),
            postalCode: address.postalCode,
            country: 'US',
          }
        : null;

    const request = {
      customerId: value.customerId,
      name: value.name,
      description: value.description || null,
      type: value.type,
      siteAddress,
      // Dates are sent as plain ISO instants; the server stores and compares them in UTC.
      startDate: value.startDate?.toISOString() ?? null,
      estimatedCompletionDate: value.estimatedCompletionDate?.toISOString() ?? null,
    };

    this.saving.set(true);

    const request$ = this.id ? this.api.update(this.id, request) : this.api.create(request);

    request$.subscribe({
      next: (project) => {
        this.snackBar.open(
          this.isNew() ? `Project ${project.projectNumber} created.` : 'Project saved.',
          'Dismiss',
          { duration: 4000 },
        );
        void this.router.navigate(['/projects', project.id]);
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.snackBar.open(errorMessage(err, 'The project could not be saved.'), 'Dismiss', {
          duration: 7000,
        });
      },
    });
  }
}
