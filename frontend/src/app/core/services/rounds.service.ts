import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Round,
  RoundMatch,
  RoundResults,
  RoundSummary,
  TemporaryStandings,
} from '../models/models';

@Injectable({ providedIn: 'root' })
export class RoundsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/rounds`;

  getAll(): Observable<RoundSummary[]> {
    return this.http.get<RoundSummary[]>(this.base);
  }

  getById(id: string): Observable<Round> {
    return this.http.get<Round>(`${this.base}/${id}`);
  }

  create(request: {
    seasonId: string;
    number: number;
    title?: string | null;
    startDate?: string | null;
    endDate?: string | null;
  }): Observable<Round> {
    return this.http.post<Round>(this.base, request);
  }

  update(
    id: string,
    request: {
      number: number;
      title?: string | null;
      startDate?: string | null;
      endDate?: string | null;
    },
  ): Observable<Round> {
    return this.http.put<Round>(`${this.base}/${id}`, request);
  }

  publish(id: string): Observable<Round> {
    return this.http.post<Round>(`${this.base}/${id}/publish`, {});
  }

  lock(id: string): Observable<Round> {
    return this.http.post<Round>(`${this.base}/${id}/lock`, {});
  }

  cancel(id: string): Observable<Round> {
    return this.http.post<Round>(`${this.base}/${id}/cancel`, {});
  }

  addMatch(roundId: string, request: unknown): Observable<RoundMatch> {
    return this.http.post<RoundMatch>(`${this.base}/${roundId}/matches`, request);
  }

  score(roundId: string): Observable<RoundResults> {
    return this.http.post<RoundResults>(`${this.base}/${roundId}/score`, {});
  }

  getResults(roundId: string): Observable<RoundResults> {
    return this.http.get<RoundResults>(`${this.base}/${roundId}/results`);
  }

  getTemporaryStandings(roundId: string): Observable<TemporaryStandings> {
    return this.http.get<TemporaryStandings>(`${this.base}/${roundId}/temporary-standings`);
  }
}
