import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { Participant, ParticipantRequest } from '@core/models';

/** Group participants, as the admin manages them (admin/users). */
@Injectable({ providedIn: 'root' })
export class AdminUsersService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;

  // --- Participants -------------------------------------------------------
  listParticipants(): Observable<Participant[]> {
    return this.http.get<Participant[]>(`${this.base}/users`);
  }

  createParticipant(request: ParticipantRequest & { password: string }): Observable<Participant> {
    return this.http.post<Participant>(`${this.base}/users`, request);
  }

  updateParticipant(id: string, request: ParticipantRequest): Observable<Participant> {
    return this.http.put<Participant>(`${this.base}/users/${id}`, request);
  }

  activateParticipant(id: string, absentRoundIds: string[] = []): Observable<void> {
    return this.http.post<void>(`${this.base}/users/${id}/activate`, { absentRoundIds });
  }

  deactivateParticipant(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/users/${id}/deactivate`, {});
  }

  eliminateParticipant(id: string, justification: string): Observable<void> {
    return this.http.post<void>(`${this.base}/users/${id}/eliminate`, { justification });
  }
}
