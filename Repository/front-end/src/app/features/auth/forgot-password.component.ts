import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';

import { environment } from '../../../environments/environment';

@Component({
  selector: 'te-forgot-password',
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
        <h1 class="te-auth__title">Reset your password</h1>
        <p class="te-auth__subtitle">
          Enter your email address and we will send you a link to choose a new password.
        </p>

        @if (sent()) {
          <div class="te-auth__notice" role="status">
            <mat-icon class="te-inline-icon">mark_email_read</mat-icon>
            <span>
              If that address has an account, a reset link is on its way. The link expires in a
              couple of hours.
            </span>
          </div>

          <p class="te-auth__footer"><a routerLink="/auth/login">Back to sign in</a></p>
        } @else {
          <form [formGroup]="form" (ngSubmit)="submit()">
            <mat-form-field appearance="outline" class="te-auth__full">
              <mat-label>Email</mat-label>
              <input matInput type="email" formControlName="email" autocomplete="username" />
              @if (form.controls.email.touched && form.controls.email.invalid) {
                <mat-error>Enter a valid email address.</mat-error>
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
                Send reset link
              }
            </button>
          </form>

          <p class="te-auth__footer">
            Remembered it? <a routerLink="/auth/login">Sign in</a>
          </p>
        }
      </div>
    </div>
  `,
})
export class ForgotPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly http = inject(HttpClient);

  readonly loading = signal(false);
  readonly sent = signal(false);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);

    this.http
      .post(`${environment.apiBaseUrl}/auth/forgot-password`, this.form.getRawValue())
      .subscribe({
        // The API answers the same way whether or not the address is registered, and so do we:
        // this page must not become a way to find out who has an account.
        next: () => {
          this.loading.set(false);
          this.sent.set(true);
        },
        error: () => {
          this.loading.set(false);
          this.sent.set(true);
        },
      });
  }
}
