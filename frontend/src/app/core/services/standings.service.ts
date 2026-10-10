import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { Standing } from '@core/models';

@Injectable({ providedIn: 'root' })
export class StandingsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/seasons`;

  getStandings(seasonId: string): Observable<Standing[]> {
    return this.http.get<Standing[]>(`${this.base}/${seasonId}/standings`);
  }

  recalculate(seasonId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${seasonId}/recalculate`, {});
  }
}
