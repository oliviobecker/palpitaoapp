import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MyGroup, PublicGroup } from '../models/models';

@Injectable({ providedIn: 'root' })
export class GroupsService {
  private readonly http = inject(HttpClient);

  /** Active groups for the public registration picker (unauthenticated). */
  listActive(): Observable<PublicGroup[]> {
    return this.http.get<PublicGroup[]>(`${environment.apiBaseUrl}/public/groups`);
  }

  /** Groups the authenticated user has approved access to. */
  myGroups(): Observable<MyGroup[]> {
    return this.http.get<MyGroup[]>(`${environment.apiBaseUrl}/auth/my-groups`);
  }

  /** The user's not-yet-approved memberships (pending/rejected), for the awaiting-approval screen. */
  pendingGroups(): Observable<MyGroup[]> {
    return this.http.get<MyGroup[]>(`${environment.apiBaseUrl}/auth/my-groups/pending`);
  }
}
