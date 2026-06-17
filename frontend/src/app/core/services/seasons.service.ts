import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TournamentType } from '../models/enums';
import { Season } from '../models/models';

export interface SeasonRequest {
  name: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
  /** Set on creation; ignored on update (the certame type is immutable). */
  tournamentType: TournamentType;
  allowParticipantsToViewOthersPredictions: boolean;
  allowParticipantsToSubmitPredictions: boolean;
}

@Injectable({ providedIn: 'root' })
export class SeasonsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/seasons`;

  list(): Observable<Season[]> {
    return this.http.get<Season[]>(this.base);
  }

  getActive(): Observable<Season | null> {
    return this.http.get<Season | null>(`${this.base}/active`);
  }

  create(request: SeasonRequest): Observable<Season> {
    return this.http.post<Season>(this.base, request);
  }

  update(id: string, request: SeasonRequest): Observable<Season> {
    return this.http.put<Season>(`${this.base}/${id}`, request);
  }

  activate(id: string): Observable<Season> {
    return this.http.post<Season>(`${this.base}/${id}/activate`, {});
  }
}
