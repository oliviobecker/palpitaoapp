import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { SKIP_ERROR_TOAST } from '@core/interceptors/http-context';
import {
  Absence,
  AbsenceCandidateRound,
  AbsenceReviewDecision,
  AbsenceReviewResult,
  AbsenceReviewRound,
} from '@core/models';

/** A participant's absences, their review and per-round overrides, and reactivation. */
@Injectable({ providedIn: 'root' })
export class AdminAbsencesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;

  getUserAbsences(userId: string): Observable<Absence[]> {
    return this.http.get<Absence[]>(`${this.base}/users/${userId}/absences`);
  }

  /**
   * Rounds that closed while the participant was out. Passive pre-check for the
   * (re)activation dialog: a failure here must not block the action, so no error toast.
   */
  getAbsenceCandidateRounds(userId: string): Observable<AbsenceCandidateRound[]> {
    return this.http.get<AbsenceCandidateRound[]>(
      `${this.base}/users/${userId}/absence-candidates`,
      { context: new HttpContext().set(SKIP_ERROR_TOAST, true) },
    );
  }

  reactivate(id: string, justification: string, absentRoundIds: string[] = []): Observable<void> {
    return this.http.post<void>(`${this.base}/users/${id}/reactivate`, {
      justification,
      absentRoundIds,
    });
  }

  /** Closed rounds the participant counts absent in (or has an override for): the review dialog. */
  getAbsenceReviewRounds(userId: string): Observable<AbsenceReviewRound[]> {
    return this.http.get<AbsenceReviewRound[]>(`${this.base}/users/${userId}/absence-review`);
  }

  /** One explicit decision per reviewed round; a scored round that changes replays the season. */
  reviewAbsences(
    userId: string,
    justification: string,
    rounds: AbsenceReviewDecision[],
  ): Observable<AbsenceReviewResult> {
    return this.http.post<AbsenceReviewResult>(`${this.base}/users/${userId}/absence-review`, {
      justification,
      rounds,
    });
  }

  overrideAbsence(
    roundId: string,
    request: { userId: string; isAbsent: boolean; justification: string },
  ): Observable<void> {
    return this.http.post<void>(`${this.base}/rounds/${roundId}/absences/override`, request);
  }
}
