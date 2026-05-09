import { Routes } from '@angular/router';
import { anonymousGuard, authGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    canMatch: [anonymousGuard],
    loadComponent: () =>
      import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    canMatch: [anonymousGuard],
    loadComponent: () =>
      import('./features/register/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: '',
    canMatch: [authGuard],
    loadComponent: () =>
      import('./features/layout/layout.component').then((m) => m.LayoutComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'expenses' },
      {
        path: 'expenses',
        loadComponent: () =>
          import('./features/expenses/expenses-list.component').then((m) => m.ExpensesListComponent),
      },
    ],
  },
  { path: '**', redirectTo: 'login' },
];
