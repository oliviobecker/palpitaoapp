import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_ERROR_TOAST, SKIP_TENANT_HEADERS } from '../interceptors/http-context';
import { PublicRound, PublicSeason, PublicStandingRow } from '../models/models';

/**
 * The public standings link. Every call opts out of the session and group headers: the
 * season's key is the whole credential, and a browser that happens to be logged into
 * another group would otherwise send a tenant that scopes this season away.
 */
@Injectable({ providedIn: 'root' })
export class PublicStandingsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/public/seasons`;

  /** Hyphens and case are cosmetic; the API normalizes, but keep URLs tidy. */
  private static normalize(key: string): string {
    return key.replace(/[^0-9a-fA-F]/g, '').toUpperCase();
  }

  private get context(): HttpContext {
    // SKIP_ERROR_TOAST as well: a stale or unpublished link is an expected outcome here,
    // and the screen already explains it — a red toast on top of that is just noise.
    return new HttpContext().set(SKIP_TENANT_HEADERS, true).set(SKIP_ERROR_TOAST, true);
  }

  season(key: string): Observable<PublicSeason> {
    return this.http.get<PublicSeason>(`${this.base}/${PublicStandingsService.normalize(key)}`, {
      context: this.context,
    });
  }

  standings(key: string): Observable<PublicStandingRow[]> {
    return this.http.get<PublicStandingRow[]>(
      `${this.base}/${PublicStandingsService.normalize(key)}/standings`,
      { context: this.context },
    );
  }

  round(key: string, number: number): Observable<PublicRound> {
    return this.http.get<PublicRound>(
      `${this.base}/${PublicStandingsService.normalize(key)}/rounds/${number}`,
      { context: this.context },
    );
  }
}
