import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { environment } from '../../../environments/environment';
import { errorMessage } from '../../core/interceptors/error.interceptor';

type VerifyState = 'working' | 'done' | 'failed';

@Component({
  selector: 'te-verify-email',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  styleUrl: './auth-shell.scss',
  template: `
    <div class="te-auth">
      <div class="te-auth__brand">
        <mat-icon>grid_view</mat-icon>
        <span>Tile Estimator</span>
      </div>

      <div class="te-auth__card" style="text-align: center">
        @switch (state()) {
          @case ('working') {
            <mat-spinner diameter="40" style="margin: 1rem auto" />
            <p>Confirming your email address…</p>
          }
          @case ('done') {
            <mat-icon style="font-size: 3rem; width: 3rem; height: 3rem; color: #15633a">
              check_circle
            </mat-icon>
            <h1 class="te-auth__title">Email confirmed</h1>
            <p class="te-auth__subtitle">Your account is fully set up.</p>
            <a mat-flat-button color="primary" routerLink="/auth/login">Sign in</a>
          }
          @case ('failed') {
            <mat-icon style="font-size: 3rem; width: 3rem; height: 3rem; color: #9b1c1c">
              error_outline
            </mat-icon>
            <h1 class="te-auth__title">That link did not work</h1>
            <p class="te-auth__subtitle">{{ error() }}</p>
            <a mat-stroked-button routerLink="/auth/login">Back to sign in</a>
          }
        }
      </div>
    </div>
  `,
})
export class VerifyEmailComponent {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);

  readonly state = signal<VerifyState>('working');
  readonly error = signal('This confirmation link is not valid or has expired.');

  constructor() {
    const params = this.route.snapshot.queryParamMap;
    const token = params.get('token');
    const email = params.get('email');

    if (!token || !email) {
      this.state.set('failed');
      this.error.set('This confirmation link is incomplete. Ask for a new one from your account.');
      return;
    }

    this.http.post(`${environment.apiBaseUrl}/auth/verify-email`, { token, email }).subscribe({
      next: () => this.state.set('done'),
      error: (err: unknown) => {
        this.error.set(errorMessage(err, 'This confirmation link is not valid or has expired.'));
        this.state.set('failed');
      },
    });
  }
}
