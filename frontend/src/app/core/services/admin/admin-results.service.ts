import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { RefreshResultsResponse } from '@core/models';

/** Pulling a round's results from the provider. */
@Injectable({ providedIn: 'root' })
export class AdminResultsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;

  // --- Match results refresh ----------------------------------------------
  refreshResults(roundId: string): Observable<RefreshResultsResponse> {
    return this.http.post<RefreshResultsResponse>(
      `${this.base}/rounds/${roundId}/refresh-results`,
      {},
    );
  }
}
