import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { AuditFilter, AuditLog } from '@core/models';

/** The admin audit trail. */
@Injectable({ providedIn: 'root' })
export class AdminAuditService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;

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
