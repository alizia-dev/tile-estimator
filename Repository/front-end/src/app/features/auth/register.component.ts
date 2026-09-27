import { Component, inject, signal } from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
  type AbstractControl,
  type ValidationErrors,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { errorMessage } from '../../core/interceptors/error.interceptor';

/** Cross-field check that the two password boxes agree. */
function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value as string | undefined;
  const confirm = group.get('confirmPassword')?.value as string | undefined;
  return password && confirm && password !== confirm ? { passwordMismatch: true } : null;
}

@Component({
  selector: 'te-register',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatCheckboxModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  styleUrl: './auth-shell.scss',
  template: `
    <div class="te-auth te-auth--wide">
      <div class="te-auth__brand">
        <mat-icon>grid_view</mat-icon>
        <span>Tile Estimator</span>
      </div>

      <div class="te-auth__card">
        <h1 class="te-auth__title">Create your account</h1>
        <p class="te-auth__subtitle">
          This sets up your company, your catalog defaults and your estimating configuration.
        </p>

        @if (error()) {
          <div class="te-auth__error" role="alert">
            <mat-icon class="te-inline-icon">error_outline</mat-icon>
            <span>{{ error() }}</span>
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="submit()">
          <div class="te-form-grid">
            <mat-form-field appearance="outline">
              <mat-label>First name</mat-label>
              <input matInput formControlName="firstName" autocomplete="given-name" />
              @if (form.controls.firstName.touched && form.controls.firstName.invalid) {
                <mat-error>Your first name is required.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Last name</mat-label>
              <input matInput formControlName="lastName" autocomplete="family-name" />
              @if (form.controls.lastName.touched && form.controls.lastName.invalid) {
                <mat-error>Your last name is required.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline" class="te-field-full">
              <mat-label>Company name</mat-label>
              <input matInput formControlName="companyName" autocomplete="organization" />
              <mat-hint>This becomes your organization and appears on your quotes.</mat-hint>
              @if (form.controls.companyName.touched && form.controls.companyName.invalid) {
                <mat-error>Your company name is required.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline" class="te-field-full">
              <mat-label>Email</mat-label>
              <input matInput type="email" formControlName="email" autocomplete="username" />
              @if (form.controls.email.touched && form.controls.email.invalid) {
                <mat-error>Enter a valid email address.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Password</mat-label>
              <input
                matInput
                [type]="showPassword() ? 'text' : 'password'"
                formControlName="password"
                autocomplete="new-password"
              />
              <button
                mat-icon-button
                matSuffix
                type="button"
                (click)="showPassword.set(!showPassword())"
                [attr.aria-label]="showPassword() ? 'Hide password' : 'Show password'"
              >
                <mat-icon>{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
              </button>
              @if (form.controls.password.touched && form.controls.password.invalid) {
                <mat-error>Use at least 10 characters.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Confirm password</mat-label>
              <input
                matInput
                [type]="showPassword() ? 'text' : 'password'"
                formControlName="confirmPassword"
                autocomplete="new-password"
              />
              @if (form.controls.confirmPassword.touched && form.hasError('passwordMismatch')) {
                <mat-error>The passwords do not match.</mat-error>
              }
            </mat-form-field>
          </div>

          <div class="te-auth__terms">
            <mat-checkbox formControlName="acceptTerms">
              I accept the terms of service and privacy policy
            </mat-checkbox>
            @if (form.controls.acceptTerms.touched && form.controls.acceptTerms.invalid) {
              <p class="te-auth__hint" style="color: #9b1c1c">
                You need to accept the terms to create an account.
              </p>
            }
          </div>

          <button
            mat-flat-button
            color="primary"
            type="submit"
            class="te-auth__submit"
            [disabled]="loading()"
          >
            @if (loading()) {
              <mat-spinner diameter="20" />
            } @else {
              Create account
            }
          </button>
        </form>

        <p class="te-auth__footer">
          Already have an account? <a routerLink="/auth/login">Sign in</a>
        </p>
      </div>
    </div>
  `,
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly showPassword = signal(false);

  readonly form = this.fb.nonNullable.group(
    {
      firstName: ['', Validators.required],
      lastName: ['', Validators.required],
      companyName: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(10)]],
      confirmPassword: ['', Validators.required],
      acceptTerms: [false, Validators.requiredTrue],
    },
    { validators: passwordsMatch },
  );

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.auth.register(this.form.getRawValue()).subscribe({
      next: () => void this.router.navigate(['/dashboard']),
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(errorMessage(err, 'That account could not be created.'));
      },
    });
  }
}
