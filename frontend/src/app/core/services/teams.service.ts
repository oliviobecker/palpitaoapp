import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Team } from '../models/models';

/** Teams catalogue (read endpoint implemented in a later phase). */
@Injectable({ providedIn: 'root' })
export class TeamsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/teams`;

  list(): Observable<Team[]> {
    return this.http.get<Team[]>(this.base);
  }
}
