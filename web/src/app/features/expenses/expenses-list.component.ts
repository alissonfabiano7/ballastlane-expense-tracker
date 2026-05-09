import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';
import { ExpenseDto, ExpensesService, PagedResult } from '../../core/expenses.service';
import { extractApiError, formatValidationErrors } from '../../core/api-error';
import {
  ConfirmDeleteDialogComponent,
  ConfirmDeleteDialogData,
} from './confirm-delete.dialog';
import { ExpenseFormDialogComponent, ExpenseFormDialogData } from './expense-form.dialog';

@Component({
  selector: 'app-expenses-list',
  standalone: true,
  imports: [
    CommonModule,
    CurrencyPipe,
    DatePipe,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatMenuModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page">
      <header class="page-header">
        <div>
          <h1>Expenses</h1>
          <p class="subtitle">
            @if (totalCount() > 0) {
              {{ totalCount() }} total entries
            } @else if (!loading()) {
              No entries yet
            }
          </p>
        </div>
        <button
          mat-flat-button
          color="primary"
          (click)="onCreate()"
          [disabled]="loading()"
        >
          <mat-icon>add</mat-icon>
          <span>New expense</span>
        </button>
      </header>

      @if (loading()) {
        <mat-progress-bar mode="indeterminate" aria-label="Loading expenses"></mat-progress-bar>
      }

      @if (errorMessage(); as msg) {
        <div class="error" role="alert">
          <mat-icon>error</mat-icon>
          <div class="error-body">
            <strong>Could not load expenses.</strong>
            <span>{{ msg }}</span>
          </div>
          <button mat-button (click)="load()">Retry</button>
        </div>
      } @else if (!loading() && expenses().length === 0) {
        <mat-card class="empty">
          <mat-card-content>
            <mat-icon class="empty-icon">receipt_long</mat-icon>
            <h2>No expenses yet</h2>
            <p>Add your first expense to start tracking your spending.</p>
            <button mat-flat-button color="primary" (click)="onCreate()">
              <mat-icon>add</mat-icon>
              <span>Add your first expense</span>
            </button>
          </mat-card-content>
        </mat-card>
      } @else {
        <div class="cards" role="list">
          @for (expense of expenses(); track expense.id) {
            <mat-card class="expense-card" role="listitem">
              <div class="card-row">
                <div class="amount">{{ expense.amount | currency: 'USD' }}</div>
                <button
                  mat-icon-button
                  [matMenuTriggerFor]="actionMenu"
                  aria-label="Expense actions"
                >
                  <mat-icon>more_vert</mat-icon>
                </button>
                <mat-menu #actionMenu="matMenu">
                  <button mat-menu-item (click)="onEdit(expense)">
                    <mat-icon>edit</mat-icon>
                    <span>Edit</span>
                  </button>
                  <button mat-menu-item (click)="onDelete(expense)">
                    <mat-icon color="warn">delete</mat-icon>
                    <span>Delete</span>
                  </button>
                </mat-menu>
              </div>
              <div class="card-row meta">
                <mat-chip [class]="'cat cat-' + expense.category.toLowerCase()" disableRipple>
                  {{ expense.category }}
                </mat-chip>
                <span class="date">{{ expense.incurredAt | date: 'mediumDate' }}</span>
              </div>
              @if (expense.description) {
                <p class="description">{{ expense.description }}</p>
              }
            </mat-card>
          }
        </div>

        <mat-paginator
          [length]="totalCount()"
          [pageIndex]="page() - 1"
          [pageSize]="pageSize()"
          [pageSizeOptions]="[5, 10, 25, 50]"
          (page)="onPageChange($event)"
          aria-label="Select expenses page"
        ></mat-paginator>
      }
    </section>
  `,
  styles: [
    `
      .page {
        display: flex;
        flex-direction: column;
        gap: 1.25rem;
      }
      .page-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 1rem;
        flex-wrap: wrap;
      }
      .page-header h1 {
        margin: 0;
        font-size: 1.75rem;
        font-weight: 500;
      }
      .subtitle {
        margin: 0.25rem 0 0;
        color: var(--mat-sys-on-surface-variant);
        font-size: 0.95rem;
      }
      .error {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        background-color: var(--mat-sys-error-container);
        color: var(--mat-sys-on-error-container);
        padding: 0.75rem 1rem;
        border-radius: 8px;
      }
      .error-body {
        flex: 1;
        display: flex;
        flex-direction: column;
      }
      .empty {
        text-align: center;
        padding: 2rem 1rem;
      }
      .empty-icon {
        font-size: 4rem;
        width: 4rem;
        height: 4rem;
        color: var(--mat-sys-primary);
        opacity: 0.5;
      }
      .empty h2 {
        margin: 1rem 0 0.25rem;
        font-weight: 500;
      }
      .empty p {
        color: var(--mat-sys-on-surface-variant);
        margin: 0 0 1.25rem;
      }
      .cards {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
        gap: 1rem;
      }
      .expense-card {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
        padding: 1rem;
      }
      .card-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 0.5rem;
      }
      .amount {
        font-size: 1.4rem;
        font-weight: 500;
        color: var(--mat-sys-on-surface);
      }
      .meta {
        gap: 0.75rem;
      }
      .date {
        color: var(--mat-sys-on-surface-variant);
        font-size: 0.9rem;
      }
      .description {
        margin: 0;
        color: var(--mat-sys-on-surface-variant);
        font-size: 0.95rem;
        white-space: pre-wrap;
        word-break: break-word;
      }
      mat-chip.cat {
        font-size: 0.8rem;
        font-weight: 500;
      }
      .cat-food {
        --mdc-chip-elevated-container-color: #e8f5e9;
        color: #2e7d32;
      }
      .cat-transport {
        --mdc-chip-elevated-container-color: #e3f2fd;
        color: #1565c0;
      }
      .cat-housing {
        --mdc-chip-elevated-container-color: #fff3e0;
        color: #e65100;
      }
      .cat-leisure {
        --mdc-chip-elevated-container-color: #fce4ec;
        color: #c2185b;
      }
      .cat-health {
        --mdc-chip-elevated-container-color: #f3e5f5;
        color: #6a1b9a;
      }
      .cat-education {
        --mdc-chip-elevated-container-color: #e0f7fa;
        color: #00838f;
      }
      .cat-other {
        --mdc-chip-elevated-container-color: #eceff1;
        color: #455a64;
      }
    `,
  ],
})
export class ExpensesListComponent implements OnInit {
  private readonly expensesService = inject(ExpensesService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly expenses = signal<ExpenseDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(10);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const result: PagedResult<ExpenseDto> = await this.expensesService.list(
        this.page(),
        this.pageSize(),
      );
      this.expenses.set(result.items);
      this.totalCount.set(result.totalCount);
    } catch (error) {
      const problem = extractApiError(error);
      this.errorMessage.set(formatValidationErrors(problem));
    } finally {
      this.loading.set(false);
    }
  }

  onPageChange(event: PageEvent): void {
    this.page.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
    void this.load();
  }

  async onCreate(): Promise<void> {
    const ref = this.dialog.open<
      ExpenseFormDialogComponent,
      ExpenseFormDialogData,
      ExpenseDto
    >(ExpenseFormDialogComponent, { data: { expense: null } });

    const result = await firstValueFrom(ref.afterClosed());
    if (result) {
      this.snackBar.open('Expense created.', 'Dismiss', { duration: 3000 });
      this.page.set(1);
      void this.load();
    }
  }

  async onEdit(expense: ExpenseDto): Promise<void> {
    const ref = this.dialog.open<
      ExpenseFormDialogComponent,
      ExpenseFormDialogData,
      ExpenseDto
    >(ExpenseFormDialogComponent, { data: { expense } });

    const result = await firstValueFrom(ref.afterClosed());
    if (result) {
      this.snackBar.open('Expense updated.', 'Dismiss', { duration: 3000 });
      void this.load();
    }
  }

  async onDelete(expense: ExpenseDto): Promise<void> {
    const ref = this.dialog.open<
      ConfirmDeleteDialogComponent,
      ConfirmDeleteDialogData,
      boolean
    >(ConfirmDeleteDialogComponent, {
      data: { description: expense.description, amount: expense.amount },
    });

    const confirmed = await firstValueFrom(ref.afterClosed());
    if (!confirmed) {
      return;
    }
    try {
      await this.expensesService.delete(expense.id);
      this.snackBar.open('Expense deleted.', 'Dismiss', { duration: 3000 });
      // If the deleted item was the only one on the current page, step back.
      if (this.expenses().length === 1 && this.page() > 1) {
        this.page.update((p) => p - 1);
      }
      void this.load();
    } catch (error) {
      const problem = extractApiError(error);
      this.snackBar.open(
        `Could not delete: ${formatValidationErrors(problem)}`,
        'Dismiss',
        { duration: 5000 },
      );
    }
  }
}
