import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { RegistrationRequest } from '@core/models';

/** Sign-ups waiting for the group admin's decision. */
@Injectable({ providedIn: 'root' })
export class AdminRegistrationRequestsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;

  // --- Registration requests ----------------------------------------------
  listRegistrationRequests(): Observable<RegistrationRequest[]> {
    return this.http.get<RegistrationRequest[]>(`${this.base}/registration-requests`);
  }

  approveRegistration(userId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/registration-requests/${userId}/approve`, {});
  }

  rejectRegistration(userId: string, reason?: string): Observable<void> {
    return this.http.post<void>(`${this.base}/registration-requests/${userId}/reject`, { reason });
  }
}
