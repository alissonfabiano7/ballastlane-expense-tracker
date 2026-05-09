import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface AuthUser {
  userId: string;
  email: string;
  expiresAtUtc?: string;
}

interface UserResponse {
  userId: string;
  email: string;
  expiresAtUtc: string;
}

interface MeResponse {
  userId: string;
  email: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly currentUser = signal<AuthUser | null>(null);

  readonly user = this.currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUser() !== null);

  async login(email: string, password: string): Promise<AuthUser> {
    const response = await firstValueFrom(
      this.http.post<UserResponse>('/auth/login', { email, password }),
    );
    const user: AuthUser = {
      userId: response.userId,
      email: response.email,
      expiresAtUtc: response.expiresAtUtc,
    };
    this.currentUser.set(user);
    await this.fetchCsrfToken();
    return user;
  }

  async register(email: string, password: string): Promise<AuthUser> {
    const response = await firstValueFrom(
      this.http.post<UserResponse>('/auth/register', { email, password }),
    );
    const user: AuthUser = {
      userId: response.userId,
      email: response.email,
      expiresAtUtc: response.expiresAtUtc,
    };
    this.currentUser.set(user);
    await this.fetchCsrfToken();
    return user;
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/auth/logout', {}));
    } finally {
      this.currentUser.set(null);
    }
  }

  async refresh(): Promise<AuthUser | null> {
    try {
      const me = await firstValueFrom(this.http.get<MeResponse>('/auth/me'));
      const user: AuthUser = { userId: me.userId, email: me.email };
      this.currentUser.set(user);
      await this.fetchCsrfToken();
      return user;
    } catch {
      this.currentUser.set(null);
      return null;
    }
  }

  async fetchCsrfToken(): Promise<void> {
    try {
      await firstValueFrom(this.http.get('/auth/csrf-token'));
    } catch {
      // CSRF bootstrap is best-effort; the cookie may already be set.
    }
  }
}
