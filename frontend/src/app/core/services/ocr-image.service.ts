import { Injectable, inject } from '@angular/core';
import { Observable, map, of, shareReplay, tap } from 'rxjs';
import { AdminService } from './admin.service';

/**
 * Loads stored OCR upload images as object URLs.
 *
 * The image endpoint is authenticated, so a plain `<img src>` would be rejected — the bytes are
 * fetched through `HttpClient` (picking up the bearer and `X-Group-Id` interceptors) and wrapped
 * in a `blob:` URL. Object URLs pin their blob until revoked, so ownership lives here rather than
 * being scattered across components: callers `release()` one or `releaseAll()` on destroy.
 */
@Injectable({ providedIn: 'root' })
export class OcrImageService {
  private readonly api = inject(AdminService);

  /** batchId -> object URL, so re-opening the viewer does not refetch the bytes. */
  private readonly urls = new Map<string, string>();
  /** batchId -> in-flight request, so concurrent loads share one download. */
  private readonly pending = new Map<string, Observable<string>>();

  load(batchId: string): Observable<string> {
    const cached = this.urls.get(batchId);
    if (cached) {
      return of(cached);
    }
    const inFlight = this.pending.get(batchId);
    if (inFlight) {
      return inFlight;
    }

    const request = this.api.getOcrImage(batchId).pipe(
      map((blob) => URL.createObjectURL(blob)),
      tap({
        next: (url) => {
          this.urls.set(batchId, url);
          this.pending.delete(batchId);
        },
        error: () => this.pending.delete(batchId),
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    this.pending.set(batchId, request);
    return request;
  }

  /** The already-loaded URL for a batch, or null when it has not been fetched. */
  urlFor(batchId: string): string | null {
    return this.urls.get(batchId) ?? null;
  }

  release(batchId: string): void {
    const url = this.urls.get(batchId);
    if (url) {
      URL.revokeObjectURL(url);
      this.urls.delete(batchId);
    }
    this.pending.delete(batchId);
  }

  releaseAll(): void {
    this.urls.forEach((url) => URL.revokeObjectURL(url));
    this.urls.clear();
    this.pending.clear();
  }
}
