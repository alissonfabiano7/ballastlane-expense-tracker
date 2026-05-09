import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';

export interface ConfirmDeleteDialogData {
  description: string | null;
  amount: number;
}

@Component({
  selector: 'app-confirm-delete-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, CurrencyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Delete expense?</h2>
    <mat-dialog-content>
      <p>
        Are you sure you want to delete
        <strong>"{{ data.description ?? 'this expense' }}"</strong>
        ({{ data.amount | currency: 'USD' }})? This cannot be undone.
      </p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="dialogRef.close(false)">Cancel</button>
      <button mat-flat-button color="warn" (click)="dialogRef.close(true)">Delete</button>
    </mat-dialog-actions>
  `,
})
export class ConfirmDeleteDialogComponent {
  protected readonly dialogRef = inject(MatDialogRef<ConfirmDeleteDialogComponent, boolean>);
  protected readonly data = inject<ConfirmDeleteDialogData>(MAT_DIALOG_DATA);
}
