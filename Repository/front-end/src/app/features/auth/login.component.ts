import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { errorMessage } from '../../core/interceptors/error.interceptor';

@Component({
  selector: 'te-login',
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
        <h1 class="te-auth__title">Sign in</h1>
        <p class="te-auth__subtitle">Estimates, quotes and approvals for tile contractors.</p>

        @if (error()) {
          <div class="te-auth__error" role="alert">
            <mat-icon class="te-inline-icon">error_outline</mat-icon>
            <span>{{ error() }}</span>
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="submit()">
          <mat-form-field appearance="outline" class="te-auth__full">
            <mat-label>Email</mat-label>
            <input matInput type="email" formControlName="email" autocomplete="username" />
            @if (form.controls.email.touched && form.controls.email.invalid) {
              <mat-error>Enter the email address you registered with.</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline" class="te-auth__full">
            <mat-label>Password</mat-label>
            <input
              matInput
              [type]="showPassword() ? 'text' : 'password'"
              formControlName="password"
              autocomplete="current-password"
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
              <mat-error>Enter your password.</mat-error>
            }
          </mat-form-field>

          <div class="te-auth__links">
            <a routerLink="/auth/forgot-password">Forgot your password?</a>
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
              Sign in
            }
          </button>
        </form>

        <p class="te-auth__footer">
          New here? <a routerLink="/auth/register">Create an account</a>
        </p>
      </div>
    </div>
  `,
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly showPassword = signal(false);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        // Return the person to wherever the guard interrupted them.
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';
        void this.router.navigateByUrl(returnUrl);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(errorMessage(err, 'That email address and password do not match an account.'));
      },
    });
  }
}
