import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { RoundScout } from '@core/models';

/** A round's predictions grouped by scoreline. */
@Injectable({ providedIn: 'root' })
export class AdminScoutService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;

  // --- Scout (predictions grouped by scoreline) ---------------------------
  getRoundScout(roundId: string): Observable<RoundScout> {
    return this.http.get<RoundScout>(`${this.base}/rounds/${roundId}/scout`);
  }
}
