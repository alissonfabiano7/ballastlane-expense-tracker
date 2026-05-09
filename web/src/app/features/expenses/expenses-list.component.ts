import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-expenses-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p>Expenses list will appear here.</p>
  `,
})
export class ExpensesListComponent {}
