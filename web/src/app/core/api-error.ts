import { HttpErrorResponse } from '@angular/common/http';

export interface ProblemDetails {
  status?: number;
  title?: string;
  type?: string;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

export function extractApiError(error: unknown): ProblemDetails {
  if (error instanceof HttpErrorResponse) {
    if (error.error && typeof error.error === 'object' && 'title' in error.error) {
      return error.error as ProblemDetails;
    }
    return {
      status: error.status,
      title: error.statusText || 'Request failed.',
    };
  }
  return { title: 'Unexpected error.' };
}

export function formatValidationErrors(problem: ProblemDetails): string {
  if (!problem.errors) {
    return problem.title ?? 'Request failed.';
  }
  const messages = Object.values(problem.errors).flat();
  return messages.join(' ');
}
