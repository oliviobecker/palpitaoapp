import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_ERROR_TOAST } from '../interceptors/http-context';
import { UserRole } from '../models/enums';
import { LoginResponse, User } from '../models/models';
import { TokenStorageService } from './token-storage.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly storage = inject(TokenStorageService);

  private readonly _currentUser = signal<User | null>(this.storage.getUser());

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
        { context: new HttpContext().set(SKIP_ERROR_TOAST, true) },
      )
      .pipe(
        tap((response) => {
          this.storage.setToken(response.token);
          this.storage.setUser(response.user);
          this._currentUser.set(response.user);
        }),
      );
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
    tournamentType: string;
    adminName: string;
    email: string;
    password: string;
    confirmPassword: string;
    allowParticipantsToViewOthersPredictions: boolean;
  }): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${environment.apiBaseUrl}/auth/create-group`,
      payload,
      { context: new HttpContext().set(SKIP_ERROR_TOAST, true) },
    );
  }

  logout(): void {
    this.storage.clear();
    this._currentUser.set(null);
  }
}
