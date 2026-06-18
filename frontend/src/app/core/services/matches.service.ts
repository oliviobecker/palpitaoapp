import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { RoundMatch } from '../models/models';

@Injectable({ providedIn: 'root' })
export class MatchesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/matches`;

  update(id: string, request: unknown): Observable<RoundMatch> {
    return this.http.put<RoundMatch>(`${this.base}/${id}`, request);
  }

  remove(id: string, justification?: string): Observable<void> {
    const query = justification ? `?justification=${encodeURIComponent(justification)}` : '';
    return this.http.delete<void>(`${this.base}/${id}${query}`);
  }

  setResult(id: string, request: { homeScore: number; awayScore: number }): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/result`, request);
  }
}
