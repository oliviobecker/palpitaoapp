import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ScoringConfig, ScoringConfigRequest } from '../models/models';

/** Reads and edits a season's scoring ruleset (base points, categories, multipliers, classic teams). */
@Injectable({ providedIn: 'root' })
export class ScoringConfigService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/seasons`;

  get(seasonId: string): Observable<ScoringConfig> {
    return this.http.get<ScoringConfig>(`${this.base}/${seasonId}/scoring-config`);
  }

  update(seasonId: string, request: ScoringConfigRequest): Observable<ScoringConfig> {
    return this.http.put<ScoringConfig>(`${this.base}/${seasonId}/scoring-config`, request);
  }
}
