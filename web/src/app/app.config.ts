import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import {
  ErrorStateMatcher,
  ShowOnDirtyErrorStateMatcher,
  provideNativeDateAdapter,
} from '@angular/material/core';

import { routes } from './app.routes';
import { credentialsInterceptor } from './core/credentials.interceptor';
import { AuthService } from './core/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideAnimationsAsync(),
    provideNativeDateAdapter(),
    // Show form-field errors only when the user has typed something invalid
    // (dirty) OR has attempted to submit — never on a passive blur of a
    // still-empty field. See "Form fields surface validation error on blur"
    // in docs/genai/issues.md.
    { provide: ErrorStateMatcher, useClass: ShowOnDirtyErrorStateMatcher },
    provideHttpClient(withFetch(), withInterceptors([credentialsInterceptor])),
    provideRouter(routes, withComponentInputBinding()),
    provideAppInitializer(async () => {
      const auth = inject(AuthService);
      await auth.refresh();
    }),
  ],
};
