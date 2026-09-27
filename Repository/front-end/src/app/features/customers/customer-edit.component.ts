import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { CustomerApi } from '../../core/services/api.service';
import { errorMessage } from '../../core/interceptors/error.interceptor';
import type {
  Address,
  CustomerStatus,
  CustomerType,
  SaveCustomerRequest,
} from '../../core/models/api.models';

/**
 * Create or edit a customer. The same form does both: the route parameter decides which, so
 * there is one layout and one set of validation rules to keep right.
 */
@Component({
  selector: 'te-customer-edit',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <div class="te-page" style="max-width: 60rem">
      <header class="te-page__header">
        <div>
          <h1 class="te-page__title">{{ isNew() ? 'New customer' : 'Edit customer' }}</h1>
          @if (customerNumber()) {
            <p class="te-page__subtitle">{{ customerNumber() }}</p>
          }
        </div>
        <div class="te-page__actions">
          <a mat-stroked-button routerLink="/customers">Cancel</a>
        </div>
      </header>

      @if (loading()) {
        <div style="display: flex; justify-content: center; padding: 3rem">
          <mat-spinner diameter="36" />
        </div>
      } @else {
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="te-card" style="margin-bottom: 1rem">
            <h2 class="te-card__title">Details</h2>

            <div class="te-form-grid">
              <mat-form-field appearance="outline">
                <mat-label>Customer type</mat-label>
                <mat-select formControlName="type">
                  <mat-option value="Residential">Residential</mat-option>
                  <mat-option value="Commercial">Commercial</mat-option>
                </mat-select>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Status</mat-label>
                <mat-select formControlName="status">
                  <mat-option value="Active">Active</mat-option>
                  <mat-option value="Prospect">Prospect</mat-option>
                  <mat-option value="Inactive">Inactive</mat-option>
                </mat-select>
              </mat-form-field>

              @if (form.controls.type.value === 'Commercial') {
                <mat-form-field appearance="outline" class="te-field-full">
                  <mat-label>Company name</mat-label>
                  <input matInput formControlName="companyName" />
                  @if (form.controls.companyName.touched && form.controls.companyName.invalid) {
                    <mat-error>A commercial customer needs a company name.</mat-error>
                  }
                </mat-form-field>
              }

              <mat-form-field appearance="outline">
                <mat-label>First name</mat-label>
                <input matInput formControlName="firstName" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Last name</mat-label>
                <input matInput formControlName="lastName" />
              </mat-form-field>

              @if (form.controls.type.value === 'Residential') {
                <mat-form-field appearance="outline" class="te-field-full">
                  <mat-label>Company name (optional)</mat-label>
                  <input matInput formControlName="companyName" />
                </mat-form-field>
              }

              <mat-form-field appearance="outline">
                <mat-label>Email</mat-label>
                <input matInput type="email" formControlName="email" />
                <mat-hint>Quotes are sent here.</mat-hint>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Phone</mat-label>
                <input matInput formControlName="phone" />
              </mat-form-field>

              <mat-form-field appearance="outline" class="te-field-full">
                <mat-label>Notes</mat-label>
                <textarea matInput formControlName="notes" rows="3"></textarea>
              </mat-form-field>
            </div>
          </div>

          <div class="te-card" style="margin-bottom: 1rem">
            <h2 class="te-card__title">Service address</h2>
            <div class="te-form-grid" formGroupName="serviceAddress">
              <mat-form-field appearance="outline" class="te-field-full">
                <mat-label>Street address</mat-label>
                <input matInput formControlName="line1" />
              </mat-form-field>
              <mat-form-field appearance="outline" class="te-field-full">
                <mat-label>Line 2</mat-label>
                <input matInput formControlName="line2" />
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

            <mat-checkbox [checked]="sameBilling()" (change)="sameBilling.set($event.checked)">
              Billing address is the same
            </mat-checkbox>
          </div>

          @if (!sameBilling()) {
            <div class="te-card" style="margin-bottom: 1rem">
              <h2 class="te-card__title">Billing address</h2>
              <div class="te-form-grid" formGroupName="billingAddress">
                <mat-form-field appearance="outline" class="te-field-full">
                  <mat-label>Street address</mat-label>
                  <input matInput formControlName="line1" />
                </mat-form-field>
                <mat-form-field appearance="outline" class="te-field-full">
                  <mat-label>Line 2</mat-label>
                  <input matInput formControlName="line2" />
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
          }

          <div class="te-form-actions">
            <a mat-stroked-button routerLink="/customers">Cancel</a>
            <button mat-flat-button color="primary" type="submit" [disabled]="saving()">
              @if (saving()) {
                <mat-spinner diameter="20" />
              } @else {
                {{ isNew() ? 'Create customer' : 'Save changes' }}
              }
            </button>
          </div>
        </form>
      }
    </div>
  `,
})
export class CustomerEditComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(CustomerApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly snackBar = inject(MatSnackBar);

  private readonly id = this.route.snapshot.paramMap.get('id');

  readonly isNew = signal(this.id === null);
  readonly loading = signal(this.id !== null);
  readonly saving = signal(false);
  readonly sameBilling = signal(true);
  readonly customerNumber = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    type: ['Residential' as CustomerType],
    status: ['Active' as CustomerStatus],
    firstName: [''],
    lastName: [''],
    companyName: [''],
    email: ['', Validators.email],
    phone: [''],
    notes: [''],
    serviceAddress: this.addressGroup(),
    billingAddress: this.addressGroup(),
  });

  constructor() {
    if (this.id) {
      this.api.get(this.id).subscribe({
        next: (customer) => {
          this.customerNumber.set(customer.customerNumber);
          this.form.patchValue({
            type: customer.type,
            status: customer.status,
            firstName: customer.firstName ?? '',
            lastName: customer.lastName ?? '',
            companyName: customer.companyName ?? '',
            email: customer.email ?? '',
            phone: customer.phone ?? '',
            notes: customer.notes ?? '',
            serviceAddress: toFormAddress(customer.serviceAddress),
            billingAddress: toFormAddress(customer.billingAddress),
          });

          this.sameBilling.set(
            customer.billingAddress?.line1 === customer.serviceAddress?.line1 &&
              customer.billingAddress?.postalCode === customer.serviceAddress?.postalCode,
          );

          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          void this.router.navigate(['/customers']);
        },
      });
    }

    // A commercial customer must have a company name; a residential one need not.
    this.form.controls.type.valueChanges.subscribe((type) => {
      const control = this.form.controls.companyName;
      if (type === 'Commercial') {
        control.addValidators(Validators.required);
      } else {
        control.removeValidators(Validators.required);
      }
      control.updateValueAndValidity();
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const serviceAddress = fromFormAddress(value.serviceAddress);

    const request: SaveCustomerRequest = {
      type: value.type,
      status: value.status,
      firstName: value.firstName || null,
      lastName: value.lastName || null,
      companyName: value.companyName || null,
      email: value.email || null,
      phone: value.phone || null,
      notes: value.notes || null,
      serviceAddress,
      billingAddress: this.sameBilling() ? serviceAddress : fromFormAddress(value.billingAddress),
    };

    this.saving.set(true);

    const request$ = this.id
      ? this.api.update(this.id, request)
      : this.api.create(request);

    request$.subscribe({
      next: (customer) => {
        this.snackBar.open(
          this.isNew() ? `Customer ${customer.customerNumber} created.` : 'Customer saved.',
          'Dismiss',
          { duration: 4000 },
        );
        void this.router.navigate(['/customers']);
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.snackBar.open(errorMessage(err, 'The customer could not be saved.'), 'Dismiss', {
          duration: 7000,
        });
      },
    });
  }

  private addressGroup() {
    return this.fb.nonNullable.group({
      line1: [''],
      line2: [''],
      city: [''],
      state: [''],
      postalCode: [''],
    });
  }
}

function toFormAddress(address: Address | null | undefined) {
  return {
    line1: address?.line1 ?? '',
    line2: address?.line2 ?? '',
    city: address?.city ?? '',
    state: address?.state ?? '',
    postalCode: address?.postalCode ?? '',
  };
}

/** An address is all-or-nothing: a partially filled one is sent as null rather than rejected. */
function fromFormAddress(value: {
  line1: string;
  line2: string;
  city: string;
  state: string;
  postalCode: string;
}): Address | null {
  if (!value.line1 || !value.city || !value.state || !value.postalCode) {
    return null;
  }

  return {
    line1: value.line1,
    line2: value.line2 || null,
    city: value.city,
    state: value.state.toUpperCase(),
    postalCode: value.postalCode,
    country: 'US',
  };
}
