import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { Team, TeamSyncResponse } from '@core/models';
import { Competition } from '@core/models/enums';

/** The global team catalogue: the list, and the admin's division edits and squad-list sync. */
@Injectable({ providedIn: 'root' })
export class TeamsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/teams`;
  private readonly adminBase = `${environment.apiBaseUrl}/admin/teams`;

  list(): Observable<Team[]> {
    return this.http.get<Team[]>(this.base);
  }

  // --- Team catalogue -----------------------------------------------------
  updateTeamDivision(teamId: string, division: Competition | null): Observable<Team> {
    return this.http.patch<Team>(`${this.adminBase}/${teamId}`, { division });
  }

  /** Compares the catalogue with the external squad lists without writing anything. */
  syncTeamsPreview(): Observable<TeamSyncResponse> {
    return this.http.post<TeamSyncResponse>(`${this.adminBase}/sync-preview`, {});
  }

  syncTeamsApply(): Observable<TeamSyncResponse> {
    return this.http.post<TeamSyncResponse>(`${this.adminBase}/sync-apply`, {});
  }
}
