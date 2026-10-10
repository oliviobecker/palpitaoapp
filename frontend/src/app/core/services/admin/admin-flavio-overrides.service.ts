import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { RoundFlavioOverrides } from '@core/models';

/** Per-round exemptions from the Flávio Rule. */
@Injectable({ providedIn: 'root' })
export class AdminFlavioOverridesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;

  getFlavioOverrides(roundId: string): Observable<RoundFlavioOverrides> {
    return this.http.get<RoundFlavioOverrides>(`${this.base}/rounds/${roundId}/flavio-overrides`);
  }

  setFlavioOverride(
    roundId: string,
    request: { userId: string; isExempt: boolean; justification: string },
  ): Observable<void> {
    return this.http.put<void>(`${this.base}/rounds/${roundId}/flavio-overrides`, request);
  }
}
