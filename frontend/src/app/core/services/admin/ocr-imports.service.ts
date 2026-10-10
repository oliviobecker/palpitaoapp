import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { SKIP_ERROR_TOAST } from '@core/interceptors/http-context';
import { OcrBatch, OcrBatchSummary } from '@core/models';

/** Prediction import from screenshots: upload, review of the candidates, confirm or cancel. */
@Injectable({ providedIn: 'root' })
export class OcrImportsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;

  // --- OCR import ---------------------------------------------------------
  importImage(
    roundId: string,
    file: File,
    language: string,
    options: { silent?: boolean } = {},
  ): Observable<OcrBatch> {
    const form = new FormData();
    form.append('file', file);
    form.append('language', language);
    // Silent for the multi-image queue, which shows each file's error on its own row rather than
    // stacking one toast per failed upload.
    const context = options.silent ? new HttpContext().set(SKIP_ERROR_TOAST, true) : undefined;
    return this.http.post<OcrBatch>(
      `${this.base}/rounds/${roundId}/predictions/import-image`,
      form,
      { context },
    );
  }

  getOcrBatch(batchId: string): Observable<OcrBatch> {
    return this.http.get<OcrBatch>(`${this.base}/ocr-imports/${batchId}`);
  }

  listOcrBatches(roundId: string): Observable<OcrBatchSummary[]> {
    return this.http.get<OcrBatchSummary[]>(`${this.base}/rounds/${roundId}/ocr-imports`);
  }

  /** Blob body: the error interceptor cannot read a Blob error, so callers render an
   *  inline placeholder instead of relying on a toast. */
  getOcrImage(batchId: string): Observable<Blob> {
    return this.http.get(`${this.base}/ocr-imports/${batchId}/image`, {
      responseType: 'blob',
      context: new HttpContext().set(SKIP_ERROR_TOAST, true),
    });
  }

  updateOcrCandidate(
    batchId: string,
    candidateId: string,
    body: Partial<{
      userId: string | null;
      roundMatchId: string | null;
      predictedHomeScore: number | null;
      predictedAwayScore: number | null;
      reviewNotes: string | null;
    }>,
  ): Observable<OcrBatch> {
    return this.http.put<OcrBatch>(
      `${this.base}/ocr-imports/${batchId}/candidates/${candidateId}`,
      body,
    );
  }

  deleteOcrCandidate(batchId: string, candidateId: string): Observable<OcrBatch> {
    return this.http.delete<OcrBatch>(
      `${this.base}/ocr-imports/${batchId}/candidates/${candidateId}`,
    );
  }

  confirmOcr(batchId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/ocr-imports/${batchId}/confirm`, {});
  }

  cancelOcr(batchId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/ocr-imports/${batchId}/cancel`, {});
  }
}
