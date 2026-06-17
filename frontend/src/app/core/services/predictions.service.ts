import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_ERROR_TOAST } from '../interceptors/http-context';
import { Mirror, MyPredictions } from '../models/models';

export interface PredictionItem {
  roundMatchId: string;
  predictedHomeScore: number;
  predictedAwayScore: number;
}

@Injectable({ providedIn: 'root' })
export class PredictionsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/rounds`;

  getMine(roundId: string): Observable<MyPredictions> {
    return this.http.get<MyPredictions>(`${this.base}/${roundId}/predictions/me`);
  }

  save(roundId: string, predictions: PredictionItem[]): Observable<MyPredictions> {
    return this.http.post<MyPredictions>(`${this.base}/${roundId}/predictions`, { predictions });
  }

  update(roundId: string, predictions: PredictionItem[]): Observable<MyPredictions> {
    return this.http.put<MyPredictions>(`${this.base}/${roundId}/predictions`, { predictions });
  }

  getMirror(roundId: string): Observable<Mirror> {
    // Before the lock the API returns 422 — that is an expected state shown as an
    // empty state, so we skip the global error toast.
    return this.http.get<Mirror>(`${this.base}/${roundId}/mirror`, {
      context: new HttpContext().set(SKIP_ERROR_TOAST, true),
    });
  }
}
