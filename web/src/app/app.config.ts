import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { ErrorStateMatcher, provideNativeDateAdapter } from '@angular/material/core';

import { routes } from './app.routes';
import { credentialsInterceptor } from './core/credentials.interceptor';
import { AuthService } from './core/auth.service';
import { SubmittedErrorStateMatcher } from './core/error-state-matcher';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideAnimationsAsync(),
    provideNativeDateAdapter(),
    // Defer form-field error visibility entirely until the user clicks
    // submit. Typing is uninterrupted; errors surface together on the
    // first submit attempt and stay live thereafter. See "Form-field
    // errors still surfaced during typing" in docs/genai/issues.md.
    { provide: ErrorStateMatcher, useClass: SubmittedErrorStateMatcher },
    provideHttpClient(withFetch(), withInterceptors([credentialsInterceptor])),
    provideRouter(routes, withComponentInputBinding()),
    provideAppInitializer(async () => {
      const auth = inject(AuthService);
      await auth.refresh();
    }),
  ],
};
