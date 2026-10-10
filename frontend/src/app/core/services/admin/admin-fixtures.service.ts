import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { SKIP_ERROR_TOAST } from '@core/interceptors/http-context';
import {
  ImportFixturesRequest,
  ImportFixturesResponse,
  SearchFixturesRequest,
  SearchFixturesResponse,
} from '@core/models';

/** Fixtures from the external provider: search a period, import the chosen ones into a round. */
@Injectable({ providedIn: 'root' })
export class AdminFixturesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;

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
}
