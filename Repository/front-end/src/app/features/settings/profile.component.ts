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
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';

import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth/auth.service';
import { errorMessage } from '../../core/interceptors/error.interceptor';

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('newPassword')?.value as string | undefined;
  const confirm = group.get('confirmPassword')?.value as string | undefined;
  return password && confirm && password !== confirm ? { passwordMismatch: true } : null;
}

@Component({
  selector: 'te-profile',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <div class="te-page" style="max-width: 44rem">
      <header class="te-page__header">
        <div>
          <h1 class="te-page__title">My profile</h1>
          <p class="te-page__subtitle">{{ auth.displayName() }} · {{ auth.user()?.email }}</p>
        </div>
        <div class="te-page__actions">
          <a mat-stroked-button routerLink="/settings">Back to settings</a>
        </div>
      </header>

      <div class="te-card" style="margin-bottom: 1rem">
        <h2 class="te-card__title">Account</h2>
        <dl class="te-detail-list">
          <dt>Name</dt>
          <dd>{{ auth.displayName() }}</dd>
          <dt>Email</dt>
          <dd>
            {{ auth.user()?.email }}
            @if (!auth.user()?.emailConfirmed) {
              <span class="te-status te-status--warn" style="margin-left: 0.5rem">Not confirmed</span>
            }
          </dd>
          <dt>Organization</dt>
          <dd>{{ auth.user()?.activeOrganizationName }}</dd>
          <dt>Role</dt>
          <dd>{{ auth.user()?.roleName }}</dd>
        </dl>

        @if (!auth.user()?.emailConfirmed) {
          <button mat-stroked-button (click)="resendVerification()" [disabled]="resending()" style="margin-top: 1rem">
            <mat-icon>send</mat-icon>
            Resend the confirmation email
          </button>
        }
      </div>

      <div class="te-card">
        <h2 class="te-card__title">Change password</h2>
        <p class="te-muted" style="margin-top: -0.5rem; font-size: 0.8125rem">
          Changing your password signs out every other session.
        </p>

        <form [formGroup]="form" (ngSubmit)="changePassword()">
          <mat-form-field appearance="outline" style="width: 100%">
            <mat-label>Current password</mat-label>
            <input matInput type="password" formControlName="currentPassword" autocomplete="current-password" />
            @if (form.controls.currentPassword.touched && form.controls.currentPassword.invalid) {
              <mat-error>Enter your current password.</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline" style="width: 100%">
            <mat-label>New password</mat-label>
            <input matInput type="password" formControlName="newPassword" autocomplete="new-password" />
            @if (form.controls.newPassword.touched && form.controls.newPassword.invalid) {
              <mat-error>Use at least 10 characters.</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline" style="width: 100%">
            <mat-label>Confirm new password</mat-label>
            <input matInput type="password" formControlName="confirmPassword" autocomplete="new-password" />
            @if (form.controls.confirmPassword.touched && form.hasError('passwordMismatch')) {
              <mat-error>The passwords do not match.</mat-error>
            }
          </mat-form-field>

          <div class="te-form-actions">
            <button mat-flat-button color="primary" type="submit" [disabled]="saving()">
              @if (saving()) {
                <mat-spinner diameter="20" />
              } @else {
                Change password
              }
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: `
    .te-detail-list {
      display: grid;
      grid-template-columns: 9rem 1fr;
      gap: 0.5rem 1rem;
      margin: 0;

      dt {
        font-size: 0.8125rem;
        opacity: 0.65;
      }

      dd {
        margin: 0;
        font-size: 0.875rem;
      }
    }
  `,
})
export class ProfileComponent {
  private readonly fb = inject(FormBuilder);
  private readonly http = inject(HttpClient);
  private readonly snackBar = inject(MatSnackBar);

  readonly auth = inject(AuthService);

  readonly saving = signal(false);
  readonly resending = signal(false);

  readonly form = this.fb.nonNullable.group(
    {
      currentPassword: ['', Validators.required],
      newPassword: ['', [Validators.required, Validators.minLength(10)]],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatch },
  );

  changePassword(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);

    this.http
      .post(`${environment.apiBaseUrl}/auth/change-password`, this.form.getRawValue())
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.form.reset({ currentPassword: '', newPassword: '', confirmPassword: '' });
          this.snackBar.open(
            'Password changed. Other sessions have been signed out.',
            'Dismiss',
            { duration: 6000 },
          );
        },
        error: (err: unknown) => {
          this.saving.set(false);
          this.snackBar.open(errorMessage(err, 'The password could not be changed.'), 'Dismiss', {
            duration: 7000,
          });
        },
      });
  }

  resendVerification(): void {
    const email = this.auth.user()?.email;
    if (!email) return;

    this.resending.set(true);

    this.http.post(`${environment.apiBaseUrl}/auth/resend-verification`, { email }).subscribe({
      next: () => {
        this.resending.set(false);
        this.snackBar.open('Confirmation email sent.', 'Dismiss', { duration: 5000 });
      },
      error: () => {
        this.resending.set(false);
        this.snackBar.open('The confirmation email could not be sent.', 'Dismiss', { duration: 6000 });
      },
    });
  }
}
