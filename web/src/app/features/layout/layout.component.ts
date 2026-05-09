import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, MatToolbarModule, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="layout">
      <mat-toolbar color="primary" class="toolbar">
        <span class="brand">
          <mat-icon>account_balance_wallet</mat-icon>
          BallastLane Expenses
        </span>
        <span class="spacer"></span>
        @if (auth.user(); as user) {
          <span class="user-email" aria-label="Signed in as">{{ user.email }}</span>
          <button mat-button (click)="onLogout()" aria-label="Sign out">
            <mat-icon>logout</mat-icon>
            <span class="button-label">Sign out</span>
          </button>
        }
      </mat-toolbar>

      <main class="content">
        <router-outlet />
      </main>
    </div>
  `,
  styles: [
    `
      .layout {
        min-height: 100dvh;
        display: flex;
        flex-direction: column;
        background-color: var(--mat-sys-surface);
      }
      .toolbar {
        position: sticky;
        top: 0;
        z-index: 10;
        gap: 0.5rem;
      }
      .brand {
        display: inline-flex;
        align-items: center;
        gap: 0.5rem;
        font-weight: 500;
      }
      .spacer {
        flex: 1 1 auto;
      }
      .user-email {
        font-size: 0.9rem;
        opacity: 0.85;
        margin-right: 0.5rem;
      }
      .button-label {
        margin-left: 0.25rem;
      }
      .content {
        flex: 1 1 auto;
        padding: 1.5rem;
        max-width: 1100px;
        margin: 0 auto;
        width: 100%;
        box-sizing: border-box;
      }
      @media (max-width: 600px) {
        .user-email {
          display: none;
        }
        .button-label {
          display: none;
        }
        .content {
          padding: 1rem;
        }
      }
    `,
  ],
})
export class LayoutComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected async onLogout(): Promise<void> {
    await this.auth.logout();
    await this.router.navigateByUrl('/login');
  }
}
