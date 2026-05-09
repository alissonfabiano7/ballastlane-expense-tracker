import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export const EXPENSE_CATEGORIES = [
  'Food',
  'Transport',
  'Housing',
  'Leisure',
  'Health',
  'Education',
  'Other',
] as const;

export type ExpenseCategory = (typeof EXPENSE_CATEGORIES)[number];

export interface ExpenseDto {
  id: string;
  amount: number;
  description: string | null;
  category: ExpenseCategory;
  incurredAt: string;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ExpenseCommand {
  amount: number;
  description: string | null;
  category: ExpenseCategory;
  incurredAt: string;
}

@Injectable({ providedIn: 'root' })
export class ExpensesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/expenses';

  list(page = 1, pageSize = 10): Promise<PagedResult<ExpenseDto>> {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return firstValueFrom(this.http.get<PagedResult<ExpenseDto>>(this.baseUrl, { params }));
  }

  getById(id: string): Promise<ExpenseDto> {
    return firstValueFrom(this.http.get<ExpenseDto>(`${this.baseUrl}/${id}`));
  }

  create(command: ExpenseCommand): Promise<ExpenseDto> {
    return firstValueFrom(this.http.post<ExpenseDto>(this.baseUrl, command));
  }

  update(id: string, command: ExpenseCommand): Promise<ExpenseDto> {
    return firstValueFrom(this.http.put<ExpenseDto>(`${this.baseUrl}/${id}`, command));
  }

  delete(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.baseUrl}/${id}`));
  }
}
