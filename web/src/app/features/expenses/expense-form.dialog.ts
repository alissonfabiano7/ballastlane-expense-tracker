import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import {
  EXPENSE_CATEGORIES,
  ExpenseCategory,
  ExpenseCommand,
  ExpenseDto,
  ExpensesService,
} from '../../core/expenses.service';
import { extractApiError, formatValidationErrors } from '../../core/api-error';

export interface ExpenseFormDialogData {
  expense: ExpenseDto | null;
}

@Component({
  selector: 'app-expense-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.expense ? 'Edit expense' : 'New expense' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" (ngSubmit)="onSubmit()" novalidate id="expenseForm">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Amount</mat-label>
          <input
            matInput
            type="number"
            inputmode="decimal"
            placeholder="0.00"
            min="0.01"
            step="0.01"
            formControlName="amount"
            required
          />
          <span matTextPrefix>$&nbsp;</span>
          @if (form.controls.amount.invalid && form.controls.amount.touched) {
            <mat-error>Amount must be greater than zero.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Category</mat-label>
          <mat-select formControlName="category" required>
            @for (cat of categories; track cat) {
              <mat-option [value]="cat">{{ cat }}</mat-option>
            }
          </mat-select>
          @if (form.controls.category.invalid && form.controls.category.touched) {
            <mat-error>Category is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Date incurred</mat-label>
          <input
            matInput
            [matDatepicker]="picker"
            formControlName="incurredAt"
            [max]="today"
            required
          />
          <mat-datepicker-toggle matIconSuffix [for]="picker"></mat-datepicker-toggle>
          <mat-datepicker #picker></mat-datepicker>
          @if (form.controls.incurredAt.invalid && form.controls.incurredAt.touched) {
            <mat-error>A valid date in the past or today is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Description</mat-label>
          <textarea
            matInput
            formControlName="description"
            rows="2"
            maxlength="500"
          ></textarea>
          <mat-hint align="end">
            {{ form.controls.description.value.length }}/500
          </mat-hint>
          @if (form.controls.description.invalid && form.controls.description.touched) {
            <mat-error>Description must be at most 500 characters.</mat-error>
          }
        </mat-form-field>

        @if (errorMessage()) {
          <div class="error" role="alert">{{ errorMessage() }}</div>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="onCancel()" [disabled]="submitting()">
        Cancel
      </button>
      <button
        mat-flat-button
        color="primary"
        type="submit"
        form="expenseForm"
        [disabled]="submitting() || form.invalid"
      >
        @if (submitting()) {
          <mat-progress-spinner diameter="20" mode="indeterminate"></mat-progress-spinner>
        } @else {
          <span>{{ data.expense ? 'Save' : 'Create' }}</span>
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      :host {
        display: block;
        min-width: min(420px, 90vw);
      }
      .full {
        width: 100%;
      }
      form {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        padding-top: 0.5rem;
      }
      .error {
        margin-top: 0.5rem;
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
export class ExpenseFormDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly expensesService = inject(ExpensesService);
  private readonly dialogRef = inject(MatDialogRef<ExpenseFormDialogComponent, ExpenseDto>);
  protected readonly data = inject<ExpenseFormDialogData>(MAT_DIALOG_DATA);

  protected readonly categories = EXPENSE_CATEGORIES;
  protected readonly today = new Date();
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    // Nullable so the input shows empty (with placeholder) on create instead
    // of the literal '0' that prepends to typed digits — see issue card
    // "Amount input retains '0' default" in docs/genai/issues.md.
    amount: new FormControl<number | null>(
      this.data.expense?.amount ?? null,
      { validators: [Validators.required, Validators.min(0.01)] },
    ),
    category: [
      (this.data.expense?.category ?? 'Food') as ExpenseCategory,
      [Validators.required],
    ],
    incurredAt: [
      this.data.expense ? new Date(this.data.expense.incurredAt) : new Date(),
      [Validators.required],
    ],
    description: [
      this.data.expense?.description ?? '',
      [Validators.maxLength(500)],
    ],
  });

  protected onCancel(): void {
    this.dialogRef.close();
  }

  protected async onSubmit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.errorMessage.set(null);
    try {
      const value = this.form.getRawValue();
      if (value.amount === null) {
        // Unreachable: Validators.required would have flagged the form as invalid
        // and the early return above would have fired. Guard kept for TS narrowing.
        return;
      }
      const command: ExpenseCommand = {
        amount: value.amount,
        description: value.description.trim().length > 0 ? value.description.trim() : null,
        category: value.category,
        incurredAt: value.incurredAt.toISOString(),
      };
      const saved = this.data.expense
        ? await this.expensesService.update(this.data.expense.id, command)
        : await this.expensesService.create(command);
      this.dialogRef.close(saved);
    } catch (error) {
      const problem = extractApiError(error);
      this.errorMessage.set(formatValidationErrors(problem));
    } finally {
      this.submitting.set(false);
    }
  }
}
