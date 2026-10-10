import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { SKIP_ERROR_TOAST } from '@core/interceptors/http-context';
import {
  AdminParticipantPredictions,
  ManualPredictionRequest,
  PredictionCoverage,
} from '@core/models';

/** Predictions an admin enters for a participant, and the round's prediction coverage. */
@Injectable({ providedIn: 'root' })
export class AdminPredictionsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;

  // --- Manual predictions -------------------------------------------------
  saveManualPredictions(roundId: string, request: ManualPredictionRequest): Observable<void> {
    return this.http.post<void>(`${this.base}/rounds/${roundId}/predictions/manual`, request);
  }

  getParticipantPredictions(
    roundId: string,
    userId: string,
  ): Observable<AdminParticipantPredictions> {
    return this.http.get<AdminParticipantPredictions>(
      `${this.base}/rounds/${roundId}/predictions/participant/${userId}`,
    );
  }

  /** Passive round-detail panel: failures degrade silently instead of popping a toast. */
  getPredictionCoverage(roundId: string): Observable<PredictionCoverage> {
    return this.http.get<PredictionCoverage>(
      `${this.base}/rounds/${roundId}/predictions/coverage`,
      { context: new HttpContext().set(SKIP_ERROR_TOAST, true) },
    );
  }
}
