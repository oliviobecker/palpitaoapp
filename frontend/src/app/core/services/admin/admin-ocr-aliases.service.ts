import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { OcrParticipantAlias } from '@core/models';

/** The participant names OCR has learned per group (admin/ocr-aliases). */
@Injectable({ providedIn: 'root' })
export class AdminOcrAliasesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;

  // --- OCR participant aliases --------------------------------------------
  listOcrAliases(): Observable<OcrParticipantAlias[]> {
    return this.http.get<OcrParticipantAlias[]>(`${this.base}/ocr-aliases`);
  }

  createOcrAlias(aliasRaw: string, userId: string): Observable<OcrParticipantAlias> {
    return this.http.post<OcrParticipantAlias>(`${this.base}/ocr-aliases`, { aliasRaw, userId });
  }

  /** Only the participant can change — the alias text is keyed and immutable. */
  updateOcrAlias(id: string, userId: string): Observable<OcrParticipantAlias> {
    return this.http.put<OcrParticipantAlias>(`${this.base}/ocr-aliases/${id}`, { userId });
  }

  deleteOcrAlias(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/ocr-aliases/${id}`);
  }
}
