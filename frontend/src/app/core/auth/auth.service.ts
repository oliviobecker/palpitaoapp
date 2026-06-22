import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, finalize, map, shareReplay, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_AUTH_REFRESH, SKIP_ERROR_TOAST } from '../interceptors/http-context';
import { UserRole } from '../models/enums';
import { LoginResponse, User } from '../models/models';
import { TokenStorageService } from './token-storage.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly storage = inject(TokenStorageService);

  private readonly _currentUser = signal<User | null>(this.storage.getUser());

  /** In-flight refresh, shared so concurrent 401s trigger a single refresh call. */
  private refresh$: Observable<string> | null = null;

  readonly currentUser = this._currentUser.asReadonly();
  readonly isAuthenticated = computed(
    () => this._currentUser() !== null && this.storage.getToken() !== null,
  );
  readonly isAdmin = computed(() => this._currentUser()?.role === UserRole.Admin);

  get token(): string | null {
    return this.storage.getToken();
  }

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(
        `${environment.apiBaseUrl}/auth/login`,
        { email, password },
        { context: skipRefreshAndToast() },
      )
      .pipe(tap((response) => this.storeSession(response)));
  }

  /**
   * Exchanges the stored refresh token for a new session. Shared via shareReplay so
   * multiple requests failing with 401 at once only fire one refresh; resolves to the
   * new access token. Errors (no/invalid refresh token) propagate to the caller.
   */
  refreshToken(): Observable<string> {
    if (this.refresh$) {
      return this.refresh$;
    }

    const refreshToken = this.storage.getRefreshToken();
    if (!refreshToken) {
      return throwError(() => new Error('No refresh token'));
    }

    this.refresh$ = this.http
      .post<LoginResponse>(
        `${environment.apiBaseUrl}/auth/refresh`,
        { refreshToken },
        { context: skipRefreshAndToast() },
      )
      .pipe(
        tap((response) => this.storeSession(response)),
        map((response) => response.token),
        shareReplay(1),
        finalize(() => (this.refresh$ = null)),
      );
    return this.refresh$;
  }

  /** Public self-registration into a group. Creates a pending request; does not authenticate. */
  register(payload: {
    name: string;
    email: string;
    password: string;
    confirmPassword: string;
    groupId: string;
  }): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${environment.apiBaseUrl}/auth/register`, payload, {
      context: new HttpContext().set(SKIP_ERROR_TOAST, true),
    });
  }

  /** Public create-group flow: creates the group and its admin account. Does not authenticate. */
  createGroup(payload: {
    groupName: string;
    adminName: string;
    email: string;
    password: string;
    confirmPassword: string;
  }): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${environment.apiBaseUrl}/auth/create-group`,
      payload,
      { context: new HttpContext().set(SKIP_ERROR_TOAST, true) },
    );
  }

  /** Clears the local session and best-effort revokes the refresh token server-side. */
  logout(): void {
    const refreshToken = this.storage.getRefreshToken();
    this.storage.clear();
    this._currentUser.set(null);
    this.refresh$ = null;

    if (refreshToken) {
      // Fire-and-forget: the local session is already gone regardless of the outcome.
      this.http
        .post(
          `${environment.apiBaseUrl}/auth/logout`,
          { refreshToken },
          { context: skipRefreshAndToast() },
        )
        .subscribe({ error: () => {} });
    }
  }

  private storeSession(response: LoginResponse): void {
    this.storage.setToken(response.token);
    this.storage.setRefreshToken(response.refreshToken);
    this.storage.setUser(response.user);
    this._currentUser.set(response.user);
  }
}

/** Context that skips both the error toast and the 401 refresh handling (auth endpoints). */
function skipRefreshAndToast(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_TOAST, true).set(SKIP_AUTH_REFRESH, true);
}
