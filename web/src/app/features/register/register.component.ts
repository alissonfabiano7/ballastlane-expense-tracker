import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { extractApiError, formatValidationErrors } from '../../core/api-error';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="auth-shell">
      <mat-card class="auth-card">
        <mat-card-header>
          <mat-card-title>Create your account</mat-card-title>
          <mat-card-subtitle>Start tracking your expenses</mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="onSubmit()" novalidate>
            <mat-form-field appearance="outline" class="full">
              <mat-label>Email</mat-label>
              <input
                matInput
                type="email"
                formControlName="email"
                autocomplete="email"
                required
              />
              @if (form.controls.email.invalid && form.controls.email.touched) {
                <mat-error>Please enter a valid email address.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline" class="full">
              <mat-label>Password</mat-label>
              <input
                matInput
                type="password"
                formControlName="password"
                autocomplete="new-password"
                required
              />
              <mat-hint>At least 8 characters, with letters and digits.</mat-hint>
              @if (form.controls.password.invalid && form.controls.password.touched) {
                <mat-error>Password must be at least 8 characters with letters and digits.</mat-error>
              }
            </mat-form-field>

            @if (errorMessage()) {
              <div class="error" role="alert">{{ errorMessage() }}</div>
            }

            <button
              mat-flat-button
              color="primary"
              type="submit"
              class="full"
              [disabled]="submitting() || form.invalid"
            >
              @if (submitting()) {
                <mat-progress-spinner diameter="20" mode="indeterminate"></mat-progress-spinner>
              } @else {
                <span>Create account</span>
              }
            </button>
          </form>
        </mat-card-content>
        <mat-card-actions align="end">
          <a mat-button routerLink="/login">Already have an account?</a>
        </mat-card-actions>
      </mat-card>
    </div>
  `,
  styles: [
    `
      .auth-shell {
        min-height: 100dvh;
        display: flex;
        align-items: center;
        justify-content: center;
        padding: 1rem;
        background-color: var(--mat-sys-surface-container);
      }
      .auth-card {
        width: 100%;
        max-width: 420px;
      }
      .full {
        width: 100%;
      }
      form {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
      }
      .error {
        background-color: var(--mat-sys-error-container);
        color: var(--mat-sys-on-error-container);
        padding: 0.75rem 1rem;
        border-radius: 6px;
        font-size: 0.9rem;
      }
      mat-progress-spinner {
        margin: 0 auto;
      }
    `,
  ],
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    // Validators.email follows the HTML5 spec (permits user@host with no
    // TLD); the extra pattern requires `local@domain.tld` so the client
    // catches obviously-malformed addresses before the server validator
    // does. See "Email validator accepted addresses without a TLD" in
    // docs/genai/issues.md.
    email: [
      '',
      [
        Validators.required,
        Validators.email,
        Validators.pattern(/^[^\s@]+@[^\s@]+\.[^\s@]+$/),
        Validators.maxLength(256),
      ],
    ],
    password: ['', [
      Validators.required,
      Validators.minLength(8),
      Validators.maxLength(100),
      Validators.pattern(/^(?=.*[A-Za-z])(?=.*\d).+$/),
    ]],
  });

  protected async onSubmit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.errorMessage.set(null);
    try {
      const { email, password } = this.form.getRawValue();
      await this.auth.register(email, password);
      await this.router.navigateByUrl('/expenses');
    } catch (error) {
      const problem = extractApiError(error);
      this.errorMessage.set(formatValidationErrors(problem));
    } finally {
      this.submitting.set(false);
    }
  }
}
