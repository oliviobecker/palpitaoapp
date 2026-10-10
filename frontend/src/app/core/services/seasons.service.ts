import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { Season, SeasonRequest } from '@core/models';

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

  /** Mints a new public key. The previously shared link stops working immediately. */
  regeneratePublicKey(id: string): Observable<Season> {
    return this.http.post<Season>(`${this.base}/${id}/public-key/regenerate`, {});
  }
}
