import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
  type AbstractControl,
  type ValidationErrors,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { environment } from '../../../environments/environment';
import { errorMessage } from '../../core/interceptors/error.interceptor';

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('newPassword')?.value as string | undefined;
  const confirm = group.get('confirmPassword')?.value as string | undefined;
  return password && confirm && password !== confirm ? { passwordMismatch: true } : null;
}

@Component({
  selector: 'te-reset-password',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  styleUrl: './auth-shell.scss',
  template: `
    <div class="te-auth">
      <div class="te-auth__brand">
        <mat-icon>grid_view</mat-icon>
        <span>Tile Estimator</span>
      </div>

      <div class="te-auth__card">
        <h1 class="te-auth__title">Choose a new password</h1>

        @if (done()) {
          <div class="te-auth__notice" role="status">
            <mat-icon class="te-inline-icon">check_circle</mat-icon>
            <span>Your password has been changed. Every other session has been signed out.</span>
          </div>
          <p class="te-auth__footer"><a routerLink="/auth/login">Sign in</a></p>
        } @else {
          @if (error()) {
            <div class="te-auth__error" role="alert">
              <mat-icon class="te-inline-icon">error_outline</mat-icon>
              <span>{{ error() }}</span>
            </div>
          }

          <form [formGroup]="form" (ngSubmit)="submit()">
            <mat-form-field appearance="outline" class="te-auth__full">
              <mat-label>New password</mat-label>
              <input
                matInput
                [type]="showPassword() ? 'text' : 'password'"
                formControlName="newPassword"
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
              @if (form.controls.newPassword.touched && form.controls.newPassword.invalid) {
                <mat-error>Use at least 10 characters.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline" class="te-auth__full">
              <mat-label>Confirm new password</mat-label>
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
                Change password
              }
            </button>
          </form>
        }
      </div>
    </div>
  `,
})
export class ResetPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(false);
  readonly done = signal(false);
  readonly showPassword = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group(
    {
      newPassword: ['', [Validators.required, Validators.minLength(10)]],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatch },
  );

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // The token and email arrive in the link the person clicked.
    const params = this.route.snapshot.queryParamMap;
    const token = params.get('token');
    const email = params.get('email');

    if (!token || !email) {
      this.error.set('This reset link is incomplete. Request a new one.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.http
      .post(`${environment.apiBaseUrl}/auth/reset-password`, {
        token,
        email,
        ...this.form.getRawValue(),
      })
      .subscribe({
        next: () => {
          this.loading.set(false);
          this.done.set(true);
        },
        error: (err: unknown) => {
          this.loading.set(false);
          this.error.set(errorMessage(err, 'That reset link is not valid or has expired.'));
        },
      });
  }
}
