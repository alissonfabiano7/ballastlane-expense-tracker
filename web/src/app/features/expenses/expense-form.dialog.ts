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
        <mat-form-field appearance="outline" floatLabel="always" class="full">
          <mat-label>Amount</mat-label>
          <input
            matInput
            type="text"
            inputmode="decimal"
            placeholder="0.00"
            autocomplete="off"
            [value]="amountDisplay()"
            (keydown)="onAmountKeyDown($event)"
            (paste)="onAmountPaste($event)"
            (blur)="onAmountBlur()"
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

  // Cash-register currency entry: digits append to the cents side of the
  // value (1, 2, 3, 4 → $0.01, $0.12, $1.23, $12.34); Backspace/Delete
  // remove the last cent; paste accepts any decimal-looking text and
  // normalizes to cents. Display always reads as "X.XX" so the decimal
  // separator is period regardless of browser locale, and the value is
  // locked to two decimal places by construction.

  private static readonly MaxAmountCents = 9_999_999_999; // $99,999,999.99

  private static readonly NavigationKeys = new Set<string>([
    'Tab', 'Escape', 'Enter',
    'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown',
    'Home', 'End',
  ]);

  protected amountDisplay(): string {
    const v = this.form.controls.amount.value;
    return v == null ? '' : v.toFixed(2);
  }

  protected onAmountKeyDown(event: KeyboardEvent): void {
    if (event.ctrlKey || event.metaKey || event.altKey) {
      return; // allow Ctrl+A, Cmd+C, etc.
    }
    if (ExpenseFormDialogComponent.NavigationKeys.has(event.key)) {
      return;
    }
    event.preventDefault();
    if (event.key === 'Backspace' || event.key === 'Delete') {
      this.removeLastCent();
      return;
    }
    if (/^\d$/.test(event.key)) {
      this.appendDigit(event.key);
    }
    // Other keys (letters, symbols) are swallowed by preventDefault above.
  }

  protected onAmountPaste(event: ClipboardEvent): void {
    event.preventDefault();
    const text = event.clipboardData?.getData('text') ?? '';
    const cents = ExpenseFormDialogComponent.parseClipboardToCents(text);
    if (cents === null || cents <= 0 || cents > ExpenseFormDialogComponent.MaxAmountCents) {
      return;
    }
    this.form.controls.amount.setValue(cents / 100);
    this.form.controls.amount.markAsDirty();
  }

  protected onAmountBlur(): void {
    // formControlName would have wired this automatically; we manage the
    // input manually so we have to mark the control touched ourselves.
    this.form.controls.amount.markAsTouched();
  }

  private appendDigit(digit: string): void {
    const current = this.form.controls.amount.value ?? 0;
    const cents = Math.round(current * 100);
    const newCents = cents * 10 + parseInt(digit, 10);
    if (newCents > ExpenseFormDialogComponent.MaxAmountCents) {
      return;
    }
    this.form.controls.amount.setValue(newCents === 0 ? null : newCents / 100);
    this.form.controls.amount.markAsDirty();
  }

  private removeLastCent(): void {
    const current = this.form.controls.amount.value;
    if (current == null || current === 0) {
      return;
    }
    const cents = Math.round(current * 100);
    const newCents = Math.floor(cents / 10);
    this.form.controls.amount.setValue(newCents === 0 ? null : newCents / 100);
    this.form.controls.amount.markAsDirty();
  }

  private static parseClipboardToCents(text: string): number | null {
    const clean = text.replace(/[^\d.,]/g, '');
    if (clean.length === 0) {
      return null;
    }
    const lastSep = Math.max(clean.lastIndexOf('.'), clean.lastIndexOf(','));
    let value: number;
    if (lastSep === -1) {
      value = parseFloat(clean);
    } else {
      const intPart = clean.slice(0, lastSep).replace(/[.,]/g, '');
      const decPart = clean.slice(lastSep + 1);
      value = parseFloat((intPart || '0') + '.' + decPart);
    }
    if (isNaN(value)) {
      return null;
    }
    return Math.round(value * 100);
  }

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
