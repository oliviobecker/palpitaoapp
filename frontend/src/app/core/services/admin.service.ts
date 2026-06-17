import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_ERROR_TOAST } from '../interceptors/http-context';
import {
  Absence,
  AuditLog,
  GroupSettings,
  ImportFixturesResponse,
  OcrBatch,
  Participant,
  RefreshResultsResponse,
  RegistrationRequest,
  RoundScout,
  SearchFixturesResponse,
} from '../models/models';
import { Competition, MatchPhase } from '../models/enums';

export interface SearchFixturesRequest {
  startDate: string;
  endDate: string;
  competitions?: Competition[];
  roundId?: string | null;
}

export interface ImportFixtureItem {
  externalId: string;
  competition: Competition;
  phase: MatchPhase;
  homeTeamName: string;
  awayTeamName: string;
  startsAt: string;
  source?: string;
}

export interface ImportFixturesRequest {
  fixtures: ImportFixtureItem[];
  leagueOneJustification?: string | null;
}

export interface ManualPredictionItem {
  roundMatchId: string;
  predictedHomeScore: number;
  predictedAwayScore: number;
}

export interface ManualPredictionRequest {
  userId: string;
  predictions: ManualPredictionItem[];
  overwriteExisting: boolean;
  justification?: string;
  allowAfterDeadline?: boolean;
}

export interface AdminPredictionItem {
  roundMatchId: string;
  predictedHomeScore: number;
  predictedAwayScore: number;
  source: 'Participant' | 'AdminManual' | 'AdminOcr';
  updatedAt?: string;
}

export interface AdminParticipantPredictions {
  roundId: string;
  userId: string;
  hasPredictions: boolean;
  predictions: AdminPredictionItem[];
}

export interface ParticipantRequest {
  name: string;
  email: string;
}

export interface AuditFilter {
  userId?: string;
  entityName?: string;
  from?: string;
  to?: string;
}

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;

  // --- Group settings -----------------------------------------------------
  getGroupSettings(): Observable<GroupSettings> {
    return this.http.get<GroupSettings>(`${this.base}/group/settings`);
  }

  updateGroupSettings(settings: {
    allowParticipantsToViewOthersPredictions: boolean;
    allowParticipantsToSubmitPredictions: boolean;
  }): Observable<GroupSettings> {
    return this.http.put<GroupSettings>(`${this.base}/group/settings`, settings);
  }

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

  activateParticipant(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/users/${id}/activate`, {});
  }

  deactivateParticipant(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/users/${id}/deactivate`, {});
  }

  eliminateParticipant(id: string, justification: string): Observable<void> {
    return this.http.post<void>(`${this.base}/users/${id}/eliminate`, { justification });
  }

  reactivate(id: string, justification: string): Observable<void> {
    return this.http.post<void>(`${this.base}/users/${id}/reactivate`, { justification });
  }

  getUserAbsences(userId: string): Observable<Absence[]> {
    return this.http.get<Absence[]>(`${this.base}/users/${userId}/absences`);
  }

  // --- Absences (round) ---------------------------------------------------
  getRoundAbsences(roundId: string): Observable<Absence[]> {
    return this.http.get<Absence[]>(`${this.base}/rounds/${roundId}/absences`);
  }

  overrideAbsence(
    roundId: string,
    request: { userId: string; isAbsent: boolean; justification: string },
  ): Observable<void> {
    return this.http.post<void>(`${this.base}/rounds/${roundId}/absences/override`, request);
  }

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

  // --- OCR import ---------------------------------------------------------
  importImage(roundId: string, file: File, language: string): Observable<OcrBatch> {
    const form = new FormData();
    form.append('file', file);
    form.append('language', language);
    return this.http.post<OcrBatch>(
      `${this.base}/rounds/${roundId}/predictions/import-image`,
      form,
    );
  }

  getOcrBatch(batchId: string): Observable<OcrBatch> {
    return this.http.get<OcrBatch>(`${this.base}/ocr-imports/${batchId}`);
  }

  updateOcrCandidate(
    batchId: string,
    candidateId: string,
    body: Partial<{
      userId: string | null;
      roundMatchId: string | null;
      predictedHomeScore: number | null;
      predictedAwayScore: number | null;
      reviewNotes: string | null;
    }>,
  ): Observable<OcrBatch> {
    return this.http.put<OcrBatch>(
      `${this.base}/ocr-imports/${batchId}/candidates/${candidateId}`,
      body,
    );
  }

  confirmOcr(batchId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/ocr-imports/${batchId}/confirm`, {});
  }

  cancelOcr(batchId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/ocr-imports/${batchId}/cancel`, {});
  }

  // --- External fixtures (round-by-period import) -------------------------
  searchFixtures(
    request: SearchFixturesRequest,
    options: { silent?: boolean } = {},
  ): Observable<SearchFixturesResponse> {
    // A silent search (e.g. the automatic pre-search) does not pop an error toast
    // if the external source is unavailable.
    const context = options.silent ? new HttpContext().set(SKIP_ERROR_TOAST, true) : undefined;
    return this.http.post<SearchFixturesResponse>(`${this.base}/fixtures/search`, request, {
      context,
    });
  }

  importFixtures(
    roundId: string,
    request: ImportFixturesRequest,
  ): Observable<ImportFixturesResponse> {
    return this.http.post<ImportFixturesResponse>(
      `${this.base}/rounds/${roundId}/matches/import`,
      request,
    );
  }

  // --- Match results refresh ----------------------------------------------
  refreshResults(roundId: string): Observable<RefreshResultsResponse> {
    return this.http.post<RefreshResultsResponse>(
      `${this.base}/rounds/${roundId}/refresh-results`,
      {},
    );
  }

  // --- Scout (predictions grouped by scoreline) ---------------------------
  getRoundScout(roundId: string): Observable<RoundScout> {
    return this.http.get<RoundScout>(`${this.base}/rounds/${roundId}/scout`);
  }

  // --- Registration requests ----------------------------------------------
  listRegistrationRequests(): Observable<RegistrationRequest[]> {
    return this.http.get<RegistrationRequest[]>(`${this.base}/registration-requests`);
  }

  approveRegistration(userId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/registration-requests/${userId}/approve`, {});
  }

  rejectRegistration(userId: string, reason?: string): Observable<void> {
    return this.http.post<void>(`${this.base}/registration-requests/${userId}/reject`, { reason });
  }

  // --- Audit --------------------------------------------------------------
  getAuditLogs(filter: AuditFilter = {}): Observable<AuditLog[]> {
    let params = new HttpParams();
    if (filter.userId) params = params.set('userId', filter.userId);
    if (filter.entityName) params = params.set('entityName', filter.entityName);
    if (filter.from) params = params.set('from', filter.from);
    if (filter.to) params = params.set('to', filter.to);
    return this.http.get<AuditLog[]>(`${this.base}/audit`, { params });
  }
}
